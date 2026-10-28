using System.Runtime.CompilerServices;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Lifecycle;
using Aion.GameServer.GeoEngine.RetailPath;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Controllers.Movement;

/// <summary>Selected NPC return ownership, on the existing movement task. Native
/// storage/transport is managed; no supplied home-area predicate or replay callback
/// answers. The area328/maker predicate and active maintained-cast stop remain
/// frontiers; registered-effect selection/order uses the closed Main37 mapping.</summary>
internal static class SkyTempleNpcReturnState
{
    internal enum Cause { ChaseTimeout, PathFailure, EncounterEnded }
    internal enum Control { Move, Complete, RemoveReason16, Unsupported }
    private static readonly ILogger Log=AionLog.For(nameof(SkyTempleNpcReturnState));
    private sealed class Life(Npc owner,SkyTempleNpcProfile profile)
    {
        internal readonly long Epoch=owner.GetController().RetailLifeEpoch;
        internal readonly string Generation=profile.Generation;
        internal readonly object World=owner.GetPosition().GetWorldMapInstance();
        internal readonly int Map=owner.GetWorldId(),Instance=owner.GetInstanceId();
        internal readonly RetailSearchPoint Birth=SkyTempleNpcNavigation.Position(owner);
        internal readonly byte Heading=owner.GetHeading();
        internal Return? Pending;
        internal long NextRegistration;
        internal string? CleanupIssue;
    }
    private sealed class Return(Cause cause,RetailSearchPoint goal,WalkerTemplate? template,List<RouteStep>? list,RouteStep? step,VisibleObject? triggerTarget)
    {
        internal readonly Cause Cause=cause;
        internal readonly RetailSearchPoint Goal=goal;
        internal readonly WalkerTemplate? Template=template;
        internal readonly List<RouteStep>? List=list;
        internal readonly RouteStep? Step=step;
        internal readonly int Index=step?.GetStepIndex()??-1;
        internal readonly VisibleObject? TriggerTarget=triggerTarget;
        internal VisibleObject? ReturnTarget;
        internal uint StartTick;
        internal bool Active;
        internal bool RemovalRequested;
    }
    private static readonly ConditionalWeakTable<Npc,Life> Lives=new();
    private sealed record Registration(Life Life,long Sequence,SkillTargetSlot Slot);
    private static readonly ConditionalWeakTable<Effect,Registration> Registrations=new();

    // Called AFTER actual Put assignment under its writer lock. This path takes
    // no controller, HP or SkillState monitor while that lock is held.
    internal static void RegisteredEffect(Creature receiver,Effect effect)
    {
        if(receiver is not Npc owner||!SkyTempleNpcProfileCatalog.TryGet(owner,out var profile)||
            !Lives.TryGetValue(owner,out var life)||life.Epoch!=owner.GetController().RetailLifeEpoch||
            life.Generation!=profile.Generation||!ReferenceEquals(effect.GetEffected(),owner))return;
        var stamp=new Registration(life,System.Threading.Interlocked.Increment(ref life.NextRegistration),effect.GetTargetSlot());
        lock(Registrations){Registrations.Remove(effect);Registrations.Add(effect,stamp);}
    }
    internal static bool TryCleanupOrder(Npc owner,out Effect[] effects)
    {
        effects=[];
        if(!Lives.TryGetValue(owner,out var life)||!CurrentLife(owner,life))return false;
        bool Reject(string issue)
        {
            if(System.Threading.Interlocked.Exchange(ref life.CleanupIssue,issue)!=issue)
                Log.LogWarning("SkyTemple return cleanup unsupported npc={Npc} detail={Detail}",owner.GetObjectId(),issue);
            return false;
        }
        if(System.Threading.Monitor.IsEntered(owner)||System.Threading.Monitor.IsEntered(owner.GetMoveController()))return Reject("actor-or-controller-held-before-effect-snapshot");
        var selected=new List<(Effect Effect,int Bank,long Sequence)>();
        foreach(var effect in owner.GetEffectController().GetAllEffects())
        {
            var activation=effect.GetSkillTemplate().GetActivationAttribute();
            if(activation==ActivationAttribute.NONE||!Enum.IsDefined(activation))return Reject("unproved-current-activation-metadata");
            if(!RemovesEffect(activation,effect.GetTargetSlot()))continue;
            if(!ReferenceEquals(effect.GetEffected(),owner)||!Registrations.TryGetValue(effect,out var stamp)||
                !ReferenceEquals(stamp.Life,life)||stamp.Slot!=effect.GetTargetSlot())return Reject("missing-or-stale-current-registration");
            int bank=stamp.Slot switch
            {SkillTargetSlot.BUFF=>0,SkillTargetSlot.DEBUFF=>1,SkillTargetSlot.CHANT=>2,SkillTargetSlot.SPEC=>3,
                SkillTargetSlot.SPEC2=>4,SkillTargetSlot.NOSHOW=>6,SkillTargetSlot.NONE=>7,_=>-1};
            if(bank<0)return Reject("unknown-current-target-bank");
            selected.Add((effect,bank,stamp.Sequence));
        }
        effects=selected.OrderBy(x=>x.Bank).ThenByDescending(x=>x.Sequence).Select(x=>x.Effect).ToArray();
        System.Threading.Interlocked.Exchange(ref life.CleanupIssue,null);return true;
    }

