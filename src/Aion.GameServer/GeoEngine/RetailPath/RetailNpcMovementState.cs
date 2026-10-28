namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>All raw consumed actor values are explicit initial data. No script poststate or callback tape.</summary>
public sealed record RetailNpcMovementActor
{
    public required uint ObjectId { get; init; }
    public required ulong Vtable { get; init; }
    public required string VtableSha256 { get; init; }
    public required string EvidenceSha256 { get; init; }
    public required string? WorldPointerIdentity { get; init; }
    public required RetailSearchPoint Position { get; init; }
    public required RetailSearchPoint Velocity { get; init; }
    public required uint HeadingB0 { get; init; }
    public required uint PacketState178 { get; init; }
    public required string? TemplatePointer1e8 { get; init; }
    public required float TemplateTurnTimeE0 { get; init; }
    public required int Kind88 { get; init; }
    public required int Kind32c { get; init; }
    public required int State598 { get; init; }
    public required int State180 { get; init; }
    public required uint Pose5a0 { get; init; }
    public required ulong Effects188 { get; init; }
    public required ulong Cutscene1f8 { get; init; }
    public required int SignedHp14c { get; init; }
    public required float RadiusBc { get; init; }
    public required float FrontC0 { get; init; }
    public required float SideC4 { get; init; }
    public required float UpperC8 { get; init; }
    public required float Speed5e0 { get; init; }
    public required float Speed5e4 { get; init; }
    public required float Speed5e8 { get; init; }
    public required float SpeedAdjustment5a8 { get; init; }
    public required float SelectedSpeed5a4 { get; init; }
    public required float DistanceBudget59c { get; init; }
    public required string? SpeedStatePointer638 { get; init; }
    public required int SpeedStateObjectPlus8 { get; init; }
    public required string? AiPointer200 { get; init; }
    public required int AiState { get; init; }
    public required int Crowd310 { get; init; }
    public required int Crowd314 { get; init; }
    public required uint TemplateIdA8 { get; init; }
    public required int Master55c { get; init; }
    public required string ScriptInterfaceIdentity { get; init; }
    public required string AiTemplateIdentity { get; init; }
    /// <summary>Script-interface+8d8 reached through actor+200; not actor+8d8.</summary>
    public required int ScriptSee8d8 { get; init; }
    /// <summary>Script-interface+8dc reached through actor+200; not actor+8dc.</summary>
    public required int ScriptMove8dc { get; init; }
    public required byte Template119 { get; init; }
    public required byte Template11a { get; init; }
    public required int Raw750 { get; init; }
    public required int Template324 { get; init; }
    public required int DeletingB8 { get; init; }
    public required int Timeout8d4 { get; init; }
    public required uint TimeoutDue8d0 { get; init; }
    public required int TimeoutState8cc { get; init; }
    public required int DebugMode8b0 { get; init; }
    public required int Motion890 { get; init; }
    public required uint AiDefer724 { get; init; }
    public required int AiExchange740 { get; init; }
    public required int Progress82c { get; init; }
    public required int PathLock290 { get; init; }
    public required RetailSearchPoint LastFailedGoalA1c { get; init; }
    public required uint SamplingCount9d4 { get; init; }
    public required RetailSearchPoint SamplingPosition9d8 { get; init; }
    public required int References8 { get; init; }
    public required int DelayedReferencesC { get; init; }
    public required int PendingIo10 { get; init; }
    public required int ReferenceHistory14 { get; init; }
    public required ulong DebugSource28 { get; init; }
    public required uint TimerGeneration64 { get; init; }
    public required ushort WorldHash62 { get; init; }
    public required int InWorldAc { get; init; }
    public required string? WrapperIdentity80 { get; init; }
}
/// <summary>Extra30c is unconsumed adjacent word, not consumed for path decisions in this unit. Null means its copied native bytes are unproved.</summary>
public sealed record RetailNpcOwnedMovementPath(IReadOnlyList<RetailSearchPoint> Points, int Count, int Index,
    RetailSearchPoint Destination, uint? Extra30c, string StorageIdentity);
public sealed record RetailNpcOwnedMotion
{
    public required uint StartGate0 { get; init; }
    public required uint Deadline4 { get; init; }
    public required uint Pending8 { get; init; }
    public required RetailSearchPoint DesiredC { get; init; }
    public required RetailSearchPoint Next18 { get; init; }
    public required uint Heading24 { get; init; }
    public required uint Duration28 { get; init; }
    public required uint PreviousHeading2c { get; init; }
    public required uint PreviousValid30 { get; init; }
    public required uint Waypoint34 { get; init; }
    public required uint Flags38 { get; init; }
    public required uint Direction3c { get; init; }
    public required uint Direction40 { get; init; }
    public required uint Action44 { get; init; }
    public required uint Action48 { get; init; }
    public required int C4 { get; init; }
    public required int C8 { get; init; }
    public required int Cc { get; init; }
    public required int D0 { get; init; }
    public required int D4 { get; init; }
}
public sealed record RetailNpcMembershipCell(int Index, bool CompleteCurrentOwnership, string Revision,
    string EvidenceSha256, IReadOnlyList<uint> OrderedUsers, IReadOnlyList<uint> OrderedObjects,
    int ScriptSeeCount, int ScriptMoveCount);
