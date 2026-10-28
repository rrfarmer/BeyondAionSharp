using System.Buffers.Binary;
using Aion.GameServer.Ai;
using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Movement;

internal enum SkyTempleArrival { Arrived, OutOfRange, BlockedLineOfSight, Unsupported }
internal enum SkyTempleMoveKind { Move, Wait, Arrived, GiveUp, Blocked, ReturnTeleport, ReturnCompleted }
internal sealed record SkyTempleMoveStep(SkyTempleMoveKind Kind,RetailSearchPoint Next,RetailSearchPoint Destination,
    byte Heading,string? Detail=null,RetailNpcPathFollowResult? Follow=null,RetailSearchPoint? Start=null);
internal sealed record SkyTempleGiveUpLease(long Operation,long OwnerLife,VisibleObject? Target,long? TargetLife,
    int Map,int Instance,string ProfileGeneration,AIState State=AIState.FIGHT,bool PointMovement=false);

/// <summary>One managed owner, called only by the existing200ms NPC move task. No timers,
/// bot navigation, fake retail callback transcripts or unchecked straight fallback. Return
/// teleport commands require the computed native follower1/4 and a separate owned-goal commit.
/// Native motion storage is replaced by authoritative4.8 current positions/SM_MOVE transport.</summary>
internal sealed class SkyTempleNpcNavigation(Npc owner)
{
    private SkyTempleNpcRoute? route;
    private Creature? targetIdentity;
    private VisibleObject? ownerTargetIdentity;
    private SkyTempleNpcApproachGoal? approachIdentity;
    private int instance=-1;
    private string? profileGeneration;
    private bool returning;
    private readonly SkyTempleNpcNavigationTiming timing=new();
    private int revision;
    private RetailSearchPoint previousTarget;
    private string? lastFailure;
    private int maximumFailedChaseMilliseconds;
    private long operation;
    private SkyTempleMoveStep? pendingGiveUp;
    private SkyTempleGiveUpLease? giveUpLease;
    private string? lastSceneRevision;
    private RetailSearchPoint? requestedGoal;
    private bool pointMovement;
    internal string? LastFailure=>lastFailure;
    internal int RemainingWaypoints=>route is null?0:System.Math.Max(0,route.Points.Count-route.Index);
    internal bool IsReturning=>returning;
    internal bool HasRouteFailure=>timing.HasFailure;
    internal string? SceneRevision=>route?.SceneRevision??lastSceneRevision;
    internal RetailSearchPoint? RequestedGoal=>requestedGoal;
    internal static bool Enabled(Npc npc)=>SkyTempleNpcProfileCatalog.TryGet(npc,out _);

    internal void Cancel()
    {ClearRoute();pendingGiveUp=null;giveUpLease=null;operation++;}
    private void ClearRoute()
    {route=null;targetIdentity=null;ownerTargetIdentity=null;approachIdentity=null;instance=-1;profileGeneration=null;timing.Reset();lastFailure=null;revision++;}