    // Called only at the actual World.Spawn->OnBeforeSpawn accepted-position seam,
    // before the object is published. Never sample combat/patrol pose as birth.
    internal static void AcceptBirth(Npc owner)
    {
        if(!SkyTempleNpcProfileCatalog.TryGet(owner,out var p)||owner.IsSpawned())return;
        lock(owner.GetMoveController())
        {
            if(!SkyTempleNpcNavigation.Position(owner).IsFinite)throw new InvalidDataException("Nonfinite accepted NPC birth");
            Lives.Remove(owner);Lives.Add(owner,new(owner,p));
        }
    }
    private static bool CurrentLife(Npc owner,Life life)=>owner.IsSpawned()&&!owner.IsDead()&&
        owner.GetController().RetailLifeEpoch==life.Epoch&&SkyTempleNpcProfileCatalog.TryGet(owner,out var p)&&p.Generation==life.Generation&&
        owner.GetWorldId()==life.Map&&owner.GetInstanceId()==life.Instance&&ReferenceEquals(owner.GetPosition().GetWorldMapInstance(),life.World);
    private static bool CurrentGoal(Npc owner,Life life,Return value)
    {
        if(!CurrentLife(owner,life)||!ReferenceEquals(life.Pending,value))return false;
        if(value.Active?!ReferenceEquals(owner.GetTarget(),value.ReturnTarget):
            owner.GetTarget() is { } target&&!ReferenceEquals(target,value.TriggerTarget))return false;
        if(value.Template is null)return true;
        var move=owner.GetMoveController();
        return ReferenceEquals(move.GetWalkerTemplate(),value.Template)&&ReferenceEquals(value.Template.GetRouteSteps(),value.List)&&
            value.List is { } list&&value.Index>=0&&value.Index<list.Count&&ReferenceEquals(list[value.Index],value.Step)&&
            ReferenceEquals(move.GetCurrentStep(),value.Step)&&value.Step!.GetStepIndex()==value.Index&&
            new RetailSearchPoint(value.Step.GetX(),value.Step.GetY(),value.Step.GetZ())==value.Goal;
    }
    internal static bool PrepareChaseTimeout(Npc owner)=>Prepare(owner,Cause.ChaseTimeout,false);
    internal static bool PreparePathFailure(Npc owner)=>Prepare(owner,Cause.PathFailure,false);
    internal static bool PrepareEncounterEnd(Npc owner)=>Prepare(owner,Cause.EncounterEnded,true);
    private static bool Prepare(Npc owner,Cause cause,bool retain)
    {
        lock(owner.GetMoveController())
        {
            if(!Lives.TryGetValue(owner,out var life)||!CurrentLife(owner,life))return false;
            if(retain&&life.Pending is { } previous&&CurrentGoal(owner,life,previous))return true;
            RetailSearchPoint goal=life.Birth;WalkerTemplate? template=null;List<RouteStep>? list=null;RouteStep? step=null;
            if(cause==Cause.ChaseTimeout)
            {
                var move=owner.GetMoveController();var candidate=move.GetWalkerTemplate();var selected=move.GetCurrentStep();
                if(candidate is not null&&selected is not null&&
                    SkyTempleNpcWaypointProjection.TryPrepare(owner,candidate,out var prepared,out _)&&ReferenceEquals(candidate,prepared))
                {
                    var points=candidate.GetRouteSteps();int index=selected.GetStepIndex();
                    if(index>=0&&index<points.Count&&ReferenceEquals(points[index],selected))
                    {template=candidate;list=points;step=selected;goal=new(selected.GetX(),selected.GetY(),selected.GetZ());}
                }
            }
            if(!goal.IsFinite)return false;
            life.Pending=new(cause,goal,template,list,step,owner.GetTarget());
            Log.LogInformation("SkyTemple return prepared npc={Npc} template={Template} cause={Cause} goal={Goal} patrol={Patrol} step={Step} life={Life}",
                owner.GetObjectId(),owner.GetNpcId(),cause,goal,template?.GetRouteId(),step?.GetStepIndex(),life.Epoch);
            return true;
        }
    }
    internal static void EncounterStarted(Npc owner)
    {lock(owner.GetMoveController())if(Lives.TryGetValue(owner,out var life))life.Pending=null;}
    internal static bool TryRead(Npc owner,out Frame frame)=>Frame.Capture(owner,out frame);

