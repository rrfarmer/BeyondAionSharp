using System.Collections.ObjectModel;
using System.Collections.Immutable;
using GI = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifeInputs;
using GG = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserOwnedStatGraph;
using GP = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserGearStatLifePlan;
using System.Diagnostics.CodeAnalysis;
using N = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Inactive complete finite User::GetHitDamage invocation for a distinct ordinary NPC source.
/// Implements the User-bound common core directly, not a fake NPC receiver. Within-tier charge
/// and nondepleting polish are calculated; reached stat/effect/activity expansion rejects the
/// whole pure invocation without publishing a damage prefix. No routing, clocks or callbacks run.
/// </summary>
/// <remarks>
/// Main 140541d20/140372920; phase3/npc-to-user-hp-owner/dependency-closure pins full bodies,
/// 107 actual vtable entries and 58 conditional source fixtures. Native-to-4.8 gear/stat/life
/// integration remains a mandatory separate owner. Runtime evidence and deferred final HP are null.
/// The frozen NPC owner's immutable POD/evidence contracts are reused, never its Evaluate method.
/// </remarks>
internal static class RetailNpcToUserImmediateDamageOwner
{
    internal const ulong UserVtable = 0x141161250;
    internal const string UserSourceSha256 = "66ede361f0f3bb5cfc75d82329eff9274284dcd2e911a410cbf7dbfcf84e4a07";
    internal const string ProposalSha256 = "6a7a7c23ba6dc86d4b8917e0ad0420ea0b7580aeae71577d65004597bb0e6811";
    private static readonly ulong[] WorldBindings =
    [ 0x141242520, 0x141242cc0, 0x1412254f0, 0x141225c18, 0x141227518,
      0x14122bc48, 0x14122a610, 0x14122b1f0, 0x141263180, 0x1412521d8 ];
    private static readonly ulong[] ItemVtables =
    [ 0x141256bc0, 0x141172990, 0x1412169b8, 0x14120d6c0, 0x1411b8548 ];

    internal enum Completion { RejectedBeforeEvaluation, IncompleteReachedProgram, CompleteImmediateUserOwner }
    internal enum Presence { Unknown, VerifiedNull, Present }
    internal sealed record RawUser
    {
        internal required Evidence Evidence { get; init; }
        internal uint? Pose13c { get; init; }
        internal byte? Protection7319 { get; init; }
        internal byte? SignedGuard74f7 { get; init; }
        internal byte? Protection731b { get; init; }
        internal byte? Protection731d { get; init; }
        internal byte? Protection731c { get; init; }
        internal uint? World43a0 { get; init; }
        internal uint? World43a4 { get; init; }
        internal int? Duel1318 { get; init; }
        internal byte? Connected1686 { get; init; }
        internal int? Spectator7bf0 { get; init; }
        internal uint? SpectatorWorld7b80 { get; init; }
        internal uint? DebugMask14276916c { get; init; }
        internal uint? DebugCategory1 { get; init; }
        internal byte? Activity8024 { get; init; }
        internal uint? Gather3b60 { get; init; }
        internal uint? Combine5680 { get; init; }
        internal uint? Combine832c { get; init; }
        internal uint? CombineDue5698 { get; init; }
        internal uint? UseItem41e0 { get; init; }
        internal uint? Gauge56b4 { get; init; }
        internal ulong? Pet7948 { get; init; }
        internal uint? Hotspot7d98 { get; init; }
        internal uint? Invincibility141c { get; init; }
        internal uint? Recall595c { get; init; }
        internal int? VerboseHistory5a10 { get; init; }
        internal int? NativeClass3a78 { get; init; }
        internal uint? Tribe1698 { get; init; }
        internal string? GearGeneration { get; init; }
        internal uint ActivityDue42cc { get; init; }
        internal int FirstDeathKind5b24 { get; init; }
        internal Presence Received270 { get; init; }
        internal uint? ReceivedCounter0c { get; init; }
        internal uint? ReceivedCounter10 { get; init; }
    }
    internal sealed record UserTelemetry(uint Log7254, int Character3a70, string Label70da,
        string Account70bc, int Field584c, int Field79c, Evidence Evidence);
    internal sealed record UserActor(ActorBinding Binding, OwnerState State, References References,
        TelemetryScalars Telemetry, RawUser Raw, UserTelemetry Log);
    internal sealed record CurrentDiagnostics(int? Guard14275eb74, byte? Guard14f563b60,
        uint TimerRegistrationFlags, uint TimerRegistrationId, ulong TimerRegistrationCount, Evidence Evidence);
    internal sealed record ClientTransport(Presence Presence, string? ConnectionIdentity3720, Evidence Evidence);
    internal sealed record SourceWorldBinding(Presence Presence, uint? MapId20, Evidence Evidence);
    internal sealed record FeatureSlot(bool KnownCurrent, string? ItemIdentity, string? ItemGeneration, Evidence Evidence);
    internal sealed record GearItem(string Identity, string Generation, string Operation, string Epoch,
        ulong Vtable, string DefinitionIdentity, uint ObjectId70, ulong Serial110,
        byte? ChargeFeature158, uint? TemplateFlags40,
        byte? DefendBurn165, int? PolishDefendBurn77c, int? Charge300, uint Counter380,
        int? Polish400, uint Counter480, IReadOnlyList<byte> ClassPermit40e,
        byte? Reduction2a2, Evidence Evidence);
    internal sealed record Gear(bool CompleteCurrentRegistries, string Generation,
        IReadOnlyList<FeatureSlot> ChargeSlots, IReadOnlyList<FeatureSlot> PolishSlots,
        IReadOnlyList<int?> ChargeLocks2ac0, int? PolishLock2bc0,
        IReadOnlyList<GearItem> Items, ushort? GlobalPermitFloor1427286e8, Evidence Evidence);
    internal enum OriginalTickRole { CancelCombine, AttributionOuter, AttributionDecay, PostHit, TimerEntry, TimerDue, TimerExit }
    internal sealed record OriginalTick(OriginalTickRole Role, ulong CallSite, uint Value,
        string Operation, string Epoch, Evidence Evidence);
    internal sealed record Input(RetailNpcNormalScope Scope, Identity Source, Actor Attacker,
        UserActor Receiver, N.World World, Invocation Call, Guards Guards, Attribution Attribution,
        Scheduler Scheduler, Transport Transport, ClientTransport Client, FloatingContract Floating,
        Infrastructure Infrastructure, CurrentDiagnostics Diagnostics, Gear? Gear,
        SourceWorldBinding SourceWorld, bool OriginalCellDisjointFromItemStorage, IReadOnlyList<OriginalTick> Ticks);

    internal enum UserStage
    {
        PoseRead, Protection, CoreSkipped, DuelRead, ReceivedCounter, SelfGate, DebugGuard,
        ActivityGuard, HotspotExchange, GearLock, GearUnlock, ChargeRead, ClassPermitRead,
        ChargeMutation, PolishRead, PolishMutation, TribeGetter, World84Base,
        PostHitDueWrite, RecallGuard, GroundGuard, FirstDeathKind, DiagnosticCounter
    }
    internal sealed record UserTrace(int Ordinal, ulong NativeSite, UserStage Kind, long Before,
        long After) : OwnerEvent(Ordinal, NativeSite);
    internal enum Resource { Hp, Mp }
    internal sealed record SelfPacket(int Ordinal, ulong NativeSite, Resource Resource,
        uint UserId, int Current, int Maximum, string ConnectionIdentity) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record DebugRead(int Ordinal, ulong NativeSite, string Reason,
        uint GlobalMask, uint? CategoryMask, bool HearReached) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record StatSync(int Ordinal, ulong NativeSite, byte Selector, uint UserId,
        int Value, TransportEffects Transport) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record ActivityRead(int Ordinal, ulong NativeSite, string Program,
        long Guard, uint? OriginalTick) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record ItemRead(int Ordinal, ulong NativeSite, int Slot, string ItemIdentity,
        string Program, long Before, long After) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record ItemDescription(int Ordinal, ulong NativeSite, string ItemIdentity,
        uint ObjectId70, ulong Serial110, bool Charge, int CurrentPoints,
        int VisualBucket) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record UserDeathTimer(int Ordinal, ulong NativeSite, uint DiagnosticId,
        TimerRequest Request) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record UserDeathTelemetry(int Ordinal, ulong NativeSite, uint EventId,
        UserTelemetry User, uint UserObjectId, uint World43a0, PositionBits UserPosition,
        int Race738, ushort Level1492, int Raw734, int NativeClass3a78,
        int ApplicationCell, int Kind14, int Category, int RawThreat, bool InitialPoseSpecial,
        uint? NpcLogId100, uint? NpcObjectId, uint? NpcMapId20,
        int? NpcX, int? NpcY, int? NpcZ, uint Skill18,
        ReadOnlyCollection<long> NativeTail) : OwnerEvent(Ordinal, NativeSite);
    internal sealed record CalculatedState(OwnerState Receiver, RawUser User,
        References ReceiverReferences, References AttackerReferences, Attribution Attribution,
        Scheduler Scheduler, CurrentDiagnostics Diagnostics, Gear? Gear);
    internal sealed record Failure(string Stage, string Reason, ulong NativeSite, bool ReachedUnsupportedProgram);
    internal sealed record Result(Completion Status, Identity Source, bool? CoreReturn,
        bool? OwnerReturn, int RequestedCell, int? ApplicationCell, int? ActualHpLoss,
        int? ActualMpDecrement, CalculatedState? Calculated, ReadOnlyCollection<OwnerEvent> Events,
        ReadOnlyCollection<OriginalTick> ConsumedTicks, Failure? Failure)
    {
        internal int? FinalHpAfterDeferredLife => null;
        internal int RngDrawsConsumed => 0;
        internal bool ActivationReady => false;
        internal bool OrdinaryEquippedCohortReady => false;
    }

