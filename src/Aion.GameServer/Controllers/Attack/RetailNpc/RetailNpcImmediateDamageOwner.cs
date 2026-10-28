using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Inactive finite replay of Npc::GetHitDamage, including the inherited Creature owner,
/// through the immediate return. No game objects, callbacks, clocks, RNG or transport run.
/// It accepts two distinct ordinary ownerless NPCs and calculated no-End effect branches.
/// The original normal Evaluate rejects an unsupported reached program without exposed mutations.
/// The distinct private-producer skill entry retains a typed known prefix at its unsupported boundary.
/// </summary>
/// <remarks>
/// Main 140b232b0 / 140372920; external phase3/hp-owner-dependency-closure pins the
/// full bodies, vtables, source spans and independent vectors. Runtime observations are
/// unavailable. Native allocator/locking/transport/telemetry normal return and absence of
/// inline actor writers are explicit conditional infrastructure contracts, not HP answers.
/// Deferred TimerExpired/Die/effects/rewards are outside this immediate owner boundary.
/// </remarks>
internal static class RetailNpcImmediateDamageOwner
{
    internal const string Dataset = "33dfd001439620a102ccfca9b30765578efa88acb6f561f8c116ce8d98873335";
    internal const string Region = "generation-a-USA-base;loaded-context-unavailable";
    internal const string WorldSourceSha256 = "bb7f5f4e08586840013db50d48f6a8e27d7463ed1e9a7e17dee9e8d9625d1fe5";
    internal const ulong NpcVtable = 0x1412b8470;
    internal const ulong WorldNop = 0x1400b0680;
    private static readonly ulong[] NopWorldVtables =
    [
        0x141242520, 0x1412575d8, 0x141242cc0, 0x1412254f0, 0x141225c18,
        0x141227518, 0x141229fa8, 0x141227090, 0x14122bc48, 0x14122a610,
        0x14122b1f0, 0x141263180, 0x1412638f0, 0x1412546a8, 0x141246950, 0x1412521d8
    ];

    internal enum FactKind { Unknown, SourceBoundDeclaration, ObservedOriginal }
    internal sealed record Evidence(FactKind Kind, string Receipt);
    internal sealed record Identity(string MainSha256, string DatasetSha256, string RegionContext,
        string Operation, string Epoch, Evidence Evidence);
    internal sealed record ActorBinding(uint ObjectId, int TemplateId, int Type144, ulong Vtable,
        string LifeGeneration, string ManagerGeneration, string Operation, string Epoch, Evidence Evidence);
    internal sealed record FloatingContract(bool RoundToNearestEven, bool MaskedExceptions,
        bool NormalReturn, Evidence Evidence);
    internal sealed record Infrastructure(bool SerializedImmediateInterval, bool NoOtherActorWriter,
        bool NoInlineGameCallback, bool NoDeferredCallbackDuringInvocation,
        bool NormalAllocationLockTransportTelemetryReturn, bool FreshDisjointStorage,
        bool StableActorAndSocketLifetimes, Evidence Evidence);

    internal enum CellStorage { Unknown, OrdinaryNormalRecordApplicationCell, OverlappingActorStorage,
        PhysicalSkillCompletionApplicationCell }
    internal enum ApplicationCellRole { Unknown, PrimaryNormalCompletionLocal, RetaliationNormalCompletionLocal,
        PrimaryPhysicalSkillCompletionLocal, RetaliationPhysicalSkillCompletionLocal }
    /// <summary>
    /// A value is insufficient evidence of the native pointer domain. This binding explicitly
    /// declares a supported original record cell, disjoint from all actor/effect/ref/timer storage.
    /// It does not admit scratch cells from the signed heal dispatcher or arbitrary pointers.
    /// OwnershipFunction/Source pin the normal-completion stack-storage proof, not a claim
    /// that the surrounding World call/context was replayed. Entry-context fields are independent.
    /// </summary>
    internal sealed record ApplicationCellBinding(CellStorage Storage, string RecordIdentity,
        string CellIdentity, string Operation, string Epoch, bool DisjointFromActorOwnedStorage,
        bool IsOriginalApplicationCell, ulong OwnershipFunction, string OwnershipSourceSha256,
        ApplicationCellRole Role, Evidence Evidence);
    internal sealed record Invocation(int RequestedHp, int RequestedMp, int RawThreat, int Category,
        int Context08, int Context0c, int Context10, int Kind14, uint Skill18, int Log1c,
        byte Flags20, byte Critical21, ApplicationCellBinding Cell);

    internal sealed record NativeTemplate(byte RaceAlert3ff, byte Bindstone400, long Xp198,
        int Reward2f0, int Reward370, int Reward1a0, int SourceType354,
        byte SourceFlag3ec, byte SourceFlag3ed, int Telemetry404);
    internal sealed record References(int Count8, int PendingTimers10, int History14, int Generation68);
    internal sealed record OwnerState(int MaxHp774, int Hp778, int MaxMp78c, int Mp790,
        int Pending728, uint Dirty770, int Field788, int Field72c, uint LastAttacker1308,
        uint? Killer918, uint Master188c, uint Related1894, ulong Pointer1850);
    internal readonly record struct PositionBits(uint X, uint Y, uint Z);
    internal sealed record TelemetryScalars(uint LogId100, int Raw734, int Race738,
        ushort Level1492, PositionBits Position120);
    internal sealed record Actor(ActorBinding Binding, OwnerState State, NativeTemplate Template,
        References References, TelemetryScalars Telemetry);
    internal sealed record World(ulong Vtable, uint NativeId24, uint MapId20,
        byte TribeConflict18468, string Generation, string Operation, string Epoch, Evidence Evidence);

    internal enum ManagerPresence { Unknown, VerifiedNull, Present }
    internal sealed record EffectRecord(ulong SkillPointer, short Level4c);
    internal sealed record EffectGroup(int Index, bool Complete, IReadOnlyList<EffectRecord> Records,
        Evidence Evidence);
    internal sealed record Manager(ManagerPresence Presence, ulong Pointer158, uint? Flags2b18,
        string Generation, IReadOnlyList<EffectGroup> Groups, Evidence Evidence);
    internal sealed record BoundSkill(ulong? Pointer, int? Group, Evidence Evidence);
    internal enum ActionPresence { Unknown, VerifiedNull, Present }
    internal sealed record ActionInfo(ActionPresence Presence, uint? Flags, Evidence Evidence);
    internal sealed record Guards(uint? Abnormal1250, uint? CurrentCast131c, uint? Systems12cc,
        byte? Byte1425, Manager Manager, BoundSkill Bound160, BoundSkill Bound210,
        BoundSkill Bound168, ActionInfo ActionInfo, Evidence Evidence);