    // The private constructor and source-owned capture keep goal/start/lifetime
    // command ownership out of caller-constructed booleans and arbitrary points.
    internal sealed class Frame
    {
        private readonly Npc owner;
        private readonly Life life;
        private readonly Return value;
        internal RetailSearchPoint Goal=>value.Goal;
        internal RetailSearchPoint Birth=>life.Birth;
        internal byte Heading=>life.Heading;
        internal uint StartTick=>value.StartTick;
        internal Cause ReturnCause=>value.Cause;
        internal uint CapturedTick { get; private set; }
        internal bool UsesPatrol=>value.Template is not null;
        private Frame(Npc owner,Life life,Return value){this.owner=owner;this.life=life;this.value=value;}
        internal static bool Capture(Npc owner,out Frame frame)
        {
            frame=null!;lock(owner.GetMoveController())
            {
                if(!Lives.TryGetValue(owner,out var life)||life.Pending is not { } value||!CurrentGoal(owner,life,value))return false;
                frame=new(owner,life,value);return true;
            }
        }
        internal bool IsCurrent(Npc current)=>ReferenceEquals(current,owner)&&CurrentGoal(owner,life,value);
        internal bool LifeIsCurrent(Npc current)=>ReferenceEquals(current,owner)&&CurrentLife(owner,life);
        internal bool Enter(Npc current)
        {lock(owner.GetMoveController()){if(!IsCurrent(current))return false;value.ReturnTarget=owner.GetTarget();value.Active=true;return true;}}
        internal Control Tick(Npc current,uint now)
        {
            lock(owner.GetMoveController())
            {
                if(!IsCurrent(current)||!value.Active||owner.GetAi().GetState()!=AIState.RETURNING)return Control.Unsupported;
                CapturedTick=now;
                // Source order: actual goal equality precedes even an expired state.
                if(SamePoint(SkyTempleNpcNavigation.Position(owner),Goal)){value.RemovalRequested=false;return Control.Complete;}
                if(Expired(value.StartTick,now)){value.RemovalRequested=true;return Control.RemoveReason16;}
                if(value.StartTick==0)value.StartTick=now; //0 remains the native sentinel.
                return Control.Move;
            }
        }
        internal bool Complete(Npc current)
        {lock(owner.GetMoveController()){if(!IsCurrent(current)||!value.Active)return false;life.Pending=null;return true;}}
        internal bool Remove(Npc current)
        {
            // World.RemoveObject uses object->lifecycle ordering. Do not acquire the
            // object monitor from a movement/nav critical section in the opposite order.
            lock(owner)
            {
                lock(owner.GetMoveController())
                {
                    if(!IsCurrent(current)||!value.Active||!value.RemovalRequested||owner.GetAi().GetState()!=AIState.RETURNING)return false;
                    if(SamePoint(SkyTempleNpcNavigation.Position(owner),Goal))return false;
                    life.Pending=null;
                }
                Log.LogInformation("SkyTemple return removal reason=16 npc={Npc} template={Template} goal={Goal} startTick={Start}",owner.GetObjectId(),owner.GetNpcId(),Goal,value.StartTick);
                // No controller/nav/skill-state monitor crosses EndEffect callbacks.
                return owner.GetController().Delete();
            }
        }
    }
    internal static bool Expired(uint start,uint now)=>start!=0&&unchecked(now-start)>60000u;
    internal static bool RemovesEffect(Aion.GameServer.SkillEngine.Model.ActivationAttribute activation,Aion.GameServer.SkillEngine.Model.SkillTargetSlot slot)
        =>slot!=Aion.GameServer.SkillEngine.Model.SkillTargetSlot.BOOST&&activation is
            Aion.GameServer.SkillEngine.Model.ActivationAttribute.ACTIVE or Aion.GameServer.SkillEngine.Model.ActivationAttribute.PROVOKED or
            Aion.GameServer.SkillEngine.Model.ActivationAttribute.MAINTAIN or Aion.GameServer.SkillEngine.Model.ActivationAttribute.TOGGLE or
            Aion.GameServer.SkillEngine.Model.ActivationAttribute.CHARGE;
    internal static bool SamePoint(RetailSearchPoint a,RetailSearchPoint b)=>RetailNpcReturnStatePlan.SamePoint(new(a.X,a.Y,a.Z),new(b.X,b.Y,b.Z));
}