    // A GiveUp step is only a proposal. Its private identity and current gameplay
    // identities must survive both the route cancellation and host stop callbacks.
    // Callers hold nav only for these checks, never while dispatching lifecycle/AI.
    internal SkyTempleGiveUpLease? ClaimGiveUp(SkyTempleMoveStep step)
    {
        if(!ReferenceEquals(step,pendingGiveUp)||giveUpLease is not { } lease||!IsCurrentGiveUp(lease))return null;
        ClearRoute();pendingGiveUp=null;return lease;
    }
    internal bool IsCurrentGiveUp(SkyTempleGiveUpLease lease)=>ReferenceEquals(lease,giveUpLease)&&lease.Operation==operation&&
        owner.IsSpawned()&&!owner.IsDead()&&owner.GetController().RetailLifeEpoch==lease.OwnerLife&&
        owner.GetAi().GetState()==lease.State&&
        (lease.PointMovement?lease.Target is null&&lease.State is AIState.WALKING or AIState.FORCED_WALKING:lease.State==AIState.FIGHT)&&
        owner.GetWorldId()==lease.Map&&owner.GetInstanceId()==lease.Instance&&
        ReferenceEquals(owner.GetTarget(),lease.Target)&&
        (lease.Target is not Creature target||target.GetController().RetailLifeEpoch==lease.TargetLife)&&
        SkyTempleNpcProfileCatalog.TryGet(owner,out var p)&&p.Generation==lease.ProfileGeneration;
    internal bool FinishGiveUp(SkyTempleGiveUpLease lease)
    {
        if(!IsCurrentGiveUp(lease))return false;
        giveUpLease=null;return true;
    }
    internal void RecoveryWait(uint now)
    {operation++;pendingGiveUp=null;giveUpLease=null;timing.MovementMilliseconds(now);}
    internal SkyTempleArrival Arrival(Creature? target,float? selectedRange=null)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var profile)||target is null||!ValidTarget(target)||owner.IsFlying())return SkyTempleArrival.Unsupported;
        if(ReferenceEquals(owner,target))return SkyTempleArrival.Arrived;
        try
        {
            var assets=SkyTempleNpcNavigationAssets.For(owner);
            var scene=assets.CurrentScene(owner);var body=Body(profile);
            var planner=new SkyTempleNpcRoutePlanner(assets,scene,body,owner.GetObjectId().ToString());
            float reach=Reach(profile,target,selectedRange);
            var from=Position(owner);var goal=Position(target);
            if(Squared(from,goal)>reach*reach+.01f)return SkyTempleArrival.OutOfRange;
            return planner.Visible(from,body.Upper,goal,target.GetObjectTemplate().GetBoundRadius().GetUpper())?
                SkyTempleArrival.Arrived:SkyTempleArrival.BlockedLineOfSight;
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
        {lastFailure=ex.Message;return SkyTempleArrival.Unsupported;}
    }
    internal SkyTempleMoveStep Tick(bool chase,RetailSearchPoint point,uint now,SkyTempleNpcApproachGoal? approach=null,
        VisibleObject? expectedOwnerTarget=null,bool requiresApproach=false)
    {
        operation++;pendingGiveUp=null;giveUpLease=null;
        requestedGoal=chase&&owner.GetTarget() is VisibleObject targetGoal?Position(approach?.Target??targetGoal):point;
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var profile))return Stop(SkyTempleMoveKind.Blocked,"cohort-not-admitted");
        maximumFailedChaseMilliseconds=profile.MaxChaseMilliseconds;
        if(owner.IsDead()||!owner.IsSpawned())return Stop(SkyTempleMoveKind.Blocked,"actor-lifetime-ended");
        bool isReturning=owner.GetAi().GetState()==AIState.RETURNING;
        pointMovement=!chase&&!isReturning&&owner.GetAi().GetState() is AIState.WALKING or AIState.FORCED_WALKING;
        if(approach is { } selected&&(!CurrentGoal(selected)||!ReferenceEquals(expectedOwnerTarget,owner.GetTarget())))
            return Failure("selected-action-lifetime-or-target-changed",now,isReturning);
        Creature? target=chase?(approach?.Target??owner.GetTarget() as Creature):null;
        if(chase&&(target is null||!ValidTarget(target)))return Stop(SkyTempleMoveKind.GiveUp,"target-lifetime-or-instance");
        if(requiresApproach&&approach is null)return Failure("selected-action-goal-unavailable",now,isReturning);
        if(owner.IsFlying())return Failure("unsupported-alternate-motion",now,isReturning);
        try
        {
            var assets=SkyTempleNpcNavigationAssets.For(owner);var scene=assets.CurrentScene(owner);
            lastSceneRevision=scene.Declaration.SceneRevision;
            var body=Body(profile);var planner=new SkyTempleNpcRoutePlanner(assets,scene,body,owner.GetObjectId().ToString());
            var from=Position(owner);var actual=target is null?point:Position(target);
            ownerTargetIdentity=owner.GetTarget();approachIdentity=approach;
            if(instance!=owner.GetInstanceId()||profileGeneration!=profile.Generation||returning!=isReturning||!ReferenceEquals(targetIdentity,target))
            {
                route=null;timing.Reset();lastFailure=null;revision++;
                instance=owner.GetInstanceId();profileGeneration=profile.Generation;returning=isReturning;targetIdentity=target;
            }
            // NpcGameStats selects the matched walk/run/combat base, then applies current
            // owned effects. Reading the raw profile here would silently ignore slows/roots.
            float budget=owner.GetGameStats().GetMovementSpeedFloat()*timing.MovementMilliseconds(now)/1000f;
            if(target is not null&&Arrival(target,approach?.Range)==SkyTempleArrival.Arrived)return Stop(SkyTempleMoveKind.Arrived,null);
            if(target is null&&(isReturning?SkyTempleNpcReturnState.SamePoint(from,point):Squared(from,point)<.01f))return Stop(SkyTempleMoveKind.Arrived,null);
            if(!(budget>0)||!float.IsFinite(budget))return Failure("nonpositive-movement-speed",now,isReturning);
            if(route is not null&&(route.SceneRevision!=scene.Declaration.SceneRevision||
                (target is null?Squared(route.RequestedGoal??route.Goal,point)>.0001f:Squared(actual,previousTarget)>.25f)))route=null;
            if(route is null)
            {
                if(!timing.CanSearch(now))return Failure(lastFailure??"retry-gate",now,isReturning);
                timing.SearchStarted(now);
                var goal=actual;
                // Current host position samples supply real metres/second. No undeclared native velocity is guessed.
                if(target is not null&&timing.PredictionMilliseconds(now) is { } sampleMilliseconds)
                {
                    float dt=sampleMilliseconds/1000f;
                    var velocity=new RetailSearchPoint((actual.X-previousTarget.X)/dt,(actual.Y-previousTarget.Y)/dt,(actual.Z-previousTarget.Z)/dt);
                    if(Squared(default,velocity)<=400)
                    {
                        var lead=new RetailSearchPoint(actual.X+velocity.X*.667f,actual.Y+velocity.Y*.667f,System.MathF.Max(actual.Z+velocity.Z*.667f,actual.Z)+1.5f);
                        var projected=planner.Ground(lead);
                        if(projected is { } p&&planner.Surface(p) is not null&&planner.StepClear(from,p))goal=p;
                    }
                }
                previousTarget=actual;timing.TargetSampled(now);
                string request=owner.GetObjectId()+"/"+(++revision);
                var found=planner.Find(from,goal,request);
                if(!found.Success&&target is not null)
                {
                    // Native angular fallback:12actual table directions, source-reconstructed heading.
                    float reach=Reach(profile,target,approach?.Range),radius=System.MathF.Min(reach,2),margin=reach*reach-System.MathF.Max(reach*reach*.02f,.21f);
                    var angle=RetailNpcNativeHeading.Atan2(BitConverter.SingleToUInt32Bits(actual.Y-from.Y),
                        BitConverter.SingleToUInt32Bits(actual.X-from.X),SkyTempleNpcNavigationAssets.Floating);
                    if(!angle.Success)return Failure("unsupported-heading",now,isReturning);
                    int heading=RetailNpcNativeHeading.QuantizeMoveToGoal(angle.ReturnWord!.Value);
                    for(int i=0;i<12&&!found.Success;i++)
                    {
                        int direction=(heading+i*10)%120;
                        float x=BinaryPrimitives.ReadSingleLittleEndian(RetailNpcGroundApproachPlan.TableXBytes.Slice(direction*4,4));
                        float y=BinaryPrimitives.ReadSingleLittleEndian(RetailNpcGroundApproachPlan.TableYBytes.Slice(direction*4,4));
                        foreach(float offset in new[]{radius*.75f,2f,radius*.3751f})
                        {
                            var candidate=planner.Ground(new(actual.X+x*offset,actual.Y+y*offset,actual.Z+System.MathF.Max(System.MathF.Abs(actual.Z-from.Z)+1,2)));
                            if(candidate is not { } p||Squared(actual,p)>=margin||planner.Surface(p) is null||
                                !planner.Visible(p,body.Upper,actual,target.GetObjectTemplate().GetBoundRadius().GetUpper()))continue;
                            found=planner.Find(from,p,request+"/angular"+i);if(found.Success)break;
                        }
                    }
                }
                if(!found.Success)return Failure(found.Failure??"no-route",now,isReturning);
                route=found.Route;timing.SearchSucceeded();lastFailure=null;
            }
            var follow=planner.Follow(route!,from,budget);
            if(!follow.HasNativeOutcome)return Failure(follow.Detail??follow.Status.ToString(),now,isReturning);
            if(isReturning&&follow.NativeValue is 1 or 4)
                return new(SkyTempleMoveKind.ReturnTeleport,from,point,owner.GetHeading(),"native-return-follow-"+follow.NativeValue,follow,from);
            if(isReturning&&follow.NativeValue==2)
                return new(SkyTempleMoveKind.ReturnCompleted,from,point,owner.GetHeading(),"native-return-completed-not-position-equality",follow,from);
            if(follow.NativeValue==4)return Stop(SkyTempleMoveKind.Wait,null);
            if(follow.NativeValue==2)
            {
                route=null;
                // A native missing-start walk can produce a clipped prefix. Consuming
                // it does not put the actor at its requested home. The actual-position
                // arrival check above remains the only return completion gate.
                return Failure(target is null?"return-segment-exhausted-before-home":"approach-exhausted-without-arrival",now,isReturning);
            }
            //225d80 owns the ordinary follower's contact/support queries (upper
            //clamped to1.5..2m).123190 stages its raw0 result directly. An extra
            //full-body2f3ab0 probe is not in that native chain: large actors can
            //be falsely blocked by their extended5m+ upper/body ray after a valid
            //followed step. Preserve the computed native result and the current
            //scene/target/position checks in CanApply and the actual-position ACK.
            if(follow.NativeValue!=0||follow.NextPosition is not { } next)
            {route=null;return Failure("route-step-blocked-or-unsupported",now,isReturning);}
            int? headingWord=RetailNpcNativeHeading.Look(from,next,owner.GetHeading(),SkyTempleNpcNavigationAssets.Floating,out _);
            if(headingWord is null)return Failure("unsupported-heading",now,isReturning);
            return new(SkyTempleMoveKind.Move,next,follow.DesiredWaypoint??route!.Goal,(byte)headingWord.Value,route!.Resolution,follow,from);
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
        {route=null;return Failure(ex.Message,now,isReturning);}
    }
    internal bool CanApply(SkyTempleMoveStep step)
    {
        if(step.Start!=Position(owner)||owner.IsDead()||!owner.IsSpawned()||owner.GetInstanceId()!=instance||
            !ReferenceEquals(owner.GetTarget(),ownerTargetIdentity)||(targetIdentity is not null&&!ValidTarget(targetIdentity))||
            (approachIdentity is { } goal&&!CurrentGoal(goal)))return false;
        try
        {
            var assets=SkyTempleNpcNavigationAssets.For(owner);
            if(!assets.HasHostSupport(owner,step.Next))
            {lastFailure="host48-surface-incompatible-with-native-step";return false;}
            return assets.CurrentScene(owner).Declaration.SceneRevision==step.Follow?.SceneDeclaration?.SceneRevision;
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or InvalidOperationException)
        {lastFailure=ex.Message;return false;}
    }
    internal void Confirm(SkyTempleMoveStep step,bool applied)
    {
        if(!applied||route is null||step.Follow is not { } f){route=null;return;}
        var context=new RetailNpcPathProgressContext(f.Epoch!,f.Epoch!,true,RetailGroundPathSearch.SupportedNpcBinarySha256,
            f.ActorDeclaration!.EvidenceSha256);
        var ack=RetailNpcPathProgressPlan.Acknowledge(context,f.FinalProgress!.Value,
            new(true,f.ActorDeclaration.EvidenceSha256));
        if(!ack.Success){route=null;return;}
        route=route with {Index=f.FinalPathIndex!.Value,Progress=ack.Progress!.Value};
        timing.ProgressConfirmed();lastFailure=null;
    }
    private SkyTempleMoveStep Failure(string detail,uint now,bool isReturning)
    {
        lastFailure=detail;
        // Explicit managed recovery bound. A physical wall is never declared a proved graph no-route.
        return Stop(timing.FailureExpired(now,isReturning,maximumFailedChaseMilliseconds)?
            SkyTempleMoveKind.GiveUp:SkyTempleMoveKind.Blocked,detail);
    }
    private SkyTempleMoveStep Stop(SkyTempleMoveKind kind,string? detail)
    {
        var step=new SkyTempleMoveStep(kind,Position(owner),requestedGoal??Position(owner),owner.GetHeading(),detail);
        if(kind==SkyTempleMoveKind.GiveUp&&SkyTempleNpcProfileCatalog.TryGet(owner,out var profile))
        {
            var target=owner.GetTarget();
            giveUpLease=new(operation,owner.GetController().RetailLifeEpoch,target,
                (target as Creature)?.GetController().RetailLifeEpoch,owner.GetWorldId(),owner.GetInstanceId(),profile.Generation,
                owner.GetAi().GetState(),pointMovement);
            pendingGiveUp=step;
        }
        return step;
    }
    private bool ValidTarget(Creature t)=>t.IsSpawned()&&!t.IsDead()&&!t.GetLifeStats().IsAboutToDie()&&
        t.GetWorldId()==owner.GetWorldId()&&t.GetInstanceId()==owner.GetInstanceId();
    internal bool CurrentGoal(SkyTempleNpcApproachGoal goal)=>owner.IsSpawned()&&!owner.IsDead()&&SkyTempleNpcProfileCatalog.TryGet(owner,out var p)&&
        p.Generation==goal.Generation&&owner.GetController().RetailLifeEpoch==goal.OwnerLifeEpoch&&
        goal.Target.GetController().RetailLifeEpoch==goal.TargetLifeEpoch&&ValidTarget(goal.Target)&&
        float.IsFinite(goal.Range)&&goal.Range>=0;
    private static SkyTempleNpcBody Body(SkyTempleNpcProfile p)=>new(p.Bounds.Front,p.Bounds.Side,p.Bounds.Upper,
        p.Bounds.HorizontalRadius,p.SourceRecordSha256,p.NpcId);
    private static float Reach(SkyTempleNpcProfile p,Creature target,float? selected)=>System.MathF.Max(2,
        p.Bounds.HorizontalRadius+target.GetObjectTemplate().GetBoundRadius().GetMaxOfFrontAndSide()*.5f+(selected??p.Attacks[0].Range));
    internal static RetailSearchPoint Position(VisibleObject o)=>new(o.GetX(),o.GetY(),o.GetZ());
    internal static float Squared(RetailSearchPoint a,RetailSearchPoint b)
    {float x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return (y*y+x*x)+z*z;}
}