public sealed record RetailNpcMovementWrapper(string Identity, uint ActorId, int ParentCell,
    string? Next, string? Previous, byte DelayFreed3f,ushort Magic3c,byte Magic3e);
public sealed record RetailNpcOwnedWorld(uint WorldId, int WidthMetres, int HeightMetres,
    int Columns32, int CellCount32, int Columns64, int LockCount64, int Deleting149b8,
    IReadOnlyList<RetailNpcMembershipCell> Cells, IReadOnlyList<RetailNpcMovementWrapper> Wrappers);
public sealed record RetailNpcMovementGlobals(int DebugGuards141781298, byte DebugGuard171cfdcd0,
    int LockDiagnostics1417818d8, byte DebugFlags1417817c4, int Sampling14176cb40,
    uint MoveNpcCalls174c54720, uint MaxObjects141774ae0, uint WorldHashDivisor14176f0a8,
    uint ObjectWrapperRtti174c5e224, int Reuse1417819f4, uint MaxPathProcessing14176cb54,
    int NoRetry14176cb44, int ClosingAi1438549c4, int ClosingIo171d640ec,
    int SendNextAsDesired14176cb6c);
public sealed record RetailNpcDelayedWrapper(string WrapperIdentity, uint ActorId, uint? Timestamp, byte DelayFreed3f);
public sealed record RetailNpcOwnedDelayRing(int ThreadIndex, uint Mask, int Capacity, uint ReadIndex,
    uint WriteIndex, IReadOnlyDictionary<int,RetailNpcDelayedWrapper> Slots, int NpcCount146955148,
    bool CompleteCurrentOwnership);
/// <summary>Logical fields only. Native uninitialized stack padding is not fabricated as zero bytes.</summary>
public sealed record RetailNpcMovementTimerEntry(uint Due, uint ActorId, int TimerId, ulong Parameter,
    uint Generation64, string ActorLifetime, string TimerIdentity);
public sealed record RetailNpcOwnedTimers(IReadOnlyList<RetailNpcMovementTimerEntry> Entries, int Capacity,
    int StripeSelectorEnabled146950684, uint ThreadStripe, uint StripeCount1417719c8,
    uint StripeNextWake, uint GlobalNextWake14176f9b8, ulong SumDelays146952ea8, ulong Added1469536a8,
    uint QueueStripeIndex,string QueueIdentity);
public sealed record RetailNpcModelStorage(int AvailablePathPointSlots, int AvailableWrapperSlots,
    int UsedPathPointSlots, int UsedWrapperSlots, string ReservationIdentity,
    bool SerializedModelOwnership, bool AllocatorNormalReturnDeclaration);
public sealed record RetailNpcMovementClockRead(string Role, ulong NativeCallSite, uint Value,
    string RequestRevision, string ActorLifetime);
public sealed record RetailNpcMovementSnapshot(RetailMovementOwnership Ownership, RetailNpcMovementActor Actor,
    RetailNpcOwnedMovementPath? Path, RetailNpcOwnedMotion Motion, RetailNpcOwnedWorld World,
    RetailNpcMovementGlobals Globals, RetailNpcOwnedDelayRing DelayedWrappers,
    RetailNpcOwnedTimers Timers, RetailNpcModelStorage Storage,
    IReadOnlyList<RetailNpcMovementClockRead> ClockReads)
{
    /// <summary>Raw/unproved +30c adjacent word survives logical Free/Clone. It is never consumed for a movement decision.</summary>
    public uint? UnconsumedPathWord30c { get; init; }
}
public sealed record RetailNpcMovementWrite(int Sequence, ulong NativeAddress, string Target,
    string OldValue, string NewValue);
public sealed record RetailNpcCalculatedChild(int Sequence, ulong NativeAddress, string Program,
    string EvaluatedGuard, string ConsumedInputs);
public enum RetailNpcMovementEffectKind
{ NativeInterserverMove, NativeInterserverStop, TimerRegistration, TimerDispatchCompletion, ModelAllocation, ModelFree, DiagnosticLog, SetEvent }
public sealed record RetailNpcMovementEffect(int Sequence, RetailNpcMovementEffectKind Kind, ulong NativeAddress,
    string Description, RetailSearchPoint? Position = null, RetailSearchPoint? Desired = null,
    uint? Heading = null, uint? Flags = null, uint? Duration = null,
    RetailNpcMovementTimerEntry? Timer = null);