    /// <summary>Complete logical POD map, not native allocator/tree storage.</summary>
    internal sealed record AttributionEntry(uint Key, uint SourceId, uint PartyId, int LegionId,
        int LegionWorld, int Faction, int Classification, int Damage, uint FirstTick,
        uint LastTick, uint Flags);
    internal sealed record Attribution(uint LastTick68, uint LastSpecialSource6c,
        IReadOnlyList<AttributionEntry> Entries, Evidence Evidence);
    internal sealed record RewardGuard(int GlobalXpRate, Evidence Evidence);

    internal enum SocketPresence { Unknown, VerifiedNull, Present }
    internal enum OpenSendOutcome { Unknown, NormalQueuedOrSent, NormalSendError }
    internal sealed record NpcSocket(int Slot, SocketPresence Presence, int? State414,
        int? ReferenceCount8, int? History14, OpenSendOutcome OpenOutcome, Evidence Evidence);
    internal sealed record Transport(IReadOnlyList<NpcSocket> Sockets, Evidence Evidence);
    internal enum TickRole { AttributionOuter, AttributionDecay, TimerEntry, TimerDue, TimerExit }
    /// <summary>Supplied original-call values; declaration provenance is valid for static fixtures only.</summary>
    internal sealed record Tick(TickRole Role, ulong CallSite, uint Value, string Operation,
        string Epoch, Evidence Evidence);
    internal sealed record TimerEntry(uint Due, uint ActorId, int TimerId, ulong Data, int Generation);
    internal sealed record TimerQueue(uint Index, uint MinimumWake, IReadOnlyList<TimerEntry> Heap);
    internal sealed record Scheduler(uint ClosingIo, bool RotationEnabled, uint TlsIndex,
        uint QueueCount, uint GlobalNextWake, uint PendingSleepMilliseconds,
        ulong AccumulatedDelay, ulong EnqueueCount, IReadOnlyList<TimerQueue> Queues, Evidence Evidence);
    internal sealed record Input(RetailNpcNormalScope Scope, Identity Source, Actor Attacker,
        Actor Receiver, World World, Invocation Call, Guards Guards, Attribution Attribution,
        RewardGuard Reward, Scheduler Scheduler, Transport Transport, FloatingContract Floating,
        Infrastructure Infrastructure, IReadOnlyList<Tick> Ticks);

    internal enum Completion { RejectedBeforeEvaluation, CompleteImmediateOwner }
    internal enum TraceKind
    {
        LastAttackerWrite, InitialGate, BreakGuard, OnGetGuard, CastGuard, SiegeGuard,
        MpAtomic, MpThresholdGuard, HpAtomic, DeathPolicy, PendingDeathWrite, NoKillRestore,
        WhenDeadGuard, Pass2Guard, HpThresholdGuard, Npc159Nop, Clear788, Tail161,
        ActionGuard, CoreReturn, WorldOnDamageNop, TribeRead, RewardRead, MasterSkip,
        BindstoneSkip, DirtyClear, KillerCas, TimerClosing, TimerSleepRequest,
        TimerPendingWrite, TimerQueueSelect, TimerMinWake, TimerWakeRequest, TimerSlowLog, OwnerReturn
    }
    internal abstract record OwnerEvent(int Ordinal, ulong NativeSite);
    internal sealed record Trace(int Ordinal, ulong NativeSite, TraceKind Kind,
        long Before, long After) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record NoEnd(int Ordinal, ulong NativeSite, ulong SkillPointer, int Group,
        short? FirstLevel, int Reason, bool ManagerPresent) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record Pass2Read(int Ordinal, ulong NativeSite, int MaxHp, int MaxMp,
        int CurrentHp, int CurrentMp) : OwnerEvent(Ordinal, NativeSite);
    internal enum SyncKind { Hp, Damage, Hate }
    internal enum TransportDisposition { NoSocketReleased, ClosedReleased, QueuedOrSent, SendError }
    internal sealed record TransportEffects(int Slot, int SendSyncReturn,
        TransportDisposition Disposition, bool? BufferReleaseInvoked,
        int SendSyncReferenceAdds, int SendSyncReferenceReleases, int? SocketReferencesAfter);
    internal sealed record HateParameters(uint SourceId, uint PartyAllianceId, uint ReceiverId,
        long Threat, uint Param5, bool Flag);
    internal sealed record Sync(int Ordinal, ulong NativeSite, SyncKind Kind,
        uint ReceiverId, uint SourceId, int? CurrentHp, int? MaxHp, int? ApplicationCell,
        long? Threat, ushort? Skill, int? ActorKind, HateParameters? HateArgs,
        TransportEffects Transport) : OwnerEvent(Ordinal, NativeSite);
    /// <summary>
    /// Ordered HP broadcast request, not an invented recipient/geometry/transport outcome.
    /// The adapter's visibility contract is DPPS(0x71), CVTTSS2SI32 then signed <=10000.
    /// </summary>
    internal sealed record WorldHp(int Ordinal, ulong NativeSite, uint ReceiverId, uint SourceId,
        int Delta, int ChangeType, uint Skill, int Log, byte Critical, byte Ratio,
        PositionBits Position) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record HistoryUpdate(int Ordinal, ulong NativeSite, bool Cleared,
        uint OuterTick, uint? DecayTick, int? DecayedDamage, AttributionEntry Entry) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record RefChange(int Ordinal, ulong NativeSite, uint ActorId,
        int Before, int After, string Reason) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record TimerRequest(int Ordinal, ulong NativeSite, uint RequestedDelay,
        int TimerId, bool Enqueued, uint NativeReturnValue, uint? SelectedQueue,
        TimerEntry? Entry, uint EntryTick, uint? DueTick, uint? ExitTick) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record TimerInserted(int Ordinal, ulong NativeSite, uint Queue,
        TimerEntry Entry, ReadOnlyCollection<TimerEntry> HeapAfter) : OwnerEvent(Ordinal, NativeSite);
    /// <summary>
    /// Typed primitive telemetry arguments. Historical proposal pins 055700 source only;
    /// logger transport remains conditional normal-return/non-inline-actor-writer infrastructure.
    /// </summary>
    internal sealed record Telemetry(int Ordinal, ulong NativeSite, uint EventId,
        uint ReceiverLogId, uint ReceiverId, uint SourceLogId, uint SourceId, uint MapId,
        PositionBits Position, int ReceiverRace, int ReceiverLevel1492, int ReceiverRaw734,
        int ReceiverKind115, int SourceRace, int SourceLevel1492, int SourceRaw734,
        int SourceKind115, uint Skill, int Category, int Threat, int ReceiverTemplate404,
        long Sentinel30, long Sentinel31, long Sentinel32) : OwnerEvent(Ordinal, NativeSite);

