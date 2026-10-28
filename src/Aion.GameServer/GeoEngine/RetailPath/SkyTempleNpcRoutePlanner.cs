using static Aion.GameServer.GeoEngine.RetailPath.RetailMovementArithmetic;

namespace Aion.GameServer.GeoEngine.RetailPath;

internal readonly record struct SkyTempleNpcBody(float Front,float Side,float Upper,float Radius,string SourceHash,int NpcId);
internal sealed record SkyTempleNpcRoute(IReadOnlyList<RetailSearchPoint> Points,RetailSearchPoint Goal,
    string SceneRevision,string Identity,int Index=0,int Progress=0,string? Resolution=null,RetailSearchPoint? RequestedGoal=null);
internal sealed record SkyTempleNpcRouteResult(SkyTempleNpcRoute? Route,string? Failure,RetailGroundSearchResult? Search=null)
{internal bool Success=>Route is not null&&Failure is null;}

/// <summary>Managed gameplay policy around the unchanged retail directed A*/clearance/follower.
/// This does not invent native AI/crowd/TLS state. Instance/home eligibility is evaluated by the
/// live owner; players are soft contacts in the existing4.8 host. Static/field geometry is computed.</summary>
internal sealed class SkyTempleNpcRoutePlanner(SkyTempleNpcNavigationAssets assets,RetailCollisionScene collision,
    SkyTempleNpcBody body,string actorIdentity)
{
    private readonly RetailNpcMovementActor actor=BodyActor(body);
    private RetailGroundMovementScene Scene()=>new(assets.Sources(actorIdentity,collision.Declaration.SceneRevision),collision,
        new(actorIdentity,actorIdentity,"live-request","live-path",assets.MapId.ToString(System.Globalization.CultureInfo.InvariantCulture),collision.Declaration.InstanceIdentity,
            collision.Declaration.SceneRevision,assets.Revision,"managed-world","managed-AI","existing200ms",
            body.SourceHash,RetailMovementInputOrigin.CurrentOwnerDeclaration),assets.SurfaceOptions,1,true,SkyTempleNpcNavigationAssets.Floating);
    internal RetailPathSurfaceRef? Surface(RetailSearchPoint p)
    {
        var result=assets.Surfaces.EnumerateSurfaceCandidates(p.X,p.Y,p.Z);
        if(result.Status!=RetailSurfaceQueryStatus.Complete)return null;
        return result.Candidates.FirstOrDefault(c=>c.Status==RetailSurfaceCandidateStatus.WithinTolerance)?.Surface;
    }
    internal RetailSearchPoint? Ground(RetailSearchPoint p)
    {
        var q=RetailCollisionQuery.ProjectGround(collision,collision.Stamp,p,SkyTempleNpcNavigationAssets.QueryLimits);
        return q.Status==RetailCollisionStatus.Computed&&q.NativeResult==1?q.Point:null;
    }
    internal bool Visible(RetailSearchPoint start,float fromUpper,RetailSearchPoint target,float targetUpper)
    {
        // Native23b2f0: full raised probe followed, only if blocked/outside, by half-height probe.
        float a=System.MathF.Max(2,fromUpper),b=System.MathF.Max(2,targetUpper);
        return Clear(start with {Z=start.Z+a},target with {Z=target.Z+b},1)||
            Clear(start with {Z=start.Z+a*.5f},target with {Z=target.Z+b*.5f},1);
    }
    private bool Clear(RetailSearchPoint a,RetailSearchPoint b,int view)
    {
        if(!collision.Bounds.Contains(a)||!collision.Bounds.Contains(b))return false;
        var q=RetailCollisionQuery.SegmentBlocked(collision,collision.Stamp,a,b,view,0,
            BitConverter.UInt32BitsToSingle(0x3d016f00),SkyTempleNpcNavigationAssets.QueryLimits);
        return q.Status==RetailCollisionStatus.Computed&&q.NativeResult==0;
    }
    internal SkyTempleNpcRouteResult Find(RetailSearchPoint from,RetailSearchPoint goal,string revision)
    {
        if(!from.IsFinite||!goal.IsFinite||!collision.Bounds.Contains(from)||!collision.Bounds.Contains(goal))return new(null,"outside-world");
        var sourceLookup=assets.Surfaces.EnumerateSurfaceCandidates(from.X,from.Y,from.Z);
        var goalLookup=assets.Surfaces.EnumerateSurfaceCandidates(goal.X,goal.Y,goal.Z);
        // A completed lookup with no matching layer is native "missing", not an
        // unsupported query.223230 deliberately runs2257e0's body/ground walk when
        // the start is missing, returning only its full or clipped checked segment.
        // Ambiguity, unavailable terrain and defensive limits must not enter it.
        if(sourceLookup.Status!=RetailSurfaceQueryStatus.Complete||goalLookup.Status!=RetailSurfaceQueryStatus.Complete)
            return new(null,$"unresolved-ground-layer: {sourceLookup.Status}/{goalLookup.Status}");
        var start=sourceLookup.Candidates.FirstOrDefault(c=>c.Status==RetailSurfaceCandidateStatus.WithinTolerance)?.Surface;
        var end=goalLookup.Candidates.FirstOrDefault(c=>c.Status==RetailSurfaceCandidateStatus.WithinTolerance)?.Surface;
        var scene=Scene();var policy=new Policy(scene,actor,revision);
        var observation=new RetailGroundSearchObservation(from,goal,assets.Bounds,new(true,false,false,false,body.Upper),
            0,RetailGroundQueueKind.LinkedList,null,true,new(start is null?RetailSearchSurfaceState.Missing:RetailSearchSurfaceState.Resolved,start),
            new(end is null?RetailSearchSurfaceState.Missing:RetailSearchSurfaceState.Resolved,end),revision,scene.Sources,assets.SurfaceOptions,false);
        var result=RetailGroundPathSearch.Search(observation,policy,new(){MaxNodes=20_480,MaxPops=12_000,MaxPolicyCalls=100_000,MaxOutputPoints=20_000});
        if(policy.Failure is not null)return new(null,policy.Failure,result);
        if(!result.Success||result.Points.Count==0)return new(null,$"{result.Status}/{result.Termination}: {result.Detail}",result);
        string? resolution=result.ConsumedPolicy.Any(c=>c.Purpose==RetailSearchPolicyPurpose.MissingStartFallback)
            ?"native-missing-start-checked-segment":null;
        //223230 stores the clipped endpoint in path metadata too. Keep the outer
        // gameplay request separately so consuming a prefix cannot complete return.
        return new(new(Array.AsReadOnly(result.Points.ToArray()),result.Points[^1],collision.Declaration.SceneRevision,revision,
            Resolution:resolution,RequestedGoal:goal),null,result);
    }
    internal RetailNpcPathFollowResult Follow(SkyTempleNpcRoute route,RetailSearchPoint current,float distance)
    {
        var epoch=new RetailNpcPathFollowEpoch(actorIdentity,route.Identity,actorIdentity,route.Identity,assets.MapId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            collision.Declaration.InstanceIdentity,collision.Declaration.SceneRevision,assets.Revision);
        string digest=RetailNpcPathFollowObservation.ComputeSnapshotSha256(route.Points,route.Points.Count,route.Index,route.Goal,20_000);
        var input=RetailNpcPathFollowObservation.Create(route.Points,route.Points.Count,route.Index,route.Goal,current,route.RequestedGoal??route.Goal,distance,0,0,
            new(0x14007dfa0,1,0,body.Upper,route.Progress,true,true,body.SourceHash),epoch,
            new(route.Identity,digest,body.SourceHash,"Actual managed route producer, source-bound ordinary-ground body"),false,20_000);
        return RetailNpcMoveByPathPlan.Plan(input,collision,new(20_000,4096,4096,1_000_000,SkyTempleNpcNavigationAssets.QueryLimits));
    }
    internal bool StepClear(RetailSearchPoint from,RetailSearchPoint to)
    {
        var r=RetailGroundClearance.Walk(Scene(),actor,from,to,1,null,SkyTempleNpcNavigationAssets.MovementLimits);
        return r.HasNativeOutcome&&r.NativeResult==0&&r.Endpoint==to;
    }

    private sealed class Policy(RetailGroundMovementScene scene,RetailNpcMovementActor actor,string revision):IRetailSearchPolicyReplay
    {
        private readonly RetailGroundSearchPolicy costs=new(scene,actor,true,0,SkyTempleNpcNavigationAssets.MovementLimits,revision);
        internal string? Failure {get;private set;}
        private RetailSearchReplayStamp Stamp=>new(Failure is null,revision,"Computed ground geometry and managed current world/instance eligibility; not a native AI-state observation");
        public RetailSearchCostObservation TransitionCost(RetailPathSurfaceRef from,RetailPathSurfaceRef to)
        {
            // Native geometry/body-ring/distance precedence is retained. The managed world treats
            // other actors as soft contacts; it does not manufacture zero native crowd fields to
            // admit the unrelated native proximity child.
            float value=0;
            try
            {
                if(!ReferenceEquals(from.Owner,scene.Sources.Path)||!ReferenceEquals(to.Owner,scene.Sources.Path))
                    throw new InvalidDataException("Cost surface belongs to a different path asset.");
                var a=scene.Position(from);var b=scene.Position(to);
                if(costs.Fields(a,b))value=2000;
                else
                {
                    float extent=F(actor.FrontC0);if(extent<=F(actor.SideC4))extent=actor.SideC4;
                    int count=Int(Mul(Add(extent,.25f),2),"body-ring-count");
                    if(count>SkyTempleNpcNavigationAssets.MovementLimits.MaxSteps)
                        throw new InvalidDataException("Body-ring walk exceeds the bounded live policy.");
                    bool missing=false;
                    foreach(int direction in new[]{0,2,1,3})
                    {
                        var current=from;
                        for(int step=0;step<count;step++)
                        {
                            var next=scene.Surfaces.GetCardinalNeighbor(current,direction);
                            if(next.Status is RetailSurfaceQueryStatus.NoLink or RetailSurfaceQueryStatus.OutsideWorld)
                            {missing=true;break;}
                            if(next.Status!=RetailSurfaceQueryStatus.Found||next.Target is null)
                                throw new InvalidDataException(next.Detail??next.Status.ToString());
                            current=next.Target;
                        }
                        if(missing)break;
                    }
                    value=missing?200:Mul(Length(a,b),100);
                }
            }
            catch(RetailMovementStop ex){Failure=ex.Failure.Detail;}
            catch(RetailCollisionStop ex){Failure=ex.Message;}
            catch(InvalidDataException ex){Failure=ex.Message;}
            return new(Stamp,value);
        }
        public RetailSearchBooleanObservation SameNodeShortcutCapability(RetailPathNode node)=>new(Stamp,node.Type is 0 or 2);
        public RetailSearchSegmentObservation CheckSegment(RetailSearchPoint from,RetailSearchPoint to,RetailSearchPolicyPurpose purpose)
        {
            //2257e0 passes mode0 to2f3ab0 for search/shortcut/missing-start
            // segments. Follow owns its own complete geometry; no extra full-body
            // mode1 probe is added before committing an ordinary follower point.
            var r=RetailGroundClearance.Walk(scene,actor,from,to,0,null,SkyTempleNpcNavigationAssets.MovementLimits);
            if(!r.HasNativeOutcome)Failure=r.Failure?.Detail??"unresolved clearance";
            return new(Stamp,!r.HasNativeOutcome||r.NativeResult!=0,r.Endpoint??from);
        }
        public RetailSearchBooleanObservation FieldObjectCollision(RetailSearchPoint from,RetailSearchPoint to,RetailSearchPolicyPurpose purpose)
        {var r=costs.FieldObjectCollision(from,to,purpose);if(costs.Failure is not null)Failure=costs.Failure.Detail;return new(Stamp,r.Value);}
    }

    /// <summary>Compatibility carrier for the unchanged body-math API. Only the proven class/bounds,
    /// ordinary-ground family and template discriminator are consumed. Native party/AI/crowd/timer
    /// fields are not observations; their algorithms are not invoked by this managed backend.</summary>
    private static RetailNpcMovementActor BodyActor(SkyTempleNpcBody b)=>new()
    {
        ObjectId=1,Vtable=RetailNpcMovementOwnerPlan.BaseNpcVtable,VtableSha256=RetailNpcMovementOwnerPlan.BaseNpcVtableSha256,
        EvidenceSha256=b.SourceHash,WorldPointerIdentity="managed-world",Position=default,Velocity=default,HeadingB0=0,PacketState178=0,
        TemplatePointer1e8=null,TemplateTurnTimeE0=0,Kind88=2,Kind32c=0,State598=0,State180=0,Pose5a0=1,Effects188=0,Cutscene1f8=0,
        SignedHp14c=1,RadiusBc=b.Radius,FrontC0=b.Front,SideC4=b.Side,UpperC8=b.Upper,Speed5e0=0,Speed5e4=0,Speed5e8=0,
        SpeedAdjustment5a8=0,SelectedSpeed5a4=0,DistanceBudget59c=0,SpeedStatePointer638=null,SpeedStateObjectPlus8=0,
        AiPointer200=null,AiState=0,Crowd310=0,Crowd314=0,TemplateIdA8=checked((uint)b.NpcId),Master55c=0,ScriptInterfaceIdentity="unused",
        AiTemplateIdentity="unused",ScriptSee8d8=0,ScriptMove8dc=0,Template119=0,Template11a=0,Raw750=0,Template324=0,
        DeletingB8=0,Timeout8d4=0,TimeoutDue8d0=0,TimeoutState8cc=0,DebugMode8b0=0,Motion890=0,AiDefer724=0,AiExchange740=0,
        Progress82c=0,PathLock290=0,LastFailedGoalA1c=default,SamplingCount9d4=0,SamplingPosition9d8=default,
        References8=0,DelayedReferencesC=0,PendingIo10=0,ReferenceHistory14=0,DebugSource28=0,TimerGeneration64=0,
        WorldHash62=0,InWorldAc=1,WrapperIdentity80=null
    };
}
