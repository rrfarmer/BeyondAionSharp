using static Aion.GameServer.GeoEngine.RetailPath.RetailMovementArithmetic;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>
/// Finite calculated NPCServer transaction owner. This is not a live adapter, scheduler,
/// observer provider, generic callback interpreter or production current-world certificate.
/// Constructed empty worlds remain explicitly synthetic. Unknown active children withhold completion.
/// </summary>
public static class RetailNpcMovementOwnerPlan
{
    public const ulong BaseNpcVtable=0x1404639a8;
    public const string BaseNpcVtableSha256="f41e9fa1dfd24352d428ecd7c7d77394adaf9cf8b9bd54f7af04647c2f4239a0";
    public static RetailNpcMovementCalculation Calculate(RetailNpcMovementSnapshot input,RetailGroundMovementScene scene,
        RetailNpcMoveToGoalRequest request,uint commonUpdateTick,ulong timerParameter,RetailMovementLimits limits) =>
        Evaluate(input,scene,limits,r=> {uint raw=RetailNpcMoveToGoalPlan.Goal(r,request);if(raw==0)RetailNpcMotionPlan.Common(r,commonUpdateTick,timerParameter);return raw;},0x140123190);

    /// <summary>Calculates the reached ID1 callback. Queue dequeue/once-only IO completion and final ref release are separate host obligations.</summary>
    public static RetailNpcMovementCalculation CalculateQueuedTimer(RetailNpcMovementSnapshot input,RetailGroundMovementScene scene,
        RetailNpcMovementTimerEntry timer,RetailMovementLimits limits) => Evaluate(input,scene,limits,r=>Queued(r,timer),hasNativeUIntResult:false);