    internal static Result Evaluate(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        try { return new Replay(Copy(input)).Run(); }
        catch (DomainException ex)
        {
            return new(ex.Incomplete ? Completion.IncompleteReachedProgram : Completion.RejectedBeforeEvaluation,
                input.Source, null, null, input.Call.RequestedHp, null, null, null, null,
                ReadOnly<OwnerEvent>([]), ReadOnly<OriginalTick>([]),
                new(ex.Stage, ex.Message, ex.Site, ex.Incomplete));
        }
    }

    /// <summary>One reviewed named cross-version construction recipe. This is a conditional
    /// current-class declaration, not evidence that an equipped account has been observed.</summary>
    internal enum OwnedClassRecipe { Unknown, Gladiator55NativePermit1JavaOrdinal1 }

    /// <summary>Immutable pre-core owned state. SourceRecord and every collection in GI.Input
    /// own immutable storage. There is no public constructor, with-copy or answer dictionary.
    /// Admission pins the source context only; gameplay validation remains lazy at core success.</summary>
    internal sealed class OwnedGearSnapshot
    {
        internal GI.Input Current { get; }
        internal OwnedClassRecipe ClassRecipe { get; }
        internal Evidence ClassEvidence { get; }
        private OwnedGearSnapshot(GI.Input current, OwnedClassRecipe recipe, Evidence evidence)
        { Current = current; ClassRecipe = recipe; ClassEvidence = evidence; }
        internal static OwnedGearSnapshot Admit(GI.Input current, OwnedClassRecipe recipe, Evidence classEvidence)
        {
            ArgumentNullException.ThrowIfNull(current);
            if (current.MainSha256 != RetailNpcNormalScope.MainBinarySha256
                || current.Selected48Recipe != "country99-selected-Java48"
                || current.ClassSourceSha256 != GI.ClassSource)
                throw new ArgumentException("Wrong owned source/selected4.8 context.", nameof(current));
            // No graph, gear, callback or life operation is run here. In particular an unused
            // snapshot with unknown reached-only fields does not force an otherwise skipped child.
            return new(current, recipe, classEvidence);
        }
    }

    /// <summary>Inner ordinals/provenance are preserved. Site0 is an abstract mixed-source
    /// policy operation, never a manufactured native instruction or observed callback.</summary>
    internal sealed record OwnedGearOperation(int Ordinal, GP.Event? Step, GP.Request? Request)
        : OwnerEvent(Ordinal, 0)
    {
        internal string Policy => GI.Policy;
        internal int ChildOrdinal => Step?.Ordinal ?? Request!.Ordinal;
        internal bool ObservedOriginal => false;
    }
    internal sealed record EffectiveMaximumView(int AuditInitialHp, int AuditInitialMp,
        int CurrentHp, int CurrentMp);
    internal sealed record OwnedResult(Result Owner, GP.Candidate? OwnedGear,
        EffectiveMaximumView? EffectiveMaxima, int? CoreActualHpLoss, int? CoreActualMpLoss,
        int? GearHpDelta, int? GearMpDelta, int? TotalImmediateHpLoss, int? TotalImmediateMpLoss)
    {
        internal string Policy => GI.Policy;
        internal bool LeaseConsumed => false;
        internal bool ActivationReady => false;
        internal bool OrdinaryEquippedCohortReady => false;
        internal int? FinalHpAfterDeferredLife => null;
        internal int? ActualRuntimeHp => null;
    }

    /// <summary>Complete finite normal User owner with exactly one owned gear/stat/life child.
    /// Current four-item/source-family admission is an activation blocker, not account coverage.
    /// No supplied post-core life/maxima/callback result is accepted. The old Evaluate is unchanged.</summary>
    internal static OwnedResult EvaluateWithOwnedStatLife(Input input, OwnedGearSnapshot? owned)
    {
        ArgumentNullException.ThrowIfNull(input);
        Replay? replay = null;
        try
        {
            replay = new Replay(Copy(input), owned, true);
            Result result = replay.Run();
            return replay.ComposeResult(result);
        }
        catch (DomainException ex)
        {
            bool incomplete = ex.Incomplete || replay?.OwnedStageReached == true;
            var rejected = new Result(incomplete ? Completion.IncompleteReachedProgram : Completion.RejectedBeforeEvaluation,
                input.Source, null, null, input.Call.RequestedHp, null, null, null, null,
                ReadOnly<OwnerEvent>([]), ReadOnly<OriginalTick>([]),
                new(ex.Stage, ex.Message, ex.Site, incomplete));
            return new(rejected, null, null, null, null, null, null, null, null);
        }
    }

    /// <summary>Defensive current-state copy only; it does not admit a World invocation.</summary>
    internal static Input CopyWorldCurrent(Input current)=>Copy(current);