/// <summary>Time ownership for the existing managed200ms task. RetailTick32 is monotonic;
/// nullable samples keep tick0 valid and unsigned deltas span its rollover. Initial200ms,
/// the1000ms movement catch-up cap/retry, and failed-route elapsed>=profile bound are
/// explicit managed recovery policies, not a replay of native absolute due comparisons.</summary>
internal sealed class SkyTempleNpcNavigationTiming
{
    private uint? lastTick,searchStarted,failureSince,targetSample;
    internal bool HasFailure=>failureSince.HasValue;
    internal void Reset()=>lastTick=searchStarted=failureSince=targetSample=null;
    internal uint MovementMilliseconds(uint now)
    {
        uint elapsed=lastTick is { } previous?unchecked(now-previous):200;
        lastTick=now;return System.Math.Min(elapsed,1000u);
    }
    internal bool CanSearch(uint now)=>searchStarted is not { } started||unchecked(now-started)>=1000;
    internal void SearchStarted(uint now)=>searchStarted=now;
    internal void SearchSucceeded()=>searchStarted=null;
    internal uint? PredictionMilliseconds(uint now)
    {
        if(targetSample is not { } sampled)return null;
        uint elapsed=unchecked(now-sampled);return elapsed is >0 and <=1000?elapsed:null;
    }
    internal void TargetSampled(uint now)=>targetSample=now;
    internal void ProgressConfirmed()=>failureSince=null;
    internal bool FailureExpired(uint now,bool returning,int maximumMilliseconds)
    {
        failureSince??=now;
        return !returning&&maximumMilliseconds>0&&unchecked(now-failureSince.Value)>=(uint)maximumMilliseconds;
    }
}