public sealed record RetailNpcMovementCalculation(RetailMovementCalculationStatus Status, uint? NativeRawResult,
    RetailMovementExecutionStatus Execution, RetailNpcMovementSnapshot State,
    IReadOnlyList<RetailNpcMovementWrite> Writes, IReadOnlyList<RetailNpcCalculatedChild> Children,
    IReadOnlyList<RetailNpcMovementEffect> Effects, RetailMovementDomainFailure? Failure,
    bool WholeEffectListApplicable)
{
    /// <summary>Identifies the uint-returning consumer when NativeRawResult is supplied. Void motion/timer programs have no invented return word.</summary>
    public ulong? NativeResultEntry { get; init; }
    /// <summary>Future adapters must reject/invalidate the matching owned path explicitly on a managed rejection; no native false/automatic rollback is implied.</summary>
    public bool RequiresAdapterRejectionPathInvalidation => Failure is not null;
    public bool RuntimeVerified => false;
    public bool HostEffectsExecuted => false;
    public bool ActivationReady => false;
}

/// <summary>Internal reducer, never a caller-supplied mutation tape or a live actor/scheduler.</summary>
internal sealed class RetailNpcMovementReduction
{
    internal readonly RetailMovementLimits Limits;
    internal readonly RetailGroundMovementScene Scene;
    internal RetailNpcMovementSnapshot Value;
    internal readonly List<RetailNpcMovementWrite> Writes=[];
    internal readonly List<RetailNpcCalculatedChild> Children=[];
    internal readonly List<RetailNpcMovementEffect> Effects=[];
    private int reads, sequence;
    internal RetailNpcMovementReduction(RetailNpcMovementSnapshot input,RetailGroundMovementScene scene,RetailMovementLimits limits)
    {
        Limits=limits;Scene=scene;
        RetailMovementArithmetic.Require(limits.MaxOperations>0&&limits.MaxSteps>0&&limits.MaxPathPoints>0&&limits.MaxMembers>0&&limits.MaxMembershipCells>0&&limits.MaxTimerEntries>0&&limits.MaxDelayedWrappers>0,"limits","All defensive count limits must be positive.");
        if(input.Path?.Points.Count>limits.MaxPathPoints||input.World.Cells.Count>limits.MaxMembershipCells||input.World.Wrappers.Count>limits.MaxMembers||
            input.Timers.Entries.Count>limits.MaxTimerEntries||input.DelayedWrappers.Slots.Count>limits.MaxDelayedWrappers||input.ClockReads.Count>limits.MaxOperations)
            RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.DefensiveLimit,0,"snapshot-copy-budget","Owned input graph exceeds bounded copy support.");
        long members=0;
        foreach(var cell in input.World.Cells)
        {members+=(long)cell.OrderedUsers.Count+cell.OrderedObjects.Count;if(members>limits.MaxMembers)RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.DefensiveLimit,0,"membership-copy-budget","Owned member-list copy budget exceeded.");}
        // Copy every externally mutable list/dictionary into this run. Original snapshots remain untouched.
        Value=input with {
            Path=input.Path is null?null:input.Path with {Points=Array.AsReadOnly(input.Path.Points.ToArray())},
            World=input.World with {Cells=Array.AsReadOnly(input.World.Cells.Select(c=>c with {OrderedUsers=Array.AsReadOnly(c.OrderedUsers.ToArray()),OrderedObjects=Array.AsReadOnly(c.OrderedObjects.ToArray())}).ToArray()),Wrappers=Array.AsReadOnly(input.World.Wrappers.ToArray())},
            Timers=input.Timers with {Entries=Array.AsReadOnly(input.Timers.Entries.ToArray())},
            DelayedWrappers=input.DelayedWrappers with {Slots=new System.Collections.ObjectModel.ReadOnlyDictionary<int,RetailNpcDelayedWrapper>(input.DelayedWrappers.Slots.ToDictionary(p=>p.Key,p=>p.Value))},
            ClockReads=Array.AsReadOnly(input.ClockReads.ToArray())};
        if(Value.Path is not null)
        {
            if(Value.UnconsumedPathWord30c is not null&&Value.Path.Extra30c is not null&&Value.UnconsumedPathWord30c!=Value.Path.Extra30c)
                RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.InvalidInput,0,"adjacent-word-alias","Conflicting declarations of the same unconsumed raw adjacent word.");
            Value=Value with {UnconsumedPathWord30c=Value.Path.Extra30c??Value.UnconsumedPathWord30c};
        }
    }
    internal RetailNpcMovementActor Actor {get=>Value.Actor;set=>Value=Value with {Actor=value};}
    internal RetailNpcOwnedMotion Motion {get=>Value.Motion;set=>Value=Value with {Motion=value};}
    internal RetailNpcOwnedMovementPath? Path {get=>Value.Path;set=>Value=Value with {Path=value};}
    internal RetailNpcOwnedWorld World {get=>Value.World;set=>Value=Value with {World=value};}
    internal RetailNpcMovementGlobals Globals {get=>Value.Globals;set=>Value=Value with {Globals=value};}
    internal RetailNpcOwnedTimers Timers {get=>Value.Timers;set=>Value=Value with {Timers=value};}
    internal RetailNpcOwnedDelayRing Delayed {get=>Value.DelayedWrappers;set=>Value=Value with {DelayedWrappers=value};}
    internal RetailNpcModelStorage Storage {get=>Value.Storage;set=>Value=Value with {Storage=value};}
    private int Next()
    {if(sequence>=Limits.MaxOperations)RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.DefensiveLimit,0,"operation-budget","Bounded reducer operation limit reached.");return ++sequence;}
    internal void Write(ulong entry,string target,object? old,object? value)=>Writes.Add(new(Next(),entry,target,Format(old),Format(value)));
    internal void Child(ulong entry,string program,string guard,string inputs)=>Children.Add(new(Next(),entry,program,guard,inputs));
    internal void Effect(RetailNpcMovementEffect effect)=>Effects.Add(effect with {Sequence=Next()});
    internal uint Tick(string role,ulong callSite)
    {
        if(reads>=Value.ClockReads.Count)RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.IncompleteAtChild,callSite,"clock-read",$"Missing reached clock read {role}.");
        var r=Value.ClockReads[reads++];var o=Value.Ownership;
        if(r.Role!=role||r.NativeCallSite!=callSite||r.RequestRevision!=o.RequestRevision||r.ActorLifetime!=o.ActorLifetime)
            RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.StaleOwnership,callSite,"clock-read","Clock role/site/order/lifetime/revision differs.");
        Next();return r.Value;
    }
    internal void Incomplete(ulong entry,string boundary,string detail)=>RetailMovementArithmetic.Stop(RetailMovementCalculationStatus.IncompleteAtChild,entry,boundary,detail);
    internal void PathAcquire(ulong entry)
    {
        if(Actor.PathLock290!=0)Incomplete(entry,"busy-path-lock","Actual spin/wait owner is not guessed.");
        Write(entry,"actor+290.exchange",Actor.PathLock290,1);Actor=Actor with {PathLock290=1};
    }
    internal void PathRelease(ulong entry)
    {Write(entry,"actor+290.exchange",Actor.PathLock290,0);Actor=Actor with {PathLock290=0};}
    internal void InvalidatePath()
    {PathAcquire(0x140124890);FreePath(0x140124890);PathRelease(0x140124890);}
    internal void FreePath(ulong entry)
    {
        if(Path is not null)
        {
            Write(entry,"path2f0/header",$"{Path.StorageIdentity}/{Path.Count}/{Path.Index}","null/0/0/metadata0; +30c adjacent word unchanged");
            Storage=Storage with {UsedPathPointSlots=System.Math.Max(0,Storage.UsedPathPointSlots-Path.Points.Count)};
            Effect(new(0,RetailNpcMovementEffectKind.ModelFree,entry,"Logical owned path storage released; native allocator not executed."));
        }
        Path=null;
    }
    internal RetailNpcMovementCalculation Result(RetailMovementCalculationStatus status,uint? native=null,RetailMovementDomainFailure? failure=null)
    {
        if(failure is null&&reads!=Value.ClockReads.Count)
        {status=RetailMovementCalculationStatus.InvalidInput;failure=new(status,0,"unused-clock-tail","Extra observation reads cannot be silently ignored.");native=null;}
        bool applicable=failure is null&&(status is RetailMovementCalculationStatus.CalculatedApplicationChain or RetailMovementCalculationStatus.NativeEarlyReturn);
        return new(status,native,Effects.Count==0?RetailMovementExecutionStatus.Unexecuted:RetailMovementExecutionStatus.HostEffectsUnexecuted,
            Value,Array.AsReadOnly(Writes.ToArray()),Array.AsReadOnly(Children.ToArray()),Array.AsReadOnly(Effects.ToArray()),failure,applicable);
    }
    private static string Format(object? x)=>x switch
    {null=>"null",float f=>$"0x{BitConverter.SingleToUInt32Bits(f):x8}",RetailSearchPoint p=>$"({BitConverter.SingleToUInt32Bits(p.X):x8},{BitConverter.SingleToUInt32Bits(p.Y):x8},{BitConverter.SingleToUInt32Bits(p.Z):x8})",_=>Convert.ToString(x,System.Globalization.CultureInfo.InvariantCulture)??"null"};
}