    internal static RetailNpcMovementCalculation Evaluate(RetailNpcMovementSnapshot input,RetailGroundMovementScene scene,
        RetailMovementLimits limits,Func<RetailNpcMovementReduction,uint> action,ulong? nativeResultEntry=null,bool hasNativeUIntResult=true)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(scene);ArgumentNullException.ThrowIfNull(limits);
        RetailNpcMovementReduction? r=null;
        try
        {
            r=new(input,scene,limits);Validate(r);
            uint raw=action(r);
            return r.Result(hasNativeUIntResult&&raw!=0?RetailMovementCalculationStatus.NativeEarlyReturn:RetailMovementCalculationStatus.CalculatedApplicationChain,hasNativeUIntResult?raw:null)
                with {NativeResultEntry=nativeResultEntry};
        }
        catch(RetailMovementStop stop)
        {return r is not null?r.Result(stop.Failure.Status,null,stop.Failure):Rejected(input,stop.Failure);}
        catch(RetailCollisionStop stop)
        {
            var failure=new RetailMovementDomainFailure(stop.Status==RetailCollisionStatus.DefensiveLimit?RetailMovementCalculationStatus.DefensiveLimit:RetailMovementCalculationStatus.UnsupportedConsumedDomain,0,"computed-geometry",stop.Message);
            return r is not null?r.Result(failure.Status,null,failure):Rejected(input,failure);
        }
    }
    private static RetailNpcMovementCalculation Rejected(RetailNpcMovementSnapshot input,RetailMovementDomainFailure f)=>
        new(f.Status,null,RetailMovementExecutionStatus.Unexecuted,input,[],[],[],f,false);
    private static void Validate(RetailNpcMovementReduction r)
    {
        r.Scene.Validate();var s=r.Value;var a=r.Actor;var w=r.World;
        if(s.Ownership!=r.Scene.Ownership)Stop(RetailMovementCalculationStatus.StaleOwnership,0,"transaction-ownership","Scene and entire immutable owned snapshot must share all owner revisions.");
        Require(a.Vtable==BaseNpcVtable&&a.VtableSha256.Equals(BaseNpcVtableSha256,StringComparison.OrdinalIgnoreCase)&&Digest(a.EvidenceSha256),"actor-class","Individually bound actual base NPC class/vtable/evidence is required.");
        Require(a.ObjectId!=0&&Text(a.ScriptInterfaceIdentity)&&Text(a.AiTemplateIdentity),"actor-current-sources","Actual current actor/script-interface/AI-template identities are required.");
        if(a.WorldPointerIdentity is not null&&a.WorldPointerIdentity!=s.Ownership.WorldIdentity)
            Stop(RetailMovementCalculationStatus.StaleOwnership,0,"world-pointer","Actual actor world pointer identity differs from current owned world.");
        Point(a.Position);Point(a.Velocity);Point(a.SamplingPosition9d8);
        Require(w.WidthMetres==r.Scene.Sources.Path.WorldWidthMetres&&w.HeightMetres==r.Scene.Sources.Path.WorldHeightMetres&&w.Columns32>0&&w.Columns64>0&&w.CellCount32>=0&&w.LockCount64>0,"world-dimensions","Explicit actual world dimensions/index strides are required; no map default.");
        Require(w.Cells.Select(c=>c.Index).Distinct().Count()==w.Cells.Count&&w.Wrappers.Select(x=>x.Identity).Distinct().Count()==w.Wrappers.Count,"world-identities","Duplicate current cell/wrapper identities are not a coherent snapshot.");
        Require(!w.Wrappers.Where(x=>x.DelayFreed3f==0).GroupBy(x=>x.ActorId).Any(g=>g.Count()>1),"active-wrapper-identities","More than one live wrapper for an actor cannot be treated as one current object pointer.");
        foreach(var c in w.Cells)Require(c.OrderedObjects.Distinct().Count()==c.OrderedObjects.Count&&c.OrderedUsers.Distinct().Count()==c.OrderedUsers.Count,"member-identities","A native pointer list cannot contain repeated owned identity.");
        var storage=r.Storage;
        Require(storage.SerializedModelOwnership&&storage.AllocatorNormalReturnDeclaration&&Text(storage.ReservationIdentity)&&storage.AvailablePathPointSlots>=0&&storage.AvailableWrapperSlots>=0&&storage.UsedPathPointSlots>=0&&storage.UsedWrapperSlots>=0,"model-storage","Explicit serialized reserved model storage and normal allocator-return declaration required; this never executes/proves the native allocator.");
        if(r.Path is not null)Require(storage.UsedPathPointSlots>=r.Path.Points.Count,"owned-path-reservation","Current owned path allocation must have an accountable model slot count.");
        if(r.Globals.DebugGuards141781298!=0&&r.Globals.DebugGuard171cfdcd0!=0)
            r.Incomplete(0x14000e690,"active-debug-frame","Native debug registration/clock/guard stack children are outside the finite application model.");
        if(r.Globals.LockDiagnostics1417818d8!=0)r.Incomplete(0x1400edc00,"active-lock-diagnostics","Native lock diagnostic recorder is active. TLS/OS lock effects remain unexecuted even when recorder is skipped.");
        if(r.Actor.PathLock290!=0)Stop(RetailMovementCalculationStatus.StaleOwnership,0x140100230,"path-serialization","Current path lock is busy; declared exclusive reducer cannot guess a wait result.");
        if(r.Path is not null)r.Path=RetailNpcMoveToGoalPlan.CopyInput(r,r.Path);
        if(r.Timers.Capacity<r.Timers.Entries.Count||r.Timers.Capacity<0)Require(false,"timer-capacity","Invalid owned timer storage.");
        Require(Text(r.Timers.QueueIdentity),"timer-queue-identity","Actual selected-stripe queue identity is required.");
        r.Child(0x1400edc00,"application ownership admission","pinned assets/base virtuals, serialized model, inactive debug recorder","OS locks, RCU, TLS depth changes, allocations, IO and actual callbacks are not executed by this calculator");
    }
    private static uint Queued(RetailNpcMovementReduction r,RetailNpcMovementTimerEntry timer)
    {
        const ulong e=0x140126470;var a=r.Actor;
        if(timer.ActorId!=a.ObjectId||timer.ActorLifetime!=r.Value.Ownership.ActorLifetime||timer.Generation64!=a.TimerGeneration64||
            r.Timers.Entries.Count(t=>t==timer)!=1)
            Stop(RetailMovementCalculationStatus.StaleOwnership,e,"queued-timer-generation","Queued logical entry must match the current lifetime/generation and one owned registration. Dispatch once-only is still a host obligation.");
        if(r.Globals.ClosingAi1438549c4!=0)return 0;
        if(a.DeletingB8>0||a.WorldPointerIdentity is null)return 0;
        if(r.World.Deleting149b8!=0)r.Incomplete(e,"Remove-world-deleted","Actual native Remove child reached; no respawn/heal state is fabricated.");
        uint infoTick=r.Tick("UpdateInfo",0x140101cd2);
        if(a.Timeout8d4!=0)r.Incomplete(0x140101cc0,"active-update-info-timeout","Reached timeout/speed/action owner needs complete independent child, not caller poststate.");
        r.Child(0x140101cc0,"UpdateInfo","current8d4=0","sampled tick retained; application timeout mutation skipped; locks/TLS infrastructure not claimed NOP");
        if(timer.TimerId!=1)r.Incomplete(e,"non-ID1-dispatch","Heartbeat/removal/other timer program is outside this admitted complete ID1 calculation.");
        if(a.State598==2)return 0;
        if(a.SignedHp14c<=0)r.Incomplete(0x140114fa0,"CheckDie-owner","Nonpositive HP reaches death/lifecycle ownership; no resurrection or return assumed.");
        r.Child(0x140114fa0,"CheckDie","raw598!=2 and signedHP>0","native false; no death/remove owner");
        uint tick=r.Tick("AiTick",0x14012685c);
        if(a.Motion890>0&&r.Motion.Deadline4!=0&&r.Motion.Deadline4<=tick)RetailNpcMotionPlan.StopPacket(r);
        if(a.AiDefer724==0)r.Incomplete(e,"active-AI-update","No future-defer guard; ff2d0/sleep/UpdateState/script owner must be independently calculated.");
        if(a.AiDefer724<=tick)
        {
            r.Write(e,"actor+724",a.AiDefer724,0);r.Actor=r.Actor with {AiDefer724=0};
            r.Incomplete(e,"expired-AI-defer","Native clears724 before reaching ff2d0/sleep/UpdateState application child.");
        }
        r.Child(e,"AI future-defer","current724!=0 and724>sampledTick","skips ff2d0/sleep/UpdateState; no arbitrary poststate accepted");
        int old=r.Actor.AiExchange740;r.Write(e,"actor+740.exchange",old,1);r.Actor=r.Actor with {AiExchange740=1};
        if(old!=0)r.Incomplete(e,"busy-ID1-retry","Native ID1 schedules another100ms timer instead of PostAI; active retry queue ownership lies outside the first-entry domain.");
        RetailNpcMotionPlan.Common(r,tick,0);
        r.Write(e,"actor+740.exchange",r.Actor.AiExchange740,0);r.Actor=r.Actor with {AiExchange740=0};
        r.Child(0x140101660,"PostAIUpdate","computed CommonUpdate; timerID1!=0","heartbeat/HP regeneration/random reschedule and auxiliary heartbeat work skipped");
        r.Effect(new(0,RetailNpcMovementEffectKind.TimerDispatchCompletion,e,"Host must dequeue exactly once and complete IO/ref ownership; calculator retains registration and does not execute/replay final release.",Timer:timer));
        _=infoTick;return 0;
    }
}