    /// <summary>Actual normal World context/pending/source entry, produced by the computed
    /// Pass2 owner. No caller-created context08/pending bypass is exposed.</summary>
    internal static OwnedResult EvaluateFromWorld(RetailNpcUserNormalCompletion.HpAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);Replay? replay=null;
        try
        {
            Input current=Copy(admission.Input);
            admission.RequireInput(current);
            replay=new Replay(current,admission.Gear,true,admission);
            Result result=replay.Run();return replay.ComposeResult(result);
        }
        catch(DomainException ex)
        {
            var rejected=new Result(Completion.IncompleteReachedProgram,admission.Input.Source,null,null,
                admission.Input.Call.RequestedHp,null,null,null,null,ReadOnly<OwnerEvent>([]),
                ReadOnly<OriginalTick>([]),new(ex.Stage,ex.Message,ex.Site,true));
            return new(rejected,null,null,null,null,null,null,null,null);
        }
    }

    private static GG.Derived OwnedGraph(GI.Input current, GI.Equipment equipment,
        System.Collections.Immutable.ImmutableArray<GI.Function> registry)
    {
        GI.Item? main = equipment.Items.SingleOrDefault(x => x.EquipmentIndex == 0);
        int delay = 1500;
        if (main is not null)
        {
            GI.Require(main.Source.Key == "item_template-100001286.xml", "unreviewed current main weapon construction");
            var weapon = main.Source.Read().Element("weapon_stats");
            GI.Require(weapon is not null, "missing selected main weapon stats");
            delay = GI.Integer(weapon, "attack_speed");
        }
        return GG.Evaluate(current.ClassId, current.Level, current.ClassSourceSha256, registry,
            equipment.Items.ToImmutableDictionary(x => x.Current.Owner, x => x.Charge, StringComparer.Ordinal),
            current.Motion.Flying, delay, main?.Current.Owner, current.Operation, current.Epoch);
    }

    private static void JoinOwned(Input input, OwnedGearSnapshot snapshot)
    {
        GI.Input current = snapshot.Current;
        EvidenceKnown(snapshot.ClassEvidence, "owned current class recipe");
        Require(snapshot.ClassRecipe == OwnedClassRecipe.Gladiator55NativePermit1JavaOrdinal1
            && current.ClassId == 1 && current.Level == 55
            && input.Receiver.Raw.NativeClass3a78 == 1 && input.Receiver.Telemetry.Level1492 == 55,
            "owned-class", "Unproved current native permit/Java class ordinal/level recipe.", 0);
        Require(current.Operation == input.Source.Operation && current.Epoch == input.Source.Epoch
            && current.MainSha256 == input.Source.MainSha256
            && current.Scope is not null && current.Scope.Attacker == input.Scope.Attacker && current.Scope.Defender == input.Scope.Defender
            && current.Scope.AttackerNpcTemplateId == input.Scope.AttackerNpcTemplateId,
            "owned-context", "Owned current context differs from the normal User invocation.", 0);
        GI.Actor a = current.Attacker, u = current.Receiver;
        a.Current.Validate(current.Operation, current.Epoch); u.Current.Validate(current.Operation, current.Epoch);
        Require(a.ObjectId == input.Attacker.Binding.ObjectId && a.Type == input.Attacker.Binding.Type144
            && a.Vtable == input.Attacker.Binding.Vtable && a.Current.Generation == input.Attacker.Binding.LifeGeneration
            && a.Master188c == input.Attacker.State.Master188c && a.Related1894 == input.Attacker.State.Related1894
            && a.Virtual1850 == GI.Known.Null && input.Attacker.State.Pointer1850 == 0
            && u.ObjectId == input.Receiver.Binding.ObjectId && u.Type == input.Receiver.Binding.Type144
            && u.Vtable == input.Receiver.Binding.Vtable && u.Current.Generation == input.Receiver.Binding.LifeGeneration,
            "owned-actor", "Actual actor identity/life generation/independent ownership join failed.", 0);
        Require(current.Life.Hp == input.Receiver.State.Hp778 && current.Life.Mp == input.Receiver.State.Mp790
            && current.Entry.OriginalLife == u.Current
            && current.Entry.OriginalApplicationCell == input.Call.Cell.CellIdentity,
            "owned-life", "Snapshot must describe the original entry life/cell, never a post-core answer.", 0);
        Gear native = input.Gear ?? throw new DomainException("owned-gear", "Missing reached original gear snapshot.", 0, true);
        Require(native.CompleteCurrentRegistries && native.Generation == current.Equipment.Current.Generation
            && native.Generation == input.Receiver.Raw.GearGeneration
            && native.ChargeSlots.Count == 17 && native.PolishSlots.Count == 17
            && native.ChargeLocks2ac0.Count == 17 && !current.Equipment.ChargeSlots.IsDefault
            && !current.Equipment.PolishSlots.IsDefault && !current.Equipment.ChargeLocks.IsDefault
            && current.Equipment.ChargeSlots.Length == 17 && current.Equipment.PolishSlots.Length == 17
            && current.Equipment.ChargeLocks.Length == 17 && !current.Equipment.Items.IsDefault
            && current.Equipment.Items.Length <= 20
            && native.Items.Count == current.Equipment.Items.Length
            && native.Items.Select(x => x.Identity).Distinct(StringComparer.Ordinal).Count() == native.Items.Count,
            "owned-gear", "Incomplete or mismatched native/4.8 current registries/items.", 0);
        EvidenceKnown(native.Evidence, "owned native gear");
        for (int slot = 0; slot < 17; slot++)
        {
            JoinSlot(native.ChargeSlots[slot], current.Equipment.ChargeSlots[slot]);
            JoinSlot(native.PolishSlots[slot], current.Equipment.PolishSlots[slot]);
            Require(native.ChargeLocks2ac0[slot] == current.Equipment.ChargeLocks[slot],
                "owned-lock", "Charge lock snapshot mismatch.", 0);
        }
        Require(native.PolishLock2bc0 == current.Equipment.PolishLock, "owned-lock", "Polish lock snapshot mismatch.", 0);
        foreach (GI.Item item in current.Equipment.Items)
        {
            GearItem? raw = native.Items.SingleOrDefault(x => x.Identity == item.Current.Owner);
            Require(raw is not null && raw.Generation == item.Current.Generation
                && raw.Operation == current.Operation && raw.Epoch == current.Epoch
                && raw.Vtable == item.NativeVtable && raw.DefinitionIdentity == item.Mapped48Definition
                && raw.ObjectId70 == item.ObjectId70 && raw.Serial110 == item.Serial110
                && raw.Charge300 == item.Charge && raw.Counter380 == item.ChargeCounter
                && raw.Polish400 == item.Polish && raw.Counter480 == item.PolishCounter
                && raw.ChargeFeature158 == item.ChargeFeature158 && raw.TemplateFlags40 == item.TemplateFlags40,
                "owned-item", "Current item identity/serial/point/counter/feature join failed.", 0);
            EvidenceKnown(raw.Evidence, "owned current item");
            if (item.EquipmentIndex < 17)
                Require(item.ChargeFeature158.HasValue && item.TemplateFlags40.HasValue,
                    "owned-feature", "Unknown current feature-registry producer fields.", 0);
            if (item.EquipmentIndex < 17 && item.ChargeFeature158 != 0)
            {
                var improve = item.Source.Read().Element("improve");
                GI.Require(improve is not null, "reached current charge source lacks improve child");
                int burn = GI.Integer(improve, "burn_defend");
                Require(raw.DefendBurn165 == burn, "owned-burn", "Current defend byte differs from selected source recipe.", 0);
                if (burn > 0 && item.Charge > 0)
                {
                    Require(!item.ClassPermits.IsDefault && item.ClassPermits.Length == 17
                        && raw.ClassPermit40e.Count == 17 && raw.ClassPermit40e.SequenceEqual(item.ClassPermits),
                        "owned-permit", "Reached class permit snapshot is absent or incompatible.", 0);
                    if (item.ClassPermits[current.ClassId] != 0)
                    {
                        Require(raw.Reduction2a2.HasValue && raw.Reduction2a2 == item.PermitReduction,
                            "owned-permit", "Reached signed permit reduction mismatch.", 0);
                        if (raw.Reduction2a2 != 0)
                            Require(native.GlobalPermitFloor1427286e8.HasValue
                                && native.GlobalPermitFloor1427286e8 == item.PermitFloor,
                                "owned-permit", "Reached global permit floor mismatch.", 0);
                    }
                }
            }
            if (item.EquipmentIndex == 0 && (item.TemplateFlags40.GetValueOrDefault() & (1u << 18)) != 0)
            {
                var idian = item.Source.Read().Element("idian");
                GI.Require(idian is not null, "reached current polish source lacks idian child");
                Require(raw.PolishDefendBurn77c == GI.Integer(idian, "burn_defend"),
                    "owned-burn", "Current polish defend integer differs from selected source recipe.", 0);
            }
        }
    }
    private static void JoinSlot(FeatureSlot raw, GI.Slot current)
    {
        EvidenceKnown(raw.Evidence, "owned feature slot");
        Require(raw.KnownCurrent && current.Presence is GI.Known.Null or GI.Known.Present
            && raw.ItemIdentity == current.ItemOwner && raw.ItemGeneration == current.Generation,
            "owned-slot", "Actual feature-slot presence/owner/generation mismatch.", 0);
    }

    private sealed class Replay(Input input, OwnedGearSnapshot? owned = null, bool ownedMode = false, RetailNpcUserNormalCompletion.HpAdmission? world = null)
    {
        private readonly List<OwnerEvent> events = [];
        private readonly List<OriginalTick> consumed = [];
        private OwnerState receiver = input.Receiver.State;
        private RawUser user = input.Receiver.Raw;
        private References sourceRefs = input.Attacker.References;
        private References receiverRefs = input.Receiver.References;
        private Attribution history = input.Attribution;
        private Scheduler scheduler = input.Scheduler;
        private CurrentDiagnostics diagnostics = input.Diagnostics;
        private Gear? gear = input.Gear;
        private int cell = input.Call.RequestedHp;
        private int actualMp;
        private int tickIndex;
        private int coreHpLoss, gearHpDelta, gearMpDelta;
        private GP.Candidate? ownedCandidate;
        internal bool OwnedStageReached { get; private set; }
        private EffectiveMaximumView effectiveMaxima = new(input.Receiver.State.MaxHp774, input.Receiver.State.MaxMp78c,
            input.Receiver.State.MaxHp774, input.Receiver.State.MaxMp78c);
        private int Ordinal => events.Count + 1;
        private Invocation Call => input.Call;

        internal Result Run()
        {
            ValidateEntry(input,world);
            uint pose = Known(user.Pose13c, "initial pose13c", 0x140541e81);
            bool poseSpecial = (pose & 15) is 3 or 13 or 14;
            Write(UserStage.PoseRead, pose, poseSpecial ? 1 : 0, 0x140541e81);
            if (ProtectionBlocked())
            {
                Write(UserStage.CoreSkipped, 0, 0, 0x1405428a0);
                return Finish(null, false);
            }
            Write(TraceKind.LastAttackerWrite, receiver.LastAttacker1308, input.Attacker.Binding.ObjectId, 0x140372a7e);
            receiver = receiver with { LastAttacker1308 = input.Attacker.Binding.ObjectId };
            Write(TraceKind.InitialGate, cell, Call.RequestedMp, 0x140372a84);
            bool core = cell > 0 || Call.RequestedMp > 0 ? Core() : false;
            Write(TraceKind.CoreReturn, core ? 1 : 0, core ? 1 : 0, 0x1403731ac);
            coreHpLoss = input.Receiver.State.Hp778 - receiver.Hp778;
            if (core)
            {
                if (ownedMode) { OwnedStageReached = true; ConsumeOwnedGear(); } else ConsumeGear();
                Write(TraceKind.TribeRead, input.World.TribeConflict18468, input.World.TribeConflict18468, 0x1406a44d0);
                Write(UserStage.World84Base, 0, 0, 0x1400b08b0);
                UpdateHistory();
                Inactive(Known(user.VerboseHistory5a10, "history5a10", 0x140541faf) == 0,
                    "history", "Reached verbose attacker-list program.", 0x140541faf);
                uint now = ReadTick(OriginalTickRole.PostHit, 0x140541fda);
                uint due = unchecked(now + 10000);
                Write(UserStage.PostHitDueWrite, user.ActivityDue42cc, due, 0x140541fe0);
                user = user with { ActivityDue42cc = due };
                uint recall = Known(user.Recall595c, "recall595c", 0x1405e6e80);
                Write(UserStage.RecallGuard, recall, 0, 0x1405e6e80);
                Inactive(recall == 0, "recall", "Reached recall cancellation owner.", 0x1405e6e80);
                uint freshPose = Known(user.Pose13c, "posthit pose13c", 0x140541ff8);
                Write(UserStage.GroundGuard, freshPose, freshPose & 0x20f, 0x140541ff8);
                Inactive((freshPose & 15) is 0 or 1 && (freshPose & 0x200) == 0,
                    "pose", "Reached non-admitted mount/glide/pose program.", 0x140541ff8);
            }
            if (receiver.Hp778 < 1) DeathTail(poseSpecial);
            return Finish(core, core);
        }

        private Result Finish(bool? core, bool owner)
        {
            Require(tickIndex == input.Ticks.Count, "clock", "Unconsumed original tick receipt.", 0);
            Write(TraceKind.OwnerReturn, owner ? 1 : 0, owner ? 1 : 0, 0x1405428b4);
            return new(Completion.CompleteImmediateUserOwner, input.Source, core, owner,
                Call.RequestedHp, cell, ownedMode ? coreHpLoss : input.Receiver.State.Hp778 - receiver.Hp778, actualMp,
                new(receiver, user, receiverRefs, sourceRefs, history, scheduler, diagnostics, gear),
                ReadOnly(events), ReadOnly(consumed), null);
        }

        private bool ProtectionBlocked()
        {
            bool Block(long a, ulong site) { Write(UserStage.Protection, a, a != 0 ? 1 : 0, site); return a != 0; }
            byte first = Known(user.Protection7319, "protection7319", 0x140541ea9);
            if (first != 0)
            {
                byte raw = Known(user.SignedGuard74f7, "signed guard74f7", 0x140541eb2);
                if (Block(unchecked((sbyte)raw) > 0 ? 1 : 0, 0x140541eb2)) return true;
            }
            if (Block(Known(user.Protection731b, "protection731b", 0x140541ec6), 0x140541ec6)) return true;
            if (Block(Known(user.Protection731d, "protection731d", 0x140541ed3), 0x140541ed3)) return true;
            if (Block(Known(user.Protection731c, "protection731c", 0x140541ee0), 0x140541ee0)) return true;
            return Block(Known(user.World43a0, "world43a0", 0x140541eed) !=
                Known(user.World43a4, "world43a4", 0x140541eed) ? 1 : 0, 0x140541eed);
        }

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
                    Inactive((abnormal & 0x210) == 0, "break", "Reached original sleep/break RNG program.", 0x140371aa1);
                if ((abnormal & 0x4000008) != 0) RemoveNoEnd(input.Guards.Bound160, 20, 0x140371b92);
                BoundSkill second = input.Guards.Bound210;
                if (secondPointer != 0) RemoveNoEnd(second, 20, 0x140371bc5);
            }
            if (present && cell != 0)
            {
                uint flags = Flags(manager);
                Write(TraceKind.OnGetGuard, flags, flags & 0x300, 0x14034d445);
                Inactive((flags & 0x300) == 0, "on-get", "Reached caster/party protector child program.", 0x14034d445);
            }
            if (Call.Kind14 == 2)
            {
                uint cast = Known(input.Guards.CurrentCast131c, "current cast131c", 0x140371651);
                Write(TraceKind.CastGuard, cast, 0, 0x140371651);
                Inactive(cast == 0, "cast", "Reached active cast cancellation/RNG program.", 0x140371651);
            }
            // The ordinary User binding has type144=1, so the NPC-only full-HP siege branch skips.
            if (receiver.Hp778 == receiver.MaxHp774)
                Write(TraceKind.SiegeGuard, 1, 0, 0x140372b06);
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
            if (cell > 0) ReceivedDamageCounter();
            if (cell > 0 && lethal)
            {
                int duel = Known(user.Duel1318, "User duel1318", 0x1400d1860);
                Write(UserStage.DuelRead, duel, duel != 0 ? 1 : 0, 0x1400d1860);
                Inactive(duel == 0, "duel", "Reached active duel death owner.", 0x1400d1860);
                Write(TraceKind.DeathPolicy, 0, 0, 0x1400d1860);
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
                        Inactive((flags & 0x400) == 0, "when-dead", "Reached death skill/effect program.", 0x14034deb4);
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
                    Inactive((flags & 0x2080) == 0, "pass2", "Reached CaseSkill/CaseHeal child program.", 0x14034da5d);
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
                if (actualMp > 0) { SendMp(); Broadcast(-actualMp, 20, 0xc5, 0, 0); }
            }
            Activity(lethal);
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
            Inactive((actionFlags & 0x10a17) == 0, "action-info", "Reached action-info system skill program.", 0x140372fea);
            // No mask means the native jump also SKIPS the later field72c clear.
            return core;
        }

        private void SystemSkip(uint bit, TraceKind kind, ulong site)
        {
            uint systems = Known(input.Guards.Systems12cc, "system flags12cc", site);
            Write(kind, systems, systems & bit, site);
            Inactive((systems & bit) == 0, "system-effect", "Reached HP/MP threshold system child program.", site);
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
                Inactive(firstLevel is null or 0, "removal", "First matching signed16 level is nonzero; active End is unsupported.", 0x140349ef0);
            }
            events.Add(new NoEnd(Ordinal, site, pointer, group, firstLevel, reason, present));
        }

        private void ReceivedDamageCounter()
        {
            if (Call.Category is not (1 or 2)) return;
            Require(user.Received270 is Presence.VerifiedNull or Presence.Present, "received-counter",
                "Unknown current User bookkeeping270 pointer.", 0x140372920);
            if (user.Received270 == Presence.Present)
            {
                uint old = Known(Call.Category == 1 ? user.ReceivedCounter0c : user.ReceivedCounter10,
                    "received selected counter", 0x140372d3c);
                uint next = unchecked(old + (uint)cell);
                user = Call.Category == 1 ? user with { ReceivedCounter0c = next }
                    : user with { ReceivedCounter10 = next };
                Write(UserStage.ReceivedCounter, old, next, 0x140372d3c);
            }
            // Actual source NPC master188c=0 was independently admitted: no FindUserMark here.
            Write(TraceKind.MasterSkip, input.Attacker.State.Master188c, 0, 0x140372920);
        }

        private void Activity(bool lethal)
        {
            uint pose = Known(user.Pose13c, "activity pose13c", 0x1405c2aa8);
            if (Call.Kind14 != 7 || lethal)
            {
                events.Add(new ActivityRead(Ordinal, 0x1405c2aa8, "CancelLoot", pose & 15, null));
                Inactive((pose & 15) is not (12 or 13), "activity", "Reached loot owner.", 0x1405c2aa8);
            }
            Skip("activity8024", Known(user.Activity8024, "activity8024", 0x1400d0c30), 0x1400d0c30);
            Skip("CancelGather", Known(user.Gather3b60, "gather3b60", 0x14053f2b1), 0x14053f2b1);
            uint combineMode = Known(user.Combine5680, "combine5680", 0x14031a3c8);
            Inactive(combineMode == 0, "activity", "Reached other combine mode.", 0x14031a3c8);
            uint tick = ReadTick(OriginalTickRole.CancelCombine, 0x14031a3d1);
            uint threshold = unchecked(Known(user.CombineDue5698, "combine5698", 0x14031a290) + 1000);
            // Preserve native short circuit and the tick even though832c=0 normally skips work.
            if (tick >= threshold)
                Inactive(Known(user.Combine832c, "combine832c", 0x14031a290) != 1,
                    "activity", "Reached active combine cancellation.", 0x14031a290);
            events.Add(new ActivityRead(Ordinal, 0x14031a3d1, "CancelCombine", threshold, tick));
            Skip("CancelUseItem", Known(user.UseItem41e0, "useitem41e0", 0x14054e9cf), 0x14054e9cf);
            Skip("CancelGaugeTimer", Known(user.Gauge56b4, "gauge56b4", 0x1405c0d42), 0x1405c0d42);
            ulong pet = Known(user.Pet7948, "pet7948", 0x1405f5d40);
            events.Add(new ActivityRead(Ordinal, 0x1405f5d40, "FunctionalPet", unchecked((long)pet), null));
            Inactive(pet == 0, "activity", "Reached functional pet cancellation.", 0x1405f5d40);
            uint hotspot = Known(user.Hotspot7d98, "hotspot7d98", 0x140611340);
            user = user with { Hotspot7d98 = 0 };
            Write(UserStage.HotspotExchange, hotspot, 0, 0x140611340);
            Inactive(hotspot == 0, "activity", "Reached hotspot broadcast child.", 0x140611340);
            uint luna = Known(user.Combine832c, "Luna combine832c", 0x14032a236);
            // CancelLunaCombine(this,0) skips even for mode2 because its second conjunct is false.
            events.Add(new ActivityRead(Ordinal, 0x14032a236, "CancelLunaCombine(param2=0)", luna, null));
            Inactive(luna == 0, "activity", "Nonzero combine832c is outside the approved finite activity state.", 0x14032a236);
            uint invincible = Known(user.Invincibility141c, "invincibility141c", 0x1405c6180);
            events.Add(new ActivityRead(Ordinal, 0x1405c6180, "RemoveInvincibleTime", invincible & 0x40, null));
            Inactive((invincible & 0x40) == 0, "activity", "Reached invincibility removal owner.", 0x1405c6180);
        }

        private void Skip(string program, uint guard, ulong site)
        {
            events.Add(new ActivityRead(Ordinal, site, program, guard, null));
            Inactive(guard == 0, "activity", "Reached active " + program + " owner.", site);
        }

        private void SendHp() => SendResource(Resource.Hp);
        private void SendMp() => SendResource(Resource.Mp);
        private void SendResource(Resource kind)
        {
            byte connected = Known(user.Connected1686, "self connection gate1686", 0x1405306f2);
            Write(UserStage.SelfGate, connected, connected != 0 ? 1 : 0, 0x1405306f2);
            if (connected == 0) return;
            EvidenceKnown(input.Client.Evidence, "client transport");
            Require(input.Client.Presence == Presence.Present && !string.IsNullOrWhiteSpace(input.Client.ConnectionIdentity3720),
                "transport", "Reached self connection must have a stable bound lifetime.", 0x140c9d4e0);
            int current = kind == Resource.Hp ? receiver.Hp778 : receiver.Mp790;
            int maximum = kind == Resource.Hp ? receiver.MaxHp774 : receiver.MaxMp78c;
            events.Add(new SelfPacket(Ordinal, kind == Resource.Hp ? 0x1400c6350UL : 0x1400c63e0UL,
                kind, input.Receiver.Binding.ObjectId, current, maximum, input.Client.ConnectionIdentity3720!));
            Inactive(Known(user.Spectator7bf0, "spectator7bf0", 0x1405305b0) == 0,
                "spectator", "Reached User spectator relay.", 0x1405305b0);
            Inactive(Known(user.SpectatorWorld7b80, "spectator7b80", 0x1405305b0) != input.World.MapId20,
                "spectator", "Reached world spectator relay.", 0x1405305b0);
            if (kind == Resource.Hp) CheckDebug("SendHP");
            events.Add(new StatSync(Ordinal, 0x1400e4190, kind == Resource.Hp ? (byte)1 : (byte)2,
                input.Receiver.Binding.ObjectId, current, ResolveTransport()));
            uint before = receiver.Dirty770;
            receiver = receiver with { Dirty770 = (before & (kind == Resource.Hp ? ~1u : ~2u)) | 0x800 };
            Write(TraceKind.DirtyClear, before, receiver.Dirty770, kind == Resource.Hp ? 0x1405305b0UL : 0x140530a40UL);
        }

        private void CheckDebug(string reason)
        {
            uint global = Known(user.DebugMask14276916c, "debug global mask", 0x14053ebd2);
            uint? category = null;
            if ((global & 2) != 0)
            {
                category = Known(user.DebugCategory1, "debug category1", 0x14053ea90);
                Inactive((category.Value & 1) == 0, "debug", "Reached original Hear debug sender.", 0x14053ec15);
            }
            events.Add(new DebugRead(Ordinal, 0x14053ebd2, reason, global, category, false));
        }

        private void Broadcast(int delta, int type, int log, byte critical) =>
            Broadcast(delta, type, log, critical, input.Attacker.Binding.ObjectId);
        private void Broadcast(int delta, int type, int log, byte critical, uint source)
        {
            bool mp = type is >= 19 and < 24;
            int maximum = mp ? receiver.MaxMp78c : receiver.MaxHp774;
            int current = mp ? receiver.Mp790 : receiver.Hp778;
            byte ratio = current < 1 || maximum < 1 ? (byte)0 : unchecked((byte)((long)current * 99 / maximum + 1));
            events.Add(new WorldHp(Ordinal, 0x140373f10, input.Receiver.Binding.ObjectId,
                source, delta, type, 0, log, critical, ratio, input.Receiver.Telemetry.Position120));
            CheckDebug(mp ? "SendHPChange:MP" : "SendHPChange:HP");
        }

        private TransportEffects ResolveTransport()
        {
            EvidenceKnown(input.Transport.Evidence, "NPC stat transport");
            int slot = (int)(input.Receiver.Binding.ObjectId & 7);
            var endpoints = input.Transport.Sockets.Where(s => s.Slot == slot).ToArray();
            Require(endpoints.Length == 1, "transport", "Missing or ambiguous reached NPC socket.", 0x1404a2d40);
            NpcSocket endpoint = endpoints[0]; EvidenceKnown(endpoint.Evidence, "NPC socket");
            Require(endpoint.Presence is SocketPresence.VerifiedNull or SocketPresence.Present,
                "transport", "Unknown current NPC socket presence.", 0x1404a2d40);
            if (endpoint.Presence == SocketPresence.VerifiedNull)
                return new(slot, 0, TransportDisposition.NoSocketReleased, true, 0, 0, null);
            int refs = Known(endpoint.ReferenceCount8, "socket refs8", 0x140d02cc0);
            int flag = Known(endpoint.History14, "socket history14", 0x140d02cc0);
            Require(refs > 1 && refs < int.MaxValue && flag == 0, "transport", "Unsupported socket lifetime/history.", 0x140d02cc0);
            int state = Known(endpoint.State414, "socket state414", 0x1404bb2a0);
            if (state == 2) return new(slot, 1, TransportDisposition.ClosedReleased, true, 1, 1, refs);
            Require(endpoint.OpenOutcome is OpenSendOutcome.NormalQueuedOrSent or OpenSendOutcome.NormalSendError,
                "transport", "Unknown normal-return transport disposition.", 0x1404bb2a0);
            return new(slot, 1, endpoint.OpenOutcome == OpenSendOutcome.NormalSendError ?
                TransportDisposition.SendError : TransportDisposition.QueuedOrSent, null, 1, 1, null);
        }

        private void ConsumeOwnedGear()
        {
            try
            {
                Inactive(owned is not null, "owned-gear", "Missing reached owned graph/life snapshot.", 0);
                JoinOwned(input, owned);
                GI.Input original = owned.Current;
                Require(original.Equipment.Items.Select(x => x.EquipmentIndex).Distinct().Count() == original.Equipment.Items.Length,
                    "owned-item", "Duplicate current equipment index.", 0);
                Require(original.Equipment.Items.Select(x => x.Current.Owner).Distinct(StringComparer.Ordinal).Count() == original.Equipment.Items.Length,
                    "owned-item", "Duplicate current equipment owner.", 0);
                var registry = GG.Project(original.Graph, original.Operation, original.Epoch);
                GG.Derived before = OwnedGraph(original, original.Equipment, registry);
                Require(before.MaxHp == input.Receiver.State.MaxHp774 && before.MaxMp == input.Receiver.State.MaxMp78c,
                    "owned-maxima", "Pre-stage effective graph is incompatible with the original User maxima.", 0);
                // Only this private callsite creates the post-core input. Caches/killing-blow
                // remain independently sourced from the original owned snapshot.
                GI.Input child = original with { Life = original.Life with { Hp = receiver.Hp778, Mp = receiver.Mp790 } };
                GP.Result result = GP.Evaluate(child);
                if (result.Status != GP.Completion.CompleteFiniteGearStatLifeChild || result.Calculated is not GP.Candidate candidate)
                    throw new DomainException("owned-child", result.Failure ?? "Incomplete reached gear/stat/life program.", 0, true);
                ownedCandidate = candidate;
                GG.Derived after = OwnedGraph(original, candidate.Equipment, candidate.Registry);
                foreach (var operation in candidate.Events.Select(x => (x.Ordinal, Step: (GP.Event?)x, Request: (GP.Request?)null))
                    .Concat(candidate.Requests.Select(x => (x.Ordinal, Step: (GP.Event?)null, Request: (GP.Request?)x)))
                    .OrderBy(x => x.Ordinal))
                    events.Add(new OwnedGearOperation(Ordinal, operation.Step, operation.Request));
                gearHpDelta = candidate.Life.Hp - receiver.Hp778;
                gearMpDelta = candidate.Life.Mp - receiver.Mp790;
                receiver = receiver with { Hp778 = candidate.Life.Hp, Mp790 = candidate.Life.Mp,
                    MaxHp774 = after.MaxHp, MaxMp78c = after.MaxMp };
                effectiveMaxima = new(input.Receiver.State.MaxHp774, input.Receiver.State.MaxMp78c, after.MaxHp, after.MaxMp);
                Gear originalGear = gear ?? throw new DomainException("owned-gear", "Missing original gear snapshot.", 0, true);
                gear = originalGear with { Items = ReadOnly(originalGear.Items.Select(raw =>
                {
                    GI.Item updated = candidate.Equipment.Items.Single(x => x.Current.Owner == raw.Identity);
                    return raw with { Charge300 = updated.Charge, Counter380 = updated.ChargeCounter,
                        Polish400 = updated.Polish, Counter480 = updated.PolishCounter };
                })) };
            }
            catch (GI.Domain ex) { throw new DomainException("owned-child", ex.Message, 0, true); }
            catch (DomainException ex) { throw new DomainException(ex.Stage, ex.Message, ex.Site, true); }
        }

        internal OwnedResult ComposeResult(Result owner) => new(owner, ownedCandidate,
            effectiveMaxima, coreHpLoss, actualMp, gearHpDelta, gearMpDelta,
            input.Receiver.State.Hp778 - receiver.Hp778, input.Receiver.State.Mp790 - receiver.Mp790);

        private void ConsumeGear()
        {
            Require(gear is not null, "gear", "Unknown current feature registries.", 0x1405e29d0);
            EvidenceKnown(gear.Evidence, "feature registries"); Text(gear.Generation, "gear generation");
            Require(gear.CompleteCurrentRegistries && gear.Generation == user.GearGeneration &&
                gear.ChargeSlots.Count == 17 && gear.PolishSlots.Count == 17 && gear.ChargeLocks2ac0.Count == 17,
                "gear", "Both current17registries/locks and receiver-bound generation are required.", 0x1405e29d0);
            Require(gear.Items.Select(i => i.Identity).Distinct().Count() == gear.Items.Count,
                "gear", "Shared aliases require one unambiguous item identity.", 0x1405e29d0);
            foreach (FeatureSlot slot in gear.ChargeSlots.Concat(gear.PolishSlots))
            {
                EvidenceKnown(slot.Evidence, "feature slot");
                Require(slot.KnownCurrent && (slot.ItemIdentity is not null || slot.ItemGeneration is null),
                    "gear", "Unknown or inconsistent feature-slot presence.", 0x1405e29d0);
            }
            for (int index = 0; index < 17; index++)
            {
                int locked = Known(gear.ChargeLocks2ac0[index], "charge lock", 0x1405e2b1e);
                Require(locked == 0, "gear-lock", "Contended lock/interleaving is outside this finite interval.", 0x1405e2b1e);
                Write(UserStage.GearLock, 0, 1, 0x1405e2b1e);
                FeatureSlot slot = gear.ChargeSlots[index];
                if (slot.ItemIdentity is not null)
                {
                    GearItem item = ResolveItem(slot);
                    byte feature = Known(item.ChargeFeature158, "charge feature158", 0x14009ebc0);
                    events.Add(new ItemRead(Ordinal, 0x14009ebc0, index, item.Identity, "charge-feature", feature, feature));
                    if (feature != 0) ConsumeCharge(item, index);
                }
                Write(UserStage.GearUnlock, 1, 0, 0x1405e29d0);
            }
            int polishLock = Known(gear.PolishLock2bc0, "polish lock", 0x1405e333f);
            Require(polishLock == 0, "gear-lock", "Contended polish lock is outside this finite interval.", 0x1405e333f);
            Write(UserStage.GearLock, 0, 1, 0x1405e333f);
            FeatureSlot main = gear.PolishSlots[0]; // Other16entries are not consumed for defense action1.
            if (main.ItemIdentity is not null)
            {
                GearItem item = ResolveItem(main);
                uint flags = Known(item.TemplateFlags40, "polish template40", 0x14009ed90);
                byte feature = (byte)((flags >> 18) & 1); // SHREAX,18; ANDAL,1; caller testsAL.
                events.Add(new ItemRead(Ordinal, 0x14009ed90, 0, item.Identity, "polish-feature", flags, feature));
                if (feature != 0) ConsumePolish(item);
            }
            Write(UserStage.GearUnlock, 1, 0, 0x1405e343a);
        }

        private GearItem ResolveItem(FeatureSlot slot)
        {
            Require(gear is not null, "gear", "Missing current gear.", 0);
            var found = gear.Items.Where(i => i.Identity == slot.ItemIdentity).ToArray();
            Require(found.Length == 1, "gear", "Missing/ambiguous current registry item.", 0x1405e2ba6);
            GearItem item = found[0]; EvidenceKnown(item.Evidence, "current item");
            Text(item.Identity, "item identity"); Text(item.Generation, "item generation"); Text(item.DefinitionIdentity, "item definition");
            Require(slot.ItemGeneration == item.Generation && item.Operation == input.Source.Operation && item.Epoch == input.Source.Epoch,
                "generation", "Stale item/registry/operation/epoch binding.", 0x1405e2ba6);
            Require(ItemVtables.Contains(item.Vtable), "item-vtable", "Unpinned item virtual program.", 0x1405e2ba6);
            return item;
        }

        private void SaveItem(GearItem item)
        {
            Require(gear is not null, "gear", "Missing current gear.", 0);
            gear = gear with { Items = ReadOnly(gear.Items.Select(i => i.Identity == item.Identity ? item : i)) };
        }

        private void ConsumeCharge(GearItem item, int index)
        {
            byte burn = Known(item.DefendBurn165, "defend byte165", 0x14009ec40);
            int points = Known(item.Charge300, "charge300", 0x1403eeada);
            Require(points is >= 0 and <= 1000000, "charge", "Unknown/out-of-range current charge domain.", 0x1403eeada);
            events.Add(new ItemRead(Ordinal, 0x1403eeada, index, item.Identity, "charge-defend-burn", points, burn));
            if (burn == 0 || points == 0) return;
            // Pinned item slot102 returns0. No caller-supplied callback decision is accepted.
            int actual = Math.Min(points, burn), after = points - actual;
            uint counter = unchecked(item.Counter380 + 1);
            GearItem next = item with { Charge300 = after, Counter380 = counter };
            SaveItem(next);
            events.Add(new ItemRead(Ordinal, 0x1403ee9a0, index, item.Identity, "charge300", points, after));
            events.Add(new ItemRead(Ordinal, 0x1403eeb76, index, item.Identity, "counter380", item.Counter380, counter));
            int oldBar = ChargeBucket(points), newBar = ChargeBucket(after);
            int oldTier = (oldBar + 9) / 10, newTier = (newBar + 9) / 10;
            int nativeClass = Known(user.NativeClass3a78, "class3a78", 0x1400d2740);
            Require(nativeClass is >= 0 and < 17 && item.ClassPermit40e.Count == 17,
                "class-permit", "Current class0..16 and full permit array are required.", 0x140099aa0);
            int permit = item.ClassPermit40e[nativeClass];
            // 099bdb..099be5 tests the selected template byte before slot95. Zero skips
            // _AdjustClassPermit entirely, including its reduction/global-floor reads.
            if (permit != 0)
            {
                byte rawReduction = Known(item.Reduction2a2, "signed reduction2a2", 0x140099819);
                if (rawReduction != 0)
                {
                    ushort floor = Known(gear!.GlobalPermitFloor1427286e8, "permit floor", 0x1400996f0);
                    if (permit > floor) permit -= unchecked((sbyte)rawReduction);
                    permit = Math.Max(permit, floor);
                }
            }
            events.Add(new ItemRead(Ordinal, 0x140099aa0, index, item.Identity, "class-permit", nativeClass, permit));
            Inactive(oldTier == newTier, "charge-stat-crossing",
                "Reached ApplyItemChargeBase/4.8stat/life expansion. A fresh maximum or prefix completion is forbidden.", 0x1403ef750);
            events.Add(new ItemRead(Ordinal, 0x1403ef750, index, item.Identity, "equal-tier-return-false", oldTier, newTier));
            if (oldBar != newBar)
                events.Add(new ItemDescription(Ordinal, 0x1405e39c0, item.Identity, item.ObjectId70,
                    item.Serial110, true, after, newBar));
        }

        private void ConsumePolish(GearItem item)
        {
            int burn = Known(item.PolishDefendBurn77c, "signed polish defend77c", 0x14009edb0);
            events.Add(new ItemRead(Ordinal, 0x14009edb0, 0, item.Identity, "polish-defend-burn", burn, burn));
            if (burn <= 0) return;
            int points = Known(item.Polish400, "polish400", 0x1403ef041);
            Require(points is >= 0 and <= 1000000, "polish", "Unknown/out-of-range current polish domain.", 0x1403ef041);
            int actual = Math.Min(points, burn), after = points - actual;
            uint counter = unchecked(item.Counter480 + (uint)actual);
            if (points > 0)
            {
                SaveItem(item with { Polish400 = after, Counter480 = counter });
                events.Add(new ItemRead(Ordinal, 0x1403eef00, 0, item.Identity, "polish400", points, after));
                events.Add(new ItemRead(Ordinal, 0x1403eef00, 0, item.Identity, "counter480", item.Counter480, counter));
                Inactive(after > 0, "polish-stat-depletion",
                    "Reached ResetPolishOption and4.8stat/life owner. No caller-chosen fresh maximum is accepted.", 0x1403ef120);
            }
            // Native tests requested burn against OLD counter, not actual burn or the updated counter.
            if (unchecked((int)(item.Counter480 + (uint)burn)) > 50000)
                events.Add(new ItemDescription(Ordinal, 0x1405e3b90, item.Identity, item.ObjectId70,
                    item.Serial110, false, after, 0));
        }

        private void UpdateHistory()
        {
            EvidenceKnown(history.Evidence, "User attacker history");
            uint outer = ReadTick(OriginalTickRole.AttributionOuter, 0x1402a740c);
            uint tribe = Known(user.Tribe1698, "receiver tribe1698", 0x1400b2fe0);
            Write(UserStage.TribeGetter, tribe, input.Receiver.Telemetry.Race738, 0x1402a72b0);
            bool clear = unchecked(outer - 600000) > history.LastTick68;
            var entries = clear ? new List<AttributionEntry>() : history.Entries.ToList();
            uint source = input.Attacker.Binding.ObjectId;
            int index = entries.FindIndex(e => e.Key == source);
            uint? inner = null; int? decayed = null;
            uint special = input.Attacker.Template.SourceType354 == 2 ? source : history.LastSpecialSource6c;
            AttributionEntry entry;
            if (index < 0)
            {
                uint flags = (input.Attacker.Template.SourceType354 == 2 ? 1u : 0) |
                    (input.Attacker.Template.SourceFlag3ec != 0 ? 0x100u : 0) |
                    (input.Attacker.Template.SourceFlag3ed != 0 ? 0x10000u : 0);
                entry = new(source, source, 0, 0, 0, 2, 5, cell, outer, outer, flags);
                entries.Add(entry);
            }
            else
            {
                inner = ReadTick(OriginalTickRole.AttributionDecay, 0x1402aa365);
                AttributionEntry old = entries[index];
                uint elapsed = unchecked(inner.Value - old.LastTick) / 1000;
                int amount = elapsed >= 120 ? 0 : Math.Min(old.Damage, 0x4fffff);
                decayed = amount;
                entry = old with { Damage = unchecked(amount + cell),
                    FirstTick = amount == 0 ? outer : old.FirstTick, LastTick = outer };
                entries[index] = entry;
            }
            history = history with { LastTick68 = outer, LastSpecialSource6c = special,
                Entries = ReadOnly(entries.OrderBy(e => e.Key)) };
            events.Add(new HistoryUpdate(Ordinal, 0x1402a72b0, clear, outer, inner, decayed, entry));
        }

        private void DeathTail(bool poseSpecial)
        {
            Require(receiver.Pending728 == 1, "death-tail", "Unexpected current pending-death state.", 0x1405420fd);
            // User541d20 snapshots pending728 before the inherited core. The old entry
            // admits only0; the authentic World preview can already have produced1.
            if(input.Receiver.State.Pending728==0)
            {
                Write(UserStage.FirstDeathKind,user.FirstDeathKind5b24,Call.Kind14,0x140542127);
                user=user with {FirstDeathKind5b24=Call.Kind14};
            }
            uint? previous = receiver.Killer918;
            receiver = receiver with { Killer918 = previous ?? input.Attacker.Binding.ObjectId };
            Write(TraceKind.KillerCas, previous ?? 0, receiver.Killer918 ?? 0, 0x14054212f);
            if (previous is null) RefSource(1, "User killer CAS won", 0x14054215c);
            EvidenceKnown(diagnostics.Evidence, "timer diagnostic identity");
            Inactive((diagnostics.TimerRegistrationFlags & 1) != 0, "diagnostic",
                "Reached first-use diagnostic registration owner.", 0x1408843f0);
            Require(diagnostics.TimerRegistrationId is >= 1 and <= 512, "diagnostic",
                "Finite timer token requires an initialized registration ID1..512.", 0x1408843f0);
            ulong before = diagnostics.TimerRegistrationCount;
            diagnostics = diagnostics with { TimerRegistrationCount = unchecked(before + 1) };
            Write(UserStage.DiagnosticCounter, unchecked((long)before),
                unchecked((long)diagnostics.TimerRegistrationCount), 0x1408843f0);
            QueueDeathTimer();
            RefSource(1, "User telemetry temporary retain", 0x1405421a0);
            EvidenceKnown(input.Receiver.Log.Evidence, "User telemetry scalars");
            var log = input.Receiver.Log;
            if(world is null)
            {
                Text(log.Label70da, "User label70da"); Text(log.Account70bc, "User account70bc");
            }
            else log=world.NativeLogAtDeathTail(input);
            int nativeClass = Known(user.NativeClass3a78, "telemetry class3a78", 0x1400d2740);
            const long missing = int.MinValue;
            ReadOnlyCollection<long> firstTail = ReadOnly<long>([cell, Call.Kind14,
                unchecked((uint)log.Field584c), log.Field79c, poseSpecial ? 1 : 0,
                missing, missing, missing, missing, missing, missing]);
            uint worldMap = Known(user.World43a0, "telemetry world43a0", 0x1405421a3);
            events.Add(new UserDeathTelemetry(Ordinal, 0x140542352, 0x8b, log,
                input.Receiver.Binding.ObjectId, worldMap, input.Receiver.Telemetry.Position120,
                input.Receiver.Telemetry.Race738, input.Receiver.Telemetry.Level1492,
                input.Receiver.Telemetry.Raw734, nativeClass, cell, Call.Kind14, Call.Category,
                Call.RawThreat, poseSpecial, null, null, null, null, null, null, Call.Skill18, firstTail));
            EvidenceKnown(input.SourceWorld.Evidence, "source world pointer");
            Require(input.SourceWorld.Presence is Presence.VerifiedNull or Presence.Present,
                "source-world", "Unknown NPC source world pointer.", 0x140542655);
            uint sourceMap = world is not null ? world.NativeSourceWorldAtDeathTail(input) :
                input.SourceWorld.Presence == Presence.VerifiedNull ? 0 :
                Known(input.SourceWorld.MapId20, "source world map20", 0x140542663);
            Floating(input.Floating);
            // Original CVTT order is Z, Y, X. User's own position remains raw f32 arguments.
            PositionBits pos = input.Attacker.Telemetry.Position120;
            int z = Cvtt32(pos.Z, 0x1405426e9), y = Cvtt32(pos.Y, 0x1405426fb), x = Cvtt32(pos.X, 0x140542706);
            ReadOnlyCollection<long> secondTail = ReadOnly<long>([Call.Skill18, Call.Category,
                Call.RawThreat, sourceMap, x, y, z, missing, missing, missing, missing]);
            events.Add(new UserDeathTelemetry(Ordinal, 0x140542857, 0x8a, log,
                input.Receiver.Binding.ObjectId, worldMap, input.Receiver.Telemetry.Position120,
                input.Receiver.Telemetry.Race738, input.Receiver.Telemetry.Level1492,
                input.Receiver.Telemetry.Raw734, nativeClass, cell, Call.Kind14, Call.Category,
                Call.RawThreat, poseSpecial, input.Attacker.Telemetry.LogId100,
                input.Attacker.Binding.ObjectId, sourceMap, x, y, z, Call.Skill18, secondTail));
            RefSource(-1, "User telemetry temporary release", 0x140542882);
        }

        private static int Cvtt32(uint bits, ulong site)
        {
            float value = Normal(BitConverter.UInt32BitsToSingle(bits), "telemetry CVTT32", site);
            Require(value >= -2147483648f && value < 2147483648f, "telemetry",
                "Unrepresentable source-position CVTT32 input.", site);
            return (int)value;
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
            uint first = ReadTick(OriginalTickRole.TimerEntry, 0x140d01b02);
            Write(TraceKind.TimerClosing, scheduler.ClosingIo, scheduler.ClosingIo, 0x140d01b08);
            if (scheduler.ClosingIo != 0)
            {
                EmitTimer(new TimerRequest(Ordinal, 0x140d01ae0, 1, 3, false, 0, null, null, first, null, null));
                return;
            }
            SafeReference(receiverRefs, 1);
            Require(receiverRefs.PendingTimers10 < int.MaxValue, "timer", "Pending timer count overflows supported domain.", 0x140d01c03);
            Write(TraceKind.TimerPendingWrite, receiverRefs.PendingTimers10, receiverRefs.PendingTimers10 + 1, 0x140d01c03);
            receiverRefs = receiverRefs with { PendingTimers10 = receiverRefs.PendingTimers10 + 1 };
            int oldRefs = receiverRefs.Count8;
            receiverRefs = receiverRefs with { Count8 = oldRefs + 1 };
            events.Add(new RefChange(Ordinal, 0x140d02cc0, input.Receiver.Binding.ObjectId, oldRefs, receiverRefs.Count8, "timer acquisition"));
            uint second = ReadTick(OriginalTickRole.TimerDue, 0x140d01c29);
            uint due = unchecked(second + 1);
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
                AccumulatedDelay = unchecked(scheduler.AccumulatedDelay + 1),
                EnqueueCount = unchecked(scheduler.EnqueueCount + 1), Queues = ReadOnly(queues) };
            Write(TraceKind.TimerMinWake, oldMinimum, minimum, 0x140d01d6b);
            if (due < scheduler.GlobalNextWake) Write(TraceKind.TimerWakeRequest, due, scheduler.GlobalNextWake, 0x140d01d9c);
            uint last = ReadTick(OriginalTickRole.TimerExit, 0x140d01da2);
            if (unchecked(first + 1000) < last) Write(TraceKind.TimerSlowLog, first, last, 0x140d01db4);
            EmitTimer(new TimerRequest(Ordinal, 0x140d01ae0, 1, 3, true, due,
                index, tuple, first, second, last));
        }

        private uint ReadTick(OriginalTickRole role, ulong site)
        {
            Require(tickIndex < input.Ticks.Count, "clock", "Missing reached original tick receipt.", site);
            OriginalTick tick = input.Ticks[tickIndex++];
            EvidenceKnown(tick.Evidence, "tick receipt");
            Require(tick.Role == role && tick.CallSite == site && tick.Operation == input.Source.Operation && tick.Epoch == input.Source.Epoch,
                "clock", "Wrong original tick role/site/operation/epoch or order.", site);
            consumed.Add(tick); return tick.Value;
        }
        private void EmitTimer(TimerRequest request) =>
            events.Add(new UserDeathTimer(Ordinal, 0x140542179, diagnostics.TimerRegistrationId, request));
        private void Write(UserStage kind, long before, long after, ulong site) =>
            events.Add(new UserTrace(Ordinal, site, kind, before, after));
        private void Write(TraceKind kind, long before, long after, ulong site) =>
            events.Add(new Trace(Ordinal, site, kind, before, after));
    }

    private static int ChargeBucket(int points) => points <= 0 ? 0 : (points + 49999) / 50000;
    private static bool HeapBefore(uint parent, uint child) => unchecked((int)(parent - child)) > 0;
    private static bool ManagerKnown(Manager manager)
    {
        EvidenceKnown(manager.Evidence, "manager");
        Require(manager.Presence is ManagerPresence.VerifiedNull or ManagerPresence.Present,
            "manager", "Unknown current manager presence.", 0x140372aa6);
        Require(manager.Presence == ManagerPresence.Present ? manager.Pointer158 != 0 : manager.Pointer158 == 0,
            "manager", "Inconsistent current manager pointer/presence.", 0x140372aa6);
        return manager.Presence == ManagerPresence.Present;
    }
    private static uint Flags(Manager manager) => Known(manager.Flags2b18, "manager flags2b18", 0x14034d445);
    private static void SafeReference(References refs, int delta) =>
        Require(refs.History14 == 0 && refs.Count8 > (delta < 0 ? 1 : 0) && refs.Count8 < int.MaxValue,
            "reference", "Reached reference destructor/history/overflow domain.", delta > 0 ? 0x140d02cc0UL : 0x140d02e30UL);

    private static void ValidateEntry(Input i, RetailNpcUserNormalCompletion.HpAdmission? world = null)
    {
        Require(i.Scope is not null && i.Scope.Attacker == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc &&
            i.Scope.Defender == RetailNpcNormalActorKind.Player, "scope", "Only ordinary NPCâ†’User is supported.", 0x140541d20);
        if(world is not null)world.RequireInput(i);
        Require(i.Source.MainSha256 == RetailNpcNormalScope.MainBinarySha256 &&
            (world is null ? i.Source.DatasetSha256 == N.Dataset && i.Source.RegionContext == N.Region :
                i.Source.DatasetSha256 == world.Selected.Context.DatasetId && i.Source.RegionContext == world.Selected.Context.Region),
            "identity", "Wrong pinned Main/dataset/regional context.", 0);
        EvidenceKnown(i.Source.Evidence, "identity"); Text(i.Source.Operation, "operation"); Text(i.Source.Epoch, "epoch");
        var a = i.Attacker.Binding; var d = i.Receiver.Binding;
        ValidateBinding(a, i.Source); ValidateBinding(d, i.Source);
        Require(a.ObjectId != d.ObjectId && a.Type144 == 2 && a.Vtable == N.NpcVtable &&
            a.TemplateId == i.Scope.AttackerNpcTemplateId && d.Type144 == 1 && d.Vtable == UserVtable &&
            d.TemplateId == 0 && i.Scope.DefenderNpcTemplateId == 0,
            "actor", "Wrong actual source/User type, vtable, template or aliased roles.", 0x140541d20);
        Require(i.Attacker.State.Master188c == 0 && i.Attacker.State.Related1894 == 0,
            "owner", "Current188c and1894 independently must be zero;1850 is not a substitute.", 0);
        Require(i.Receiver.State.Hp778 > 0 && i.Receiver.State.MaxHp774 >= i.Receiver.State.Hp778 &&
            i.Receiver.State.Mp790 >= 0 && i.Receiver.State.MaxMp78c >= 0 && (world is null ? i.Receiver.State.Pending728 == 0 : i.Receiver.State.Pending728 is 0 or 1),
            "life", "Finite invocation requires initial alive User, HP<=maxHP, nonnegative MP and pending0.", 0x140541d20);
        Require(i.Receiver.State.Killer918 is null or > 0 && i.Attacker.References.Count8 > 0 &&
            i.Receiver.References.Count8 > 0 && i.Receiver.References.PendingTimers10 >= 0,
            "reference", "Malformed initial pointer/reference/life state.", 0);
        Require(i.Call.RequestedHp >= 0 && i.Call.RequestedMp >= 0 && i.Call.Kind14 is 2 or 9 &&
            i.Call.Skill18 == 0 && i.Call.Context08 == (world is null ? 1 : 0),
            "invocation", "Signed heal, skill metadata or other normal context is unsupported.", 0x140372920);
        var c = i.Call.Cell; EvidenceKnown(c.Evidence, "application-cell");
        Text(c.RecordIdentity, "record identity"); Text(c.CellIdentity, "cell identity");
        Require(c.Storage == CellStorage.OrdinaryNormalRecordApplicationCell && c.DisjointFromActorOwnedStorage &&
            i.OriginalCellDisjointFromItemStorage && c.IsOriginalApplicationCell &&
            c.Operation == i.Source.Operation && c.Epoch == i.Source.Epoch &&
            c.OwnershipFunction == 0x14068ed40 && c.OwnershipSourceSha256 == N.WorldSourceSha256 &&
            (i.Call.Kind14 == 2 ? c.Role == ApplicationCellRole.PrimaryNormalCompletionLocal :
                c.Role == ApplicationCellRole.RetaliationNormalCompletionLocal),
            "alias", "Original record cell must be disjoint from actor/item/effect/ref/timer storage.", 0x140372d01);
        var infra = i.Infrastructure; EvidenceKnown(infra.Evidence, "infrastructure");
        Require(infra.SerializedImmediateInterval && infra.NoOtherActorWriter && infra.NoInlineGameCallback &&
            infra.NoDeferredCallbackDuringInvocation && infra.NormalAllocationLockTransportTelemetryReturn &&
            infra.FreshDisjointStorage && infra.StableActorAndSocketLifetimes,
            "infrastructure", "Explicit normal-return, stable-lifetime, serialized no-inline-writer contract is required.", 0);
        EvidenceKnown(i.Receiver.Raw.Evidence, "current User raw fields");
        EvidenceKnown(i.World.Evidence, "current world"); Text(i.World.Generation, "world generation");
        Require(WorldBindings.Contains(i.World.Vtable) && i.World.Operation == i.Source.Operation && i.World.Epoch == i.Source.Epoch,
            "world", "Unbound world generation or slot44NOP/slot84base program.", 0);
        Require(i.Guards.Manager.Generation == d.ManagerGeneration, "generation", "Wrong receiver manager generation.", 0);
        Require(i.Attribution.Entries.Select(e => e.Key).Distinct().Count() == i.Attribution.Entries.Count &&
            i.Attribution.Entries.All(e => e.Key == e.SourceId), "history", "Malformed complete attribution map.", 0x1402a72b0);
        Require(i.Scheduler.Queues.Select(q => q.Index).Distinct().Count() == i.Scheduler.Queues.Count,
            "timer", "Duplicate logical timer queues.", 0x140d01ae0);
        EvidenceKnown(i.Diagnostics.Evidence, "current instrumentation guards");
        int guard = Known(i.Diagnostics.Guard14275eb74, "native guard14275eb74", 0x140541d20);
        if (guard != 0)
            Inactive(Known(i.Diagnostics.Guard14f563b60, "native guard14f563b60", 0x140541d20) == 0,
                "diagnostic", "Reached native diagnostic instrumentation adds unmodeled original clocks.", 0x140541d20);
    }

    private static void ValidateBinding(ActorBinding b, Identity source)
    {
        EvidenceKnown(b.Evidence, "actor binding"); Text(b.LifeGeneration, "life generation");
        Text(b.ManagerGeneration, "manager generation");
        Require(b.ObjectId != 0 && b.Operation == source.Operation && b.Epoch == source.Epoch,
            "generation", "Unbound actor/operation/lifetime epoch.", 0);
    }

    private static Input Copy(Input i)
    {
        ArgumentNullException.ThrowIfNull(i.Source); ArgumentNullException.ThrowIfNull(i.Call);
        ArgumentNullException.ThrowIfNull(i.Receiver); ArgumentNullException.ThrowIfNull(i.Attacker);
        ArgumentNullException.ThrowIfNull(i.Guards); ArgumentNullException.ThrowIfNull(i.Guards.Manager);
        ArgumentNullException.ThrowIfNull(i.Attribution); ArgumentNullException.ThrowIfNull(i.Scheduler);
        ArgumentNullException.ThrowIfNull(i.Transport); ArgumentNullException.ThrowIfNull(i.Ticks);
        var groups = i.Guards.Manager.Groups.Select(g => g with { Records = ReadOnly(g.Records) });
        var manager = i.Guards.Manager with { Groups = ReadOnly(groups) };
        Gear? gear = i.Gear;
        if (gear is not null)
            gear = gear with { ChargeSlots = ReadOnly(gear.ChargeSlots), PolishSlots = ReadOnly(gear.PolishSlots),
                ChargeLocks2ac0 = ReadOnly(gear.ChargeLocks2ac0),
                Items = ReadOnly(gear.Items.Select(x => x with { ClassPermit40e = ReadOnly(x.ClassPermit40e) })) };
        return i with { Guards = i.Guards with { Manager = manager },
            Attribution = i.Attribution with { Entries = ReadOnly(i.Attribution.Entries) },
            Scheduler = i.Scheduler with { Queues = ReadOnly(i.Scheduler.Queues.Select(q => q with { Heap = ReadOnly(q.Heap) })) },
            Transport = i.Transport with { Sockets = ReadOnly(i.Transport.Sockets) },
            Gear = gear, Ticks = ReadOnly(i.Ticks) };
    }

    private static void Floating(FloatingContract c)
    {
        EvidenceKnown(c.Evidence, "floating environment");
        Require(c.RoundToNearestEven && c.MaskedExceptions && c.NormalReturn,
            "floating", "Conditional RNE/masked-exception/normal-return contract is required; MXCSR was not observed.", 0);
    }
    private static float Normal(float value, string stage, ulong site)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value) & 0x7fffffff;
        Require(bits < 0x7f800000 && (bits == 0 || bits >= 0x00800000), stage,
            "Nonfinite/subnormal consumed value or intermediate.", site);
        return value;
    }
    private static T Known<T>(T? value, string stage, ulong site) where T : struct
    {
        Require(value.HasValue, stage, "Unknown reached current raw field.", site);
        return value.GetValueOrDefault();
    }
    private static void EvidenceKnown(Evidence e, string stage)
    {
        Require(e is not null && e.Kind is FactKind.SourceBoundDeclaration or FactKind.ObservedOriginal,
            stage, "Source-bound declaration or original observation provenance is required.", 0);
        Text(e.Receipt, stage);
    }
    private static void Text(string? value, string stage) => Require(!string.IsNullOrWhiteSpace(value), stage, "Missing identity/evidence.", 0);
    private static void Require([DoesNotReturnIf(false)] bool condition, string stage, string reason, ulong site)
    { if (!condition) throw new DomainException(stage, reason, site, false); }
    private static void Inactive([DoesNotReturnIf(false)] bool condition, string stage, string reason, ulong site)
    { if (!condition) throw new DomainException(stage, reason, site, true); }
    private sealed class DomainException(string stage, string message, ulong site, bool incomplete) : ArgumentException(message)
    {
        internal string Stage => stage;
        internal ulong Site => site;
        internal bool Incomplete => incomplete;
    }
    private static ReadOnlyCollection<T> ReadOnly<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}