    internal sealed record State(OwnerState Receiver, References ReceiverReferences,
        References AttackerReferences, Attribution Attribution, Scheduler Scheduler);
    internal sealed record Rejection(string Stage, string Reason, ulong NativeSite);
    internal sealed record Result(Completion Status, Identity Source, bool? CoreReturn,
        bool? OwnerReturn, int RequestedCell, int? ApplicationCell, int? ActualHpLoss,
        int? ActualMpDecrement, State? Calculated, ReadOnlyCollection<OwnerEvent> Events,
        ReadOnlyCollection<Tick> ConsumedTicks, Rejection? Rejection)
    {
        internal int? FinalHpAfterDeferredLife => null;
        internal int RngDrawsConsumed => 0;
        internal bool ActivationReady => false;
        internal string Boundary => "Complete admitted immediate owner; deferred TimerExpired/Die is separate.";
    }

    internal static Result Evaluate(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentNullException.ThrowIfNull(input.Call);
        ArgumentNullException.ThrowIfNull(input.Guards);
        ArgumentNullException.ThrowIfNull(input.Guards.Manager);
        ArgumentNullException.ThrowIfNull(input.Attacker);
        ArgumentNullException.ThrowIfNull(input.Receiver);
        ArgumentNullException.ThrowIfNull(input.World);
        ArgumentNullException.ThrowIfNull(input.Attribution);
        ArgumentNullException.ThrowIfNull(input.Scheduler);
        ArgumentNullException.ThrowIfNull(input.Transport);
        ArgumentNullException.ThrowIfNull(input.Floating);
        ArgumentNullException.ThrowIfNull(input.Infrastructure);
        try { return new Replay(Copy(input)).Run(); }
        catch (DomainException ex)
        {
            // No mutable object or partial trace escapes a rejected invocation.
            return new(Completion.RejectedBeforeEvaluation, input.Source, null, null,
                input.Call.RequestedHp, null, null, null, null, ReadOnly<OwnerEvent>([]),
                ReadOnly<Tick>([]), new(ex.Stage, ex.Message, ex.Site));
        }
    }

    /// <summary>Separate physical-skill entry. The producer is privately created by the
    /// complete wrapper/retained-completion replay; normal Evaluate is unchanged.</summary>
    internal sealed record SkillEvaluation(Result? Complete, State? KnownPrefixState,
        int? KnownApplicationCell, ReadOnlyCollection<OwnerEvent> KnownEvents,
        ReadOnlyCollection<Tick> KnownTicks, Rejection? Unsupported)
    {
        internal int? FinalHpAfterDeferredLife => null;
        internal bool ActivationReady => false;
    }
    internal static SkillEvaluation EvaluatePhysicalSkill(Input input,
        RetailNpcPhysicalSkillRecordCompletion.OwnerCall producer)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(producer);
        var replay = new Replay(Copy(input), producer);
        try
        {
            Result result = replay.Run();
            return new(result, null, null, ReadOnly<OwnerEvent>([]), ReadOnly<Tick>([]), null);
        }
        catch (DomainException ex)
        {
            // This is known state at an unsupported boundary, never a completed HP result.
            return replay.SkillBoundary(new(ex.Stage, ex.Message, ex.Site));
        }
    }

    /// <summary>Optional adapter comparison only; this does not calculate DPPS or recipients.</summary>
    internal static (int Truncated, bool Within) CompareSuppliedDppsResult(float squared, FloatingContract contract)
    {
        Floating(contract);
        Normal(squared, "visibility DPPS result", 0x1406896e8);
        Require(squared >= 0 && squared < 2147483648f, "visibility", "Unrepresentable or negative CVTT32 input.", 0x1406896e8);
        int truncated = (int)squared;
        return (truncated, truncated <= 10000);
    }

    private sealed class Replay(Input input, RetailNpcPhysicalSkillRecordCompletion.OwnerCall? skillProducer = null)
    {
        private readonly List<OwnerEvent> events = [];
        private readonly List<Tick> consumed = [];
        private OwnerState receiver = input.Receiver.State;
        private References sourceRefs = input.Attacker.References;
        private References receiverRefs = input.Receiver.References;
        private Attribution history = input.Attribution;
        private Scheduler scheduler = input.Scheduler;
        private int cell = input.Call.RequestedHp;
        private int actualMp;
        private int tickIndex;
        private int Ordinal => events.Count + 1;
        private Invocation Call => input.Call;

        internal Result Run()
        {
            ValidateEntry(input, skillProducer);
            Write(TraceKind.LastAttackerWrite, receiver.LastAttacker1308, input.Attacker.Binding.ObjectId, 0x140372a7e);
            receiver = receiver with { LastAttacker1308 = input.Attacker.Binding.ObjectId };
            bool nontrivial = cell > 0 || Call.RequestedMp > 0;
            Write(TraceKind.InitialGate, cell, Call.RequestedMp, 0x140372a84);
            bool core = false;
            if (nontrivial) core = Core();
            Write(TraceKind.CoreReturn, core ? 1 : 0, core ? 1 : 0, 0x1403731ac);

            if (core)
            {
                Require(NopWorldVtables.Contains(input.World.Vtable), "world", "Reached world slot44 is not a pinned NOP binding.", WorldNop);
                Write(TraceKind.WorldOnDamageNop, (long)input.World.Vtable, 0, WorldNop);
                Write(TraceKind.TribeRead, input.World.TribeConflict18468, input.World.TribeConflict18468, 0x1406a44d0);
                Require(input.World.NativeId24 != 400010000, "world", "Derived abyss HP alert branch is unsupported.", 0x140b234a4);
                bool reward = RewardIsNonzero();
                if (reward) UpdateHistory();
                Write(TraceKind.MasterSkip, receiver.Master188c, 0, 0x140b208c0);
                Require(input.Receiver.Template.Bindstone400 == 0, "bindstone", "Reached bindstone attacked program.", 0x140b2376a);
                Write(TraceKind.BindstoneSkip, 0, 0, 0x140b2376a);
                if ((Call.Context10 == 0 || receiver.Pending728 == 0) && Call.Context0c != 0)
                    SendSync(SyncKind.Damage, cell, Call.RawThreat, 0x140b23886);
            }
            else if (Call.Context0c != 0 && Call.RawThreat != 0)
                SendSync(SyncKind.Hate, 0, Call.RawThreat, 0x140b23e20);

            if (receiver.Hp778 < 1) DeathTail();
            Write(TraceKind.OwnerReturn, core ? 1 : 0, core ? 1 : 0, 0x140b23dae);
            Require(tickIndex == input.Ticks.Count, "clock", "Unconsumed supplied tick receipt.", 0);
            var state = new State(receiver, receiverRefs, sourceRefs, history, scheduler);
            return new(Completion.CompleteImmediateOwner, input.Source, core, core,
                Call.RequestedHp, cell, input.Receiver.State.Hp778 - receiver.Hp778, actualMp,
                state, ReadOnly(events), ReadOnly(consumed), null);
        }

        internal SkillEvaluation SkillBoundary(Rejection rejection) => new(null,
            events.Count == 0 ? null : new State(receiver, receiverRefs, sourceRefs, history, scheduler),
            events.Count == 0 ? null : cell, ReadOnly(events), ReadOnly(consumed), rejection);

        private bool Core()
        {
            Manager manager = input.Guards.Manager;
            bool present = ManagerKnown(manager);
            uint abnormal = Known(input.Guards.Abnormal1250, "break abnormal1250", 0x140371970);
            Write(TraceKind.BreakGuard, abnormal, Call.Kind14, 0x140371aa1);
            ulong secondPointer = Known(input.Guards.Bound210.Pointer, "bound210 pointer", 0x140371970);
            if (present)
            {
                if (Call.Kind14 == 2)
                    Require((abnormal & 0x210) == 0, "break", "Reached original sleep/break RNG program.", 0x140371aa1);
                if ((abnormal & 0x4000008) != 0) RemoveNoEnd(input.Guards.Bound160, 20, 0x140371b92);
                BoundSkill second = input.Guards.Bound210;
                if (secondPointer != 0) RemoveNoEnd(second, 20, 0x140371bc5);
            }
            if (present && cell != 0)
            {
                uint flags = Flags(manager);
                Write(TraceKind.OnGetGuard, flags, flags & 0x300, 0x14034d445);
                Require((flags & 0x300) == 0, "on-get", "Reached caster/party protector child program.", 0x14034d445);
            }
            if (Call.Kind14 == 2)
            {
                uint cast = Known(input.Guards.CurrentCast131c, "current cast131c", 0x140371651);
                Write(TraceKind.CastGuard, cast, 0, 0x140371651);
                Require(cast == 0, "cast", "Reached active cast cancellation/RNG program.", 0x140371651);
            }
            if (receiver.Hp778 == receiver.MaxHp774)
            {
                byte race = input.Receiver.Template.RaceAlert3ff;
                Write(TraceKind.SiegeGuard, race, 0, 0x140372b06);
                Require(race >= 13 || ((0x104aU >> race) & 1) == 0,
                    "siege", "Reached full-HP abyss ownership alert program.", 0x140372b06);
            }
            if (Call.RequestedMp > 0)
            {
                actualMp = Math.Min(receiver.Mp790, Call.RequestedMp);
                int old = receiver.Mp790;
                receiver = receiver with { Mp790 = old - actualMp, Dirty770 = receiver.Dirty770 | 2 };
                Write(TraceKind.MpAtomic, old, receiver.Mp790, 0x140366854);
                SystemSkip(2, TraceKind.MpThresholdGuard, 0x140379710);
            }

            bool lethal = cell >= receiver.Hp778;
            if (cell > 0)
            {
                int old = receiver.Hp778;
                if (cell < old)
                    receiver = receiver with { Hp778 = old - cell };
                else
                {
                    receiver = receiver with { Hp778 = 0 };
                    cell = old; // Native XCHG old HP -> ORIGINAL application cell, not actual loss.
                }
                Write(TraceKind.HpAtomic, old, receiver.Hp778, lethal ? 0x140372d01UL : 0x140372ce5UL);
            }
            if (cell > 0 && lethal)
            {
                Write(TraceKind.DeathPolicy, 0, 0, 0x140b22220); // ordinary ownerless NPC slot179
                if ((Call.Flags20 & 1) != 0)
                {
                    receiver = receiver with { Hp778 = 1 };
                    Write(TraceKind.NoKillRestore, 0, 1, 0x140372e8e);
                }
                else
                {
                    Write(TraceKind.PendingDeathWrite, receiver.Pending728, 1, 0x140372e98);
                    receiver = receiver with { Pending728 = 1 };
                    if (present)
                    {
                        uint flags = Flags(manager);
                        Write(TraceKind.WhenDeadGuard, flags, flags & 0x400, 0x14034deb4);
                        Require((flags & 0x400) == 0, "when-dead", "Reached death skill/effect program.", 0x14034deb4);
                    }
                }
            }
            else
            {
                if (present && cell != 0)
                {
                    uint flags = Flags(manager);
                    Write(TraceKind.Pass2Guard, flags, flags & 0x2080, 0x14034da5d);
                    // CheckCaseHealAndSkill returns before stat getters only when the ENTIRE flag word is zero.
                    if (flags != 0)
                        events.Add(new Pass2Read(Ordinal, 0x14034da5d, receiver.MaxHp774,
                            receiver.MaxMp78c, receiver.Hp778, receiver.Mp790));
                    Require((flags & 0x2080) == 0, "pass2", "Reached CaseSkill/CaseHeal child program.", 0x14034da5d);
                }
                SystemSkip(1, TraceKind.HpThresholdGuard, 0x140379600);
            }

            bool core = cell > 0 || actualMp > 0;
            if (core)
            {
                if (Call.Context08 == 1)
                {
                    if (cell > 0) { SendHp(); Broadcast(-cell, Call.Kind14, Call.Log1c, Call.Critical21); }
                }
                else { SendHp(); Broadcast(0, 5, 0xc5, 0); }
                // NPC branch never reaches User::SendMP, including positive MP damage.
            }
            Write(TraceKind.Npc159Nop, Call.Kind14, lethal ? 1 : 0, 0x1400b0680);
            Write(TraceKind.Clear788, receiver.Field788, 0, 0x140372fc2);
            receiver = receiver with { Field788 = 0 };
            byte mode = Known(input.Guards.Byte1425, "tail byte1425", 0x14037bcc0);
            Write(TraceKind.Tail161, mode, mode is 1 or 3 ? 0 : 1, 0x14037bcc0);
            if (mode is not (1 or 3)) RemoveNoEnd(input.Guards.Bound168, 10, 0x1400b2a00);
            ActionInfo action = input.Guards.ActionInfo;
            EvidenceKnown(action.Evidence, "action-info");
            Require(action.Presence is ActionPresence.VerifiedNull or ActionPresence.Present,
                "action-info", "Unknown/unsupported current action-info pointer.", 0x140372fdc);
            uint actionFlags = action.Presence == ActionPresence.Present
                ? Known(action.Flags, "action-info flags", 0x140372fe8) : 0;
            Write(TraceKind.ActionGuard, actionFlags, actionFlags & 0x10a17, 0x140372fea);
            Require((actionFlags & 0x10a17) == 0, "action-info", "Reached action-info system skill program.", 0x140372fea);
            // No mask means the native jump also SKIPS the later field72c clear.
            return core;
        }

        private void SystemSkip(uint bit, TraceKind kind, ulong site)
        {
            uint systems = Known(input.Guards.Systems12cc, "system flags12cc", site);
            Write(kind, systems, systems & bit, site);
            Require((systems & bit) == 0, "system-effect", "Reached HP/MP threshold system child program.", site);
        }

        private void RemoveNoEnd(BoundSkill binding, int reason, ulong site)
        {
            EvidenceKnown(binding.Evidence, "bound skill");
            ulong pointer = Known(binding.Pointer, "bound skill pointer", site);
            int group = Known(binding.Group, "bound group index", site);
            bool present = ManagerKnown(input.Guards.Manager);
            short? firstLevel = null;
            if (present && pointer != 0)
            {
                Require(group is >= 0 and < 8, "removal", "Reached effect-group index is outside 0..7.", 0x140098330);
                var groups = input.Guards.Manager.Groups.Where(g => g.Index == group).ToArray();
                Require(groups.Length == 1 && groups[0].Complete, "removal", "Reached effect group is unknown or incomplete.", 0x140098330);
                EffectGroup found = groups[0]; EvidenceKnown(found.Evidence, "effect-group");
                foreach (EffectRecord record in found.Records)
                    if (record.SkillPointer == pointer) { firstLevel = record.Level4c; break; }
                Require(firstLevel is null or 0, "removal", "First matching signed16 level is nonzero; active End is unsupported.", 0x140349ef0);
            }
            events.Add(new NoEnd(Ordinal, site, pointer, group, firstLevel, reason, present));
        }

        private bool RewardIsNonzero()
        {
            EvidenceKnown(input.Reward.Evidence, "reward guard");
            Floating(input.Floating);
            float xp = Normal((float)input.Receiver.Template.Xp198, "XP int64->f32", 0x140b236f6);
            float rate = Normal((float)input.Reward.GlobalXpRate, "rate int32->f32", 0x140b23709);
            float divided = Normal(Divide(rate, 100f), "XP DIVSS", 0x140b2370c);
            float product = Normal(Multiply(divided, xp), "XP MULSS", 0x140b23714);
            Require(product >= -9223372036854775808f && product < 9223372036854775808f,
                "reward", "XP CVTTSS2SI64 input is unrepresentable.", 0x140b23718);
            long scaled = (long)product;
            bool result = scaled != 0 || input.Receiver.Template.Reward2f0 != 0 ||
                input.Receiver.Template.Reward370 != 0 || input.Receiver.Template.Reward1a0 != 0;
            Write(TraceKind.RewardRead, scaled, result ? 1 : 0, 0x140b23718);
            return result;
        }

        private void UpdateHistory()
        {
            EvidenceKnown(history.Evidence, "attacker history");
            uint outer = ReadTick(TickRole.AttributionOuter, 0x1402a740c);
            int ttl = input.Receiver.Template.RaceAlert3ff == 1 ? 180 : 600;
            uint special = input.Attacker.Template.SourceType354 == 2
                ? input.Attacker.Binding.ObjectId : history.LastSpecialSource6c;
            bool clear = unchecked(outer - (uint)(ttl * 1000)) > history.LastTick68;
            var entries = clear ? new List<AttributionEntry>() : history.Entries.ToList();
            int index = entries.FindIndex(e => e.Key == input.Attacker.Binding.ObjectId);
            uint? inner = null; int? decayed = null;
            AttributionEntry entry;
            if (index < 0)
            {
                uint flags = (input.Attacker.Template.SourceType354 == 2 ? 1u : 0) |
                    (input.Attacker.Template.SourceFlag3ec != 0 ? 0x100u : 0) |
                    (input.Attacker.Template.SourceFlag3ed != 0 ? 0x10000u : 0);
                entry = new(input.Attacker.Binding.ObjectId, input.Attacker.Binding.ObjectId,
                    0, 0, 0, 2, 5, cell, outer, outer, flags);
                entries.Add(entry);
            }
            else
            {
                inner = ReadTick(TickRole.AttributionDecay, 0x1402aa365);
                AttributionEntry old = entries[index];
                uint elapsed = unchecked(inner.Value - old.LastTick) / 1000;
                int amount = elapsed >= ttl ? 0 : Math.Min(old.Damage, 0x4fffff);
                decayed = amount;
                entry = old with { Damage = unchecked(amount + cell),
                    FirstTick = amount == 0 ? outer : old.FirstTick, LastTick = outer };
                entries[index] = entry;
            }
            history = history with { LastTick68 = outer, LastSpecialSource6c = special,
                Entries = ReadOnly(entries.OrderBy(e => e.Key)) };
            events.Add(new HistoryUpdate(Ordinal, 0x1402a72b0, clear, outer, inner, decayed, entry));
        }

        private void SendHp()
        {
            if (receiver.Pending728 == 0) SendSync(SyncKind.Hp, 0, 0, 0x140b22aa0);
            uint old = receiver.Dirty770;
            receiver = receiver with { Dirty770 = old & ~1u };
            Write(TraceKind.DirtyClear, old, receiver.Dirty770, 0x140b22c53);
            Write(TraceKind.MasterSkip, receiver.Master188c, 0, 0x140b208c0);
        }

        private void Broadcast(int delta, int type, int log, byte critical)
        {
            byte ratio = receiver.Hp778 < 1 || receiver.MaxHp774 < 1 ? (byte)0 :
                unchecked((byte)(((long)receiver.Hp778 * 99 / receiver.MaxHp774) + 1));
            events.Add(new WorldHp(Ordinal, 0x140373f10, input.Receiver.Binding.ObjectId,
                input.Attacker.Binding.ObjectId, delta, type, 0, log, critical, ratio,
                input.Receiver.Telemetry.Position120));
        }

        private void SendSync(SyncKind kind, int applicationCell, int threat, ulong site)
        {
            EvidenceKnown(input.Transport.Evidence, "transport");
            int slot = (int)(input.Receiver.Binding.ObjectId & 7);
            var endpoints = input.Transport.Sockets.Where(s => s.Slot == slot).ToArray();
            Require(endpoints.Length == 1, "transport", "Reached NPC socket slot is missing/ambiguous.", 0x1404a2d40);
            NpcSocket endpoint = endpoints[0]; EvidenceKnown(endpoint.Evidence, "NPC socket");
            Require(endpoint.Presence is SocketPresence.VerifiedNull or SocketPresence.Present,
                "transport", "Unknown/unsupported current NPC socket presence.", 0x1404a2d40);
            TransportEffects effect;
            if (endpoint.Presence == SocketPresence.VerifiedNull)
                effect = new(slot, 0, TransportDisposition.NoSocketReleased, true, 0, 0, null);
            else
            {
                int refs = Known(endpoint.ReferenceCount8, "socket refs8", 0x140d02cc0);
                int historyFlag = Known(endpoint.History14, "socket history14", 0x140d02cc0);
                Require(refs > 1 && refs < int.MaxValue && historyFlag == 0,
                    "transport", "Unsafe socket reference/destructor/history domain.", 0x140d02cc0);
                int state = Known(endpoint.State414, "socket state414", 0x1404bb2a0);
                if (state == 2)
                    effect = new(slot, 1, TransportDisposition.ClosedReleased, true, 1, 1, refs);
                else
                {
                    Require(endpoint.OpenOutcome is OpenSendOutcome.NormalQueuedOrSent or OpenSendOutcome.NormalSendError,
                        "transport", "Open transport normal-return disposition is unknown.", 0x1404bb2a0);
                    bool error = endpoint.OpenOutcome == OpenSendOutcome.NormalSendError;
                    // Only SendSync's temporary +1/-1 is closed here. Open I/O can retain
                    // another socket/buffer reference; do not invent its final lifetime state.
                    effect = new(slot, 1, error ? TransportDisposition.SendError : TransportDisposition.QueuedOrSent,
                        null, 1, 1, null);
                }
            }
            events.Add(new Sync(Ordinal, site, kind, input.Receiver.Binding.ObjectId,
                kind == SyncKind.Hp ? 0 : input.Attacker.Binding.ObjectId,
                kind == SyncKind.Hate ? null : receiver.Hp778,
                kind == SyncKind.Hp ? receiver.MaxHp774 : null,
                kind == SyncKind.Damage ? applicationCell : null,
                kind == SyncKind.Hp ? null : (long)threat,
                kind == SyncKind.Damage ? unchecked((ushort)Call.Skill18) : null,
                kind == SyncKind.Hp ? 0x11 : null,
                kind == SyncKind.Hate ? new(input.Attacker.Binding.ObjectId, 0,
                    input.Receiver.Binding.ObjectId, (long)threat, 0, false) : null, effect));
        }

        private void DeathTail()
        {
            // Initial alive/pending0 plus all admitted no-writer branches make pending1 here.
            Require(receiver.Pending728 == 1, "death-tail", "Unexpected pending-death state.", 0x140b238f5);
            RefSource(1, "killer acquisition", 0x140d02cc0);
            uint? old = receiver.Killer918;
            receiver = receiver with { Killer918 = old ?? input.Attacker.Binding.ObjectId };
            Write(TraceKind.KillerCas, old ?? 0, receiver.Killer918 ?? 0, 0x140b23933);
            if (old is not null) RefSource(-1, "failed killer CAS", 0x140d02e30);
            QueueDeathTimer();
            var a = input.Attacker.Telemetry; var d = input.Receiver.Telemetry;
            events.Add(new Telemetry(Ordinal, 0x140055700, 0x135, d.LogId100,
                input.Receiver.Binding.ObjectId, a.LogId100, input.Attacker.Binding.ObjectId,
                input.World.MapId20, d.Position120, d.Race738, d.Level1492, d.Raw734, 0x11,
                a.Race738, a.Level1492, a.Raw734, 0x11,
                Call.Skill18, Call.Category, Call.RawThreat, input.Receiver.Template.Telemetry404,
                int.MinValue, int.MinValue, int.MinValue));
        }

        private void RefSource(int delta, string reason, ulong site)
        {
            SafeReference(sourceRefs, delta);
            int old = sourceRefs.Count8;
            sourceRefs = sourceRefs with { Count8 = old + delta };
            events.Add(new RefChange(Ordinal, site, input.Attacker.Binding.ObjectId, old, sourceRefs.Count8, reason));
        }

        private void QueueDeathTimer()
        {
            EvidenceKnown(scheduler.Evidence, "scheduler");
            uint first = ReadTick(TickRole.TimerEntry, 0x140d01b02);
            Write(TraceKind.TimerClosing, scheduler.ClosingIo, scheduler.ClosingIo, 0x140d01b08);
            if (scheduler.ClosingIo != 0)
            {
                events.Add(new TimerRequest(Ordinal, 0x140d01ae0, 2000, 3, false, 0, null, null, first, null, null));
                return;
            }
            SafeReference(receiverRefs, 1);
            Require(receiverRefs.PendingTimers10 < int.MaxValue, "timer", "Pending timer count overflows supported domain.", 0x140d01c03);
            Write(TraceKind.TimerPendingWrite, receiverRefs.PendingTimers10, receiverRefs.PendingTimers10 + 1, 0x140d01c03);
            receiverRefs = receiverRefs with { PendingTimers10 = receiverRefs.PendingTimers10 + 1 };
            int oldRefs = receiverRefs.Count8;
            receiverRefs = receiverRefs with { Count8 = oldRefs + 1 };
            events.Add(new RefChange(Ordinal, 0x140d02cc0, input.Receiver.Binding.ObjectId, oldRefs, receiverRefs.Count8, "timer acquisition"));
            uint second = ReadTick(TickRole.TimerDue, 0x140d01c29);
            uint due = unchecked(second + 2000);
            uint index;
            if (!scheduler.RotationEnabled) index = 0;
            else
            {
                Require(scheduler.QueueCount > 0, "timer", "Timer rotation queue divisor is zero.", 0x140d01c8a);
                Require(scheduler.QueueCount == scheduler.Queues.Count &&
                    scheduler.Queues.All(q => q.Index < scheduler.QueueCount),
                    "timer", "Rotation requires a complete logical queue index domain.", 0x140d01c8a);
                index = unchecked(scheduler.TlsIndex + 1) % scheduler.QueueCount;
            }
            Write(TraceKind.TimerQueueSelect, scheduler.TlsIndex, index, 0x140d01c90);
            var queues = scheduler.Queues.ToArray();
            int selected = Array.FindIndex(queues, q => q.Index == index);
            Require(selected >= 0 && queues.Count(q => q.Index == index) == 1,
                "timer", "Selected logical timer queue is missing/ambiguous.", 0x140d01c8a);
            var heap = queues[selected].Heap.ToList();
            for (int i = 1; i < heap.Count; i++)
                Require(!HeapBefore(heap[(i - 1) / 2].Due, heap[i].Due), "timer", "Input timer heap violates the native comparison.", 0x140d0f110);
            uint sleep = scheduler.PendingSleepMilliseconds;
            if (sleep != 0) Write(TraceKind.TimerSleepRequest, sleep, 0, 0x140d01cde);
            var tuple = new TimerEntry(due, input.Receiver.Binding.ObjectId, 3, 0, receiverRefs.Generation68);
            heap.Add(tuple);
            int child = heap.Count - 1;
            while (child > 0)
            {
                int parent = (child - 1) / 2;
                if (!HeapBefore(heap[parent].Due, tuple.Due)) break;
                heap[child] = heap[parent]; child = parent;
            }
            heap[child] = tuple;
            events.Add(new TimerInserted(Ordinal, 0x140d0f110, index, tuple, ReadOnly(heap)));
            uint oldMinimum = queues[selected].MinimumWake;
            uint minimum = Math.Min(oldMinimum, due);
            queues[selected] = queues[selected] with { MinimumWake = minimum, Heap = ReadOnly(heap) };
            scheduler = scheduler with { TlsIndex = index, PendingSleepMilliseconds = 0,
                AccumulatedDelay = unchecked(scheduler.AccumulatedDelay + 2000),
                EnqueueCount = unchecked(scheduler.EnqueueCount + 1), Queues = ReadOnly(queues) };
            Write(TraceKind.TimerMinWake, oldMinimum, minimum, 0x140d01d6b);
            if (due < scheduler.GlobalNextWake) Write(TraceKind.TimerWakeRequest, due, scheduler.GlobalNextWake, 0x140d01d9c);
            uint last = ReadTick(TickRole.TimerExit, 0x140d01da2);
            if (unchecked(first + 1000) < last) Write(TraceKind.TimerSlowLog, first, last, 0x140d01db4);
            events.Add(new TimerRequest(Ordinal, 0x140d01ae0, 2000, 3, true, due,
                index, tuple, first, second, last));
        }

        private uint ReadTick(TickRole role, ulong site)
        {
            Require(tickIndex < input.Ticks.Count, "clock", "Missing reached original tick receipt.", site);
            Tick tick = input.Ticks[tickIndex++];
            EvidenceKnown(tick.Evidence, "tick receipt");
            Require(tick.Role == role && tick.CallSite == site && tick.Operation == input.Source.Operation && tick.Epoch == input.Source.Epoch,
                "clock", "Wrong original tick role/site/operation/epoch or order.", site);
            consumed.Add(tick); return tick.Value;
        }
        private void Write(TraceKind kind, long before, long after, ulong site) =>
            events.Add(new Trace(Ordinal, site, kind, before, after));
    }

    private static bool HeapBefore(uint parent, uint child) => unchecked((int)(parent - child)) > 0;
    private static bool ManagerKnown(Manager manager)
    {
        EvidenceKnown(manager.Evidence, "manager");
        Require(manager.Presence is ManagerPresence.VerifiedNull or ManagerPresence.Present,
            "manager", "Unknown/unsupported reached current manager presence.", 0x140372aa6);
        Require(manager.Presence == ManagerPresence.Present ? manager.Pointer158 != 0 : manager.Pointer158 == 0,
            "manager", "Inconsistent manager pointer/presence.", 0x140372aa6);
        return manager.Presence == ManagerPresence.Present;
    }
    private static uint Flags(Manager manager) => Known(manager.Flags2b18, "manager flags2b18", 0x14034d445);
    private static void SafeReference(References refs, int delta) =>
        Require(refs.History14 == 0 && refs.Count8 > (delta < 0 ? 1 : 0) && refs.Count8 < int.MaxValue,
            "reference", "Reference destruction/history/overflow is outside the admitted domain.", delta > 0 ? 0x140d02cc0UL : 0x140d02e30UL);

    private static void ValidateEntry(Input i, RetailNpcPhysicalSkillRecordCompletion.OwnerCall? skillProducer = null)
    {
        Require(i.Scope is not null && i.Scope.Attacker == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc &&
            i.Scope.Defender == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
            "scope", "Only two ordinary ownerless NPCs are supported.", 0x140b232b0);
        Require(i.Source.MainSha256 == RetailNpcNormalScope.MainBinarySha256 && i.Source.DatasetSha256 == Dataset && i.Source.RegionContext == Region,
            "identity", "Wrong pinned Main/dataset/region context.", 0);
        EvidenceKnown(i.Source.Evidence, "identity"); Text(i.Source.Operation, "operation"); Text(i.Source.Epoch, "epoch");
        ValidateActor(i.Attacker, i, i.Scope.AttackerNpcTemplateId);
        ValidateActor(i.Receiver, i, i.Scope.DefenderNpcTemplateId);
        Require(i.Attacker.Binding.ObjectId != i.Receiver.Binding.ObjectId, "scope", "Self/aliased actors are unsupported.", 0);
        Require(i.Receiver.State.Hp778 > 0 && i.Receiver.State.MaxHp774 >= i.Receiver.State.Hp778 &&
            i.Receiver.State.Mp790 >= 0 && i.Receiver.State.MaxMp78c >= 0 &&
            (skillProducer is null ? i.Receiver.State.Pending728 == 0 : i.Receiver.State.Pending728 is 0 or 1),
            "life", "Receiver must start alive, HP<=maxHP, nonnegative MP and pending0.", 0x140b232b0);
        // MP can exceed maxMP: ordinary NPC maxMP recompute0 does not prove currentMP0.
        Require(i.Call.RequestedHp >= 0 && i.Call.RequestedMp >= 0 && i.Call.Kind14 is 2 or 9 && (skillProducer is not null || i.Call.Skill18 == 0),
            "invocation", "Signed heal/other kinds/nonzero skill metadata are outside this normal owner.", 0x140372920);
        var c = i.Call.Cell; EvidenceKnown(c.Evidence, "application-cell alias");
        Text(c.RecordIdentity, "record identity"); Text(c.CellIdentity, "cell identity");
        if (skillProducer is null)
        {
            Require(c.Storage == CellStorage.OrdinaryNormalRecordApplicationCell && c.DisjointFromActorOwnedStorage &&
                c.IsOriginalApplicationCell && c.Operation == i.Source.Operation && c.Epoch == i.Source.Epoch &&
                c.OwnershipFunction == 0x14068ed40 && c.OwnershipSourceSha256 == WorldSourceSha256 &&
                (i.Call.Kind14 == 2 ? c.Role == ApplicationCellRole.PrimaryNormalCompletionLocal
                    : c.Role == ApplicationCellRole.RetaliationNormalCompletionLocal),
                "alias", "Original supported record cell must be explicitly disjoint from actor/effect/ref/timer storage.", 0x140372d01);
        }
        else Require(skillProducer.Matches(i), "skill-producer",
            "Actual nonzero skill/header, original bb5370 frame/cell, current actor and preview binding must agree.", 0x140bb5535);
        var infra = i.Infrastructure; EvidenceKnown(infra.Evidence, "infrastructure");
        Require(infra.SerializedImmediateInterval && infra.NoOtherActorWriter && infra.NoInlineGameCallback &&
            infra.NoDeferredCallbackDuringInvocation && infra.NormalAllocationLockTransportTelemetryReturn &&
            infra.FreshDisjointStorage && infra.StableActorAndSocketLifetimes,
            "infrastructure", "Normal-return, lifetime, serialization and no-inline-writer contracts are required.", 0);
        EvidenceKnown(i.World.Evidence, "world"); Text(i.World.Generation, "world generation");
        Require(i.World.Vtable != 0 && i.World.Operation == i.Source.Operation && i.World.Epoch == i.Source.Epoch,
            "world", "Unknown/mismatched world binding or generation.", 0);
        Require(i.Guards.Manager.Generation == i.Receiver.Binding.ManagerGeneration,
            "generation", "Manager generation does not bind the receiver snapshot.", 0);
        Require(i.Attribution.Entries.Select(e => e.Key).Distinct().Count() == i.Attribution.Entries.Count &&
            i.Attribution.Entries.All(e => e.Key == e.SourceId), "history", "Malformed logical attribution map.", 0x1402a72b0);
        Require(i.Scheduler.Queues.Select(q => q.Index).Distinct().Count() == i.Scheduler.Queues.Count,
            "timer", "Duplicate logical timer queues.", 0x140d01ae0);
    }

    private static void ValidateActor(Actor actor, Input i, int templateId)
    {
        ActorBinding b = actor.Binding; EvidenceKnown(b.Evidence, "actor binding");
        Text(b.LifeGeneration, "life generation"); Text(b.ManagerGeneration, "manager generation");
        Require(b.ObjectId != 0 && b.TemplateId == templateId && b.Type144 == 2 && b.Vtable == NpcVtable &&
            b.Operation == i.Source.Operation && b.Epoch == i.Source.Epoch,
            "actor", "Actor/template/vtable/type/operation/generation binding is unsupported.", 0);
        Require(actor.State.Master188c == 0 && actor.State.Related1894 == 0,
            "owner", "Master188c and related1894 independently must both be zero; pointer1850 is not a proxy.", 0);
        Require(actor.State.Killer918 is null or > 0, "life", "Use null for a null killer pointer, not object ID zero.", 0);
        Require(actor.References.PendingTimers10 >= 0, "reference", "Negative pending timer count.", 0);
    }

    private static Input Copy(Input i)
    {
        // Snapshot all nested collections before any interpretation; no caller-owned collection escapes.
        var groups = i.Guards.Manager.Groups.Select(g => g with { Records = ReadOnly(g.Records) }).ToArray();
        var manager = i.Guards.Manager with { Groups = ReadOnly(groups) };
        var queues = i.Scheduler.Queues.Select(q => q with { Heap = ReadOnly(q.Heap) }).ToArray();
        return i with { Guards = i.Guards with { Manager = manager },
            Attribution = i.Attribution with { Entries = ReadOnly(i.Attribution.Entries) },
            Scheduler = i.Scheduler with { Queues = ReadOnly(queues) },
            Transport = i.Transport with { Sockets = ReadOnly(i.Transport.Sockets) }, Ticks = ReadOnly(i.Ticks) };
    }

    private static void Floating(FloatingContract c)
    {
        EvidenceKnown(c.Evidence, "floating-point environment");
        Require(c.RoundToNearestEven && c.MaskedExceptions && c.NormalReturn,
            "floating", "RNE/masked exceptions/normal return must be declared; actual MXCSR is not observed.", 0);
    }
    private static float Normal(float value, string stage, ulong site)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value) & 0x7fffffff;
        Require(bits < 0x7f800000 && (bits == 0 || bits >= 0x00800000), stage,
            "Nonfinite/subnormal intermediate is outside the admitted FP domain.", site);
        return value;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float a, float b) => a * b;
    private static T Known<T>(T? value, string stage, ulong site) where T : struct
    {
        Require(value.HasValue, stage, "Unknown reached raw input.", site); return value.GetValueOrDefault();
    }
    private static void EvidenceKnown(Evidence evidence, string stage)
    {
        Require(evidence is not null && evidence.Kind is FactKind.SourceBoundDeclaration or FactKind.ObservedOriginal,
            stage, "Explicit declaration/original provenance is required.", 0);
        Text(evidence.Receipt, stage);
    }
    private static void Text(string text, string stage) => Require(!string.IsNullOrWhiteSpace(text), stage, "Missing evidence identity.", 0);
    private static void Require([DoesNotReturnIf(false)] bool condition, string stage, string reason, ulong site)
    {
        if (!condition) throw new DomainException(stage, reason, site);
    }
    private sealed class DomainException(string stage, string message, ulong site) : ArgumentException(message)
    { internal string Stage { get; } = stage; internal ulong Site { get; } = site; }
    private static ReadOnlyCollection<T> ReadOnly<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}
