using System.Collections.ObjectModel;
using System.Globalization;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Inactive, serial World normal forced-precheck transition. Calculates comparisons,
/// wrapped counts and mandatory outer writes; suspends for original reads/removal/ticks.
/// Never executes callbacks, getters, RNG, clocks, actor mutation or damage/HP routing.
/// </summary>
/// <remarks>
/// Main 0x140369c90; player 0x14068da3b..db22; NPC 0x14068f6d3..f799.
/// Removal 0x140360920 -> 0x140349ef0 -> 0x140039400 -> 0x1400371c0.
/// Exact identities, independent vectors and boundaries: external phase3/forced-defense-runtime
/// and forced-defense-fixtures. Receipts declare original observations, not a native oracle.
/// Existing normal-record replay's ForcedDefense absence gate remains unchanged.
/// </remarks>
internal static class RetailNpcForcedDefenseTransition
{
    internal const string DatasetSha256 = "33dfd001439620a102ccfca9b30765578efa88acb6f561f8c116ce8d98873335";
    internal const string RegionContext = "generation-a-USA-base;loaded-context-unavailable";
    internal const ulong DodgeTableAddress = 0x145a2c630;
    internal const ulong ParryTableAddress = 0x141122d00;
    internal const ulong CounterHelperAddress = 0x1400b4640;

    internal enum Presence { Unknown, VerifiedAbsent, Present }
    internal enum Receiver { Attacker, Defender }
    internal enum Family { AlwaysHit, AlwaysDodge, AlwaysParry, AlwaysBlock, AlwaysNoResist, AlwaysResist }
    internal enum Stage
    {
        BlindRead, DodgeTableRead, DodgeComparisonRead, BlockComparisonRead, ParryTableRead,
        ParryComparisonRead, ResistComparisonRead, ConsumeRead, BoundSkillRead,
        OriginalRemoval, OriginalTick, Completed, Unsupported
    }
    internal enum Progress { NeedsOriginalRead, NeedsOriginalRemoval, NeedsOriginalTick, Completed, Unsupported }
    internal enum Continuation { Blind, AfterDodge, AfterBlock, NoForce, ChosenTick }

    internal sealed record Identity(string MainSha256, string DatasetSha256, string RegionContext,
        string ActionId, uint AttackerObjectId, uint DefenderObjectId, string AttackerGeneration,
        string DefenderGeneration, string Evidence);
    internal sealed record Binding(Presence Presence, string? SkillKey, int? EffectIdentity, string Evidence);
    internal sealed record FamilyState(Family Family, int? Comparison, int? Uses, Binding Binding);
    /// <summary>
    /// Raw evidence snapshot, not eager native getter evaluation. Null scalar means unknown.
    /// Absent manager has null manager fields; bindings remain a separate channel.
    /// </summary>
    internal sealed record ActorSnapshot(Presence ManagerPresence, string? ManagerIdentity,
        uint? AbnormalFlags1250, int? NativeTableIndex13d0, IReadOnlyList<FamilyState> Families,
        string SnapshotId, string Evidence, uint ObjectId, string ActorGeneration);
    internal sealed record EntrySnapshot(RetailNpcNormalScope Scope, Identity Source,
        ActorSnapshot Attacker, ActorSnapshot Defender, uint? MainPhysicalFlag8e4,
        uint? OffhandPhysicalFlag8e8, bool OriginalNormalEntryGuardsPassed,
        Presence InterveningWrites, string Evidence);
    /// <summary>Ordinal counts modeled original boundaries, not a native clock/counter.</summary>
    internal sealed record Stamp(Identity Source, Receiver Receiver, string ActorGeneration,
        string SnapshotId, int Ordinal, string Evidence);
    internal abstract record OriginalRead(Stamp Stamp);
    internal sealed record BlindRead(Stamp Stamp, uint Flags1250) : OriginalRead(Stamp);
    internal sealed record TableRead(Stamp Stamp, ulong TableAddress, int NativeIndex, int Entry) : OriginalRead(Stamp);
    internal sealed record ComparisonRead(Stamp Stamp, Family Family, Presence ManagerPresence,
        string? ManagerIdentity, int? Comparison) : OriginalRead(Stamp);
    /// <summary>Uses is omitted when comparison is zero: native short-circuits that read.</summary>
    internal sealed record ConsumeRead(Stamp Stamp, Family Family, Presence ManagerPresence,
        string? ManagerIdentity, int? Comparison, int? Uses) : OriginalRead(Stamp);
    /// <summary>Identity is omitted for an absent bound pointer, which native does not dereference.</summary>
    internal sealed record BoundSkillRead(Stamp Stamp, Family Family, Presence Presence,
        string? SkillKey, int? EffectIdentity) : OriginalRead(Stamp);
    internal sealed record OriginalTick(Stamp Stamp, ulong HelperAddress, uint Mask, uint Tick);
    internal sealed record RemovalRequest(Receiver Receiver, Family Family, string SkillKey,
        int EffectIdentity, int Param4, int Reason, int ExpectedOrdinal, string SourceHelper);
    internal sealed record CallbackObservation(int OriginalOrder, string OriginalEvent, string Evidence);
    /// <summary>
    /// Post-state is observed after original removal returns, BEFORE the outer pointer clear.
    /// It cannot supply a chosen status, damage or HP answer. Callback bodies are not replayed.
    /// </summary>
    internal sealed record OriginalRemoval(Stamp Stamp, RemovalRequest Call, int ReturnValue,
        Presence EndCallbackDispatch, IReadOnlyList<CallbackObservation> Callbacks,
        ActorSnapshot AttackerAfter, ActorSnapshot DefenderAfter, bool CompleteOutcomeEvidence,
        string Evidence);
    internal readonly record struct Consumption(int? UsesBefore, int? UsesAfter, bool Decremented, bool Expired);
    internal sealed record Operation(string Kind, Receiver Receiver, Family? Family,
        string Before, string After, string NativeSource, string Evidence);
    internal sealed record ReceiptReference(int Ordinal, string Kind, Receiver Receiver,
        Family? Family, string Evidence);
    /// <summary>Original facts kept separate from independently calculated outer writes.</summary>
    internal sealed record RemovalObservation(int Ordinal, RemovalRequest Call, int ReturnValue,
        Presence EndCallbackDispatch, ReadOnlyCollection<CallbackObservation> Callbacks,
        ActorSnapshot AttackerAfterOriginalReturn, ActorSnapshot DefenderAfterOriginalReturn, string Evidence);
    internal readonly record struct DeadlineWrite(uint Mask, int Offset, uint OriginalTick, uint Deadline);

    internal sealed record Transition
    {
        internal RetailNpcNormalScope Scope { get; init; } = null!;
        internal Identity Source { get; init; } = null!;
        internal ActorSnapshot Attacker { get; init; } = null!;
        internal ActorSnapshot Defender { get; init; } = null!;
        internal bool PhysicalSelector { get; init; }
        internal int LatchedComparison { get; init; }
        internal int MainClass { get; init; }
        internal int OffhandClass { get; init; }
        internal Stage PendingStage { get; init; }
        internal Receiver PendingReceiver { get; init; }
        internal Family? PendingFamily { get; init; }
        internal Continuation AfterConsume { get; init; }
        internal int? ChosenCode { get; init; }
        internal int NextOrdinal { get; init; }
        internal RemovalRequest? Removal { get; init; }
        internal string? UnsupportedReason { get; init; }
        internal ReadOnlyCollection<Operation> Operations { get; init; } = Array.AsReadOnly(Array.Empty<Operation>());
        internal ReadOnlyCollection<ReceiptReference> Receipts { get; init; } = Array.AsReadOnly(Array.Empty<ReceiptReference>());
        internal ReadOnlyCollection<DeadlineWrite> Deadlines { get; init; } = Array.AsReadOnly(Array.Empty<DeadlineWrite>());
        internal ReadOnlyCollection<RemovalObservation> OriginalRemovals { get; init; } = Array.AsReadOnly(Array.Empty<RemovalObservation>());
        internal Progress Status => PendingStage switch
        {
            Stage.OriginalRemoval => Progress.NeedsOriginalRemoval,
            Stage.OriginalTick => Progress.NeedsOriginalTick,
            Stage.Completed => Progress.Completed,
            Stage.Unsupported => Progress.Unsupported,
            _ => Progress.NeedsOriginalRead
        };
    }
    internal sealed record Result(Identity Source, int PrecheckCode, bool PhysicalSelector,
        int LatchedComparison, int MainClass, int OffhandClass, ActorSnapshot Attacker,
        ActorSnapshot Defender, ReadOnlyCollection<Operation> Operations,
        ReadOnlyCollection<ReceiptReference> OriginalReceipts, ReadOnlyCollection<DeadlineWrite> CounterWrites,
        ReadOnlyCollection<RemovalObservation> OriginalRemovals)
    {
        internal bool TerminalBeforeBase => PrecheckCode is 0 or 3;
        internal int? Damage => null;
        internal int? FinalHpAfterCallbacks => null;
        internal string LaterCounterObligation => PrecheckCode switch
        {
            0 or 3 => "World early-miss counter call is separate; preserve its original tick.",
            1 or 2 => "Later active-defense counter call is separate and group-dependent.",
            _ => "No precheck counter was selected; later normal stages remain separate."
        };
    }

    internal static Transition Begin(EntrySnapshot entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.Scope);
        ValidateIdentity(entry.Source);
        Text(entry.Evidence, nameof(entry.Evidence));
        if (!entry.OriginalNormalEntryGuardsPassed || entry.InterveningWrites != Presence.VerifiedAbsent)
            throw new ArgumentException("Original normal guards and serial/no-intervening-write evidence are required.");
        if (entry.MainPhysicalFlag8e4 is null || entry.OffhandPhysicalFlag8e8 is null)
            throw new ArgumentException("Original main/offhand selectors must be known before consumption.");
        var attacker = CopySnapshot(entry.Attacker); var defender = CopySnapshot(entry.Defender);
        ValidateActorIdentity(entry.Source, attacker, Receiver.Attacker);
        ValidateActorIdentity(entry.Source, defender, Receiver.Defender);
        bool physical = entry.MainPhysicalFlag8e4 != 0;
        Family opposing = physical ? Family.AlwaysHit : Family.AlwaysNoResist;
        int a = Comparison(attacker, opposing);
        var state = new Transition
        {
            Scope = entry.Scope, Source = entry.Source, Attacker = attacker, Defender = defender,
            PhysicalSelector = physical, LatchedComparison = a, MainClass = physical ? 1 : 2,
            OffhandClass = entry.OffhandPhysicalFlag8e8 != 0 ? 1 : 2, NextOrdinal = 1,
            PendingStage = Stage.BlindRead, PendingReceiver = Receiver.Attacker
        };
        state = Add(state, "LatchWorldSelectorAndComparison", Receiver.Attacker, opposing,
            "original", a.ToString(CultureInfo.InvariantCulture), "World User68da3b/5b;Npc68f6d3/e3", entry.Evidence);
        return entry.Scope.Attacker == RetailNpcNormalActorKind.Player && a != 0
            ? Consume(state, Receiver.Attacker, opposing, Continuation.Blind) : state;
    }

    /// <summary>No input keeps the explicit pending boundary; wrong/extra input rejects.</summary>
    internal static Transition Advance(Transition state, OriginalRead? read = null, OriginalTick? tick = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (read is not null && tick is not null) throw new ArgumentException("Supply exactly the next original boundary.");
        if (read is null && tick is null) return state;
        if (state.PendingStage is Stage.Completed or Stage.Unsupported or Stage.OriginalRemoval)
            throw new ArgumentException("This transition cannot consume a read/tick at its current boundary.");
        if (tick is not null) return ApplyTick(state, tick);
        if (state.PendingStage == Stage.OriginalTick) throw new ArgumentException("An original counter tick is required.");
        ArgumentNullException.ThrowIfNull(read);
        ValidateStamp(state, read.Stamp);
        var next = Record(state, read.GetType().Name, read.Stamp);
        switch (state.PendingStage)
        {
            case Stage.BlindRead:
                if (read is not BlindRead blind) throw WrongRead();
                if (Current(state).AbnormalFlags1250 is not uint flags || flags != blind.Flags1250)
                    throw new ArgumentException("Blind read must match the current known post-callback flags.");
                if ((flags & 0x20u) != 0)
                    return next with { PendingStage = Stage.Unsupported, UnsupportedReason = "Original blind branch requires separate RNG/status evidence." };
                return SetStage(next, state.PhysicalSelector ? Stage.DodgeTableRead : Stage.ResistComparisonRead,
                    Receiver.Defender, state.PhysicalSelector ? null : Family.AlwaysResist);
            case Stage.DodgeTableRead:
            case Stage.ParryTableRead:
                if (read is not TableRead table) throw WrongRead();
                ulong address = state.PendingStage == Stage.DodgeTableRead ? DodgeTableAddress : ParryTableAddress;
                if (table.TableAddress != address || Current(state).NativeTableIndex13d0 is not int index || table.NativeIndex != index)
                    throw new ArgumentException("Original native table/index must match this current read boundary.");
                if (state.PendingStage == Stage.DodgeTableRead)
                    return table.Entry == 0
                        ? SetStage(next, Stage.DodgeComparisonRead, Receiver.Defender, Family.AlwaysDodge)
                        : SetStage(next, Stage.BlockComparisonRead, Receiver.Defender, Family.AlwaysBlock);
                return table.Entry == 0
                    ? SetStage(next, Stage.ParryComparisonRead, Receiver.Defender, Family.AlwaysParry) : NoForce(next);
            case Stage.DodgeComparisonRead:
            case Stage.BlockComparisonRead:
            case Stage.ParryComparisonRead:
            case Stage.ResistComparisonRead:
                if (read is not ComparisonRead comparison || comparison.Family != state.PendingFamily) throw WrongRead();
                CheckManagerRead(Current(state), comparison.ManagerPresence, comparison.ManagerIdentity, comparison.Comparison);
                CheckComparisonValue(Current(state), comparison.Family, comparison.Comparison);
                int d = Comparison(Current(state), comparison.Family);
                bool chosen = state.LatchedComparison < d;
                if (chosen) next = next with { ChosenCode = Code(comparison.Family) };
                next = Add(next, "SignedStrictComparison", Receiver.Defender, comparison.Family,
                    state.LatchedComparison.ToString(CultureInfo.InvariantCulture), d.ToString(CultureInfo.InvariantCulture),
                    "0x140369c90", comparison.Stamp.Evidence);
                if (chosen || state.LatchedComparison != 0)
                    return Consume(next, Receiver.Defender, comparison.Family,
                        chosen ? Continuation.ChosenTick : AfterFamily(comparison.Family));
                return Continue(next, AfterFamily(comparison.Family));
            case Stage.ConsumeRead:
                if (read is not ConsumeRead consume || consume.Family != state.PendingFamily) throw WrongRead();
                var actor = Current(state);
                CheckManagerRead(actor, consume.ManagerPresence, consume.ManagerIdentity, consume.Comparison);
                CheckComparisonValue(actor, consume.Family, consume.Comparison);
                var family = GetFamily(actor, consume.Family);
                if (consume.ManagerPresence == Presence.Present && consume.Comparison != 0)
                {
                    if (family.Uses is not int uses || consume.Uses != uses)
                        throw new ArgumentException("Required original activation-count read is unknown or stale.");
                }
                else if (consume.Uses is not null)
                    throw new ArgumentException("Native short-circuits the count read for absent manager/zero comparison.");
                var change = ConsumeManagerPair(consume.ManagerPresence, consume.Comparison, consume.Uses);
                if (change.Decremented)
                    next = ReplaceFamily(next, state.PendingReceiver, family with { Uses = change.UsesAfter });
                next = Add(next, change.Decremented ? "WriteWrappedUses" : "ConsumeNoop", state.PendingReceiver,
                    consume.Family, Number(change.UsesBefore), Number(change.UsesAfter), DecrementSource(consume.Family), consume.Stamp.Evidence);
                return change.Expired ? SetStage(next, Stage.BoundSkillRead, state.PendingReceiver, consume.Family)
                    : Continue(next, state.AfterConsume);
            case Stage.BoundSkillRead:
                if (read is not BoundSkillRead bound || bound.Family != state.PendingFamily) throw WrongRead();
                var binding = GetFamily(Current(state), bound.Family).Binding;
                if (binding.Presence == Presence.Unknown || bound.Presence != binding.Presence || bound.SkillKey != binding.SkillKey)
                    throw new ArgumentException("Original bound-skill presence/value must be known and current.");
                if (binding.Presence == Presence.VerifiedAbsent)
                {
                    if (bound.EffectIdentity is not null) throw new ArgumentException("An absent bound pointer skips its identity read.");
                    return Continue(next, state.AfterConsume);
                }
                if (binding.EffectIdentity is not int identity || bound.EffectIdentity != identity)
                    throw new ArgumentException("Original bound effect identity must be known and current.");
                var request = new RemovalRequest(state.PendingReceiver, bound.Family, binding.SkillKey!, identity,
                    1, Reason(bound.Family), next.NextOrdinal, ConsumeSource(bound.Family));
                return next with { PendingStage = Stage.OriginalRemoval, Removal = request };
            default:
                throw WrongRead();
        }
    }

    internal static Transition ResumeRemoval(Transition state, OriginalRemoval original)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(original);
        if (state.PendingStage != Stage.OriginalRemoval || state.Removal is not RemovalRequest request)
            throw new ArgumentException("No original removal is pending.");
        ValidateStamp(state, original.Stamp);
        if (original.Call != request || original.ReturnValue is not (0 or 1) || !original.CompleteOutcomeEvidence)
            throw new ArgumentException("Original removal identity, return and complete outcome evidence are required.");
        Text(original.Evidence, nameof(original.Evidence));
        ArgumentNullException.ThrowIfNull(original.Callbacks);
        if (original.EndCallbackDispatch == Presence.Unknown || !Enum.IsDefined(original.EndCallbackDispatch)
            || (original.EndCallbackDispatch == Presence.VerifiedAbsent) != (original.Callbacks.Count == 0)
            || (original.ReturnValue == 0 && original.EndCallbackDispatch != Presence.VerifiedAbsent))
            throw new ArgumentException("End-callback execution or verified absence must be explicit.");
        int previous = -1;
        foreach (var callback in original.Callbacks)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (callback.OriginalOrder <= previous) throw new ArgumentException("Original callback order must be strictly increasing.");
            Text(callback.OriginalEvent, nameof(callback.OriginalEvent)); Text(callback.Evidence, nameof(callback.Evidence));
            previous = callback.OriginalOrder;
        }
        var attacker = CopySnapshot(original.AttackerAfter); var defender = CopySnapshot(original.DefenderAfter);
        ValidateActorIdentity(state.Source, attacker, Receiver.Attacker);
        ValidateActorIdentity(state.Source, defender, Receiver.Defender);
        RequireCompletePostState(attacker); RequireCompletePostState(defender);
        if (attacker.SnapshotId == state.Attacker.SnapshotId || defender.SnapshotId == state.Defender.SnapshotId)
            throw new ArgumentException("Fresh post-callback snapshots for both actors are required before outer clear.");
        var next = Record(state, "OriginalRemovalReturn", original.Stamp) with
        {
            Attacker = attacker, Defender = defender, Removal = null,
            OriginalRemovals = Array.AsReadOnly(state.OriginalRemovals.Append(new RemovalObservation(
                original.Stamp.Ordinal, request, original.ReturnValue, original.EndCallbackDispatch,
                Array.AsReadOnly(original.Callbacks.ToArray()), attacker, defender, original.Evidence)).ToArray())
        };
        var family = GetFamily(Current(next), request.Family);
        var before = family.Binding;
        // Source writes this pointer unconditionally AFTER return, even result0 or callback rebind.
        next = ReplaceFamily(next, request.Receiver, family with
        { Binding = new(Presence.VerifiedAbsent, null, before.EffectIdentity, original.Evidence) });
        next = Add(next, "ClearBoundPointerAfterOriginalReturn", request.Receiver, request.Family,
            before.SkillKey ?? before.Presence.ToString(), "VerifiedAbsent", request.SourceHelper, original.Evidence);
        return Continue(next, state.AfterConsume);
    }

    internal static Consumption ConsumeManagerPair(Presence manager, int? comparison, int? uses)
    {
        if (manager == Presence.Unknown || !Enum.IsDefined(manager)) throw new ArgumentException("Manager presence is unknown.");
        if (manager == Presence.VerifiedAbsent)
        {
            if (comparison is not null || uses is not null) throw new ArgumentException("Absent manager has no observed pair fields.");
            return new(null, null, false, false);
        }
        if (comparison is null) throw new ArgumentException("Present manager comparison is unknown.");
        if (comparison == 0) return new(uses, uses, false, false);
        if (uses is null) throw new ArgumentException("Nonzero comparison requires the original count.");
        if (uses == 0) return new(0, 0, false, false);
        int value = unchecked(uses.Value - 1);
        return new(uses, value, true, value <= 0);
    }

    internal static Result Finish(Transition state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.PendingStage != Stage.Completed || state.ChosenCode is not int code || code is < 0 or > 4)
            throw new ArgumentException("A complete admitted precheck is required.");
        return new(state.Source, code, state.PhysicalSelector, state.LatchedComparison, state.MainClass,
            state.OffhandClass, state.Attacker, state.Defender, state.Operations, state.Receipts, state.Deadlines, state.OriginalRemovals);
    }

    private static Transition ApplyTick(Transition state, OriginalTick tick)
    {
        if (state.PendingStage != Stage.OriginalTick || state.ChosenCode is not int code || code is < 0 or > 3)
            throw new ArgumentException("No original precheck counter tick is pending.");
        ValidateStamp(state, tick.Stamp);
        (uint mask, int offset) = code switch { 0 => (0x80u, 0x13e4), 1 => (0x40u, 0x13e0),
            2 => (0x20u, 0x13dc), 3 => (0x100u, 0x13e8), _ => throw new ArgumentOutOfRangeException(nameof(code)) };
        if (tick.HelperAddress != CounterHelperAddress || tick.Mask != mask)
            throw new ArgumentException("This receipt is not the selected original precheck counter call.");
        uint deadline = unchecked(tick.Tick + 5000u);
        var next = Record(state, "OriginalCounterTick", tick.Stamp);
        next = Add(next, "WriteCounterDeadline", Receiver.Defender, state.PendingFamily,
            tick.Tick.ToString(CultureInfo.InvariantCulture), deadline.ToString(CultureInfo.InvariantCulture),
            "0x1400b4640", tick.Stamp.Evidence);
        return next with { PendingStage = Stage.Completed,
            Deadlines = Array.AsReadOnly(state.Deadlines.Append(new DeadlineWrite(mask, offset, tick.Tick, deadline)).ToArray()) };
    }
    private static Transition Continue(Transition state, Continuation continuation) => continuation switch
    {
        Continuation.Blind => SetStage(state, Stage.BlindRead, Receiver.Attacker, null),
        Continuation.AfterDodge => SetStage(state, Stage.BlockComparisonRead, Receiver.Defender, Family.AlwaysBlock),
        Continuation.AfterBlock => SetStage(state, Stage.ParryTableRead, Receiver.Defender, null),
        Continuation.NoForce => NoForce(state),
        Continuation.ChosenTick => SetStage(state, Stage.OriginalTick, Receiver.Defender, state.PendingFamily),
        _ => throw new ArgumentOutOfRangeException(nameof(continuation))
    };
    private static Continuation AfterFamily(Family family) => family switch
    {
        Family.AlwaysDodge => Continuation.AfterDodge, Family.AlwaysBlock => Continuation.AfterBlock,
        Family.AlwaysParry or Family.AlwaysResist => Continuation.NoForce,
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };
    private static Transition Consume(Transition state, Receiver receiver, Family family, Continuation after) =>
        SetStage(state, Stage.ConsumeRead, receiver, family) with { AfterConsume = after };
    private static Transition SetStage(Transition state, Stage stage, Receiver receiver, Family? family) =>
        state with { PendingStage = stage, PendingReceiver = receiver, PendingFamily = family };
    private static Transition NoForce(Transition state) => state with { PendingStage = Stage.Completed, ChosenCode = 4, PendingFamily = null };
    private static ActorSnapshot Current(Transition state) => state.PendingReceiver == Receiver.Attacker ? state.Attacker : state.Defender;
    private static FamilyState GetFamily(ActorSnapshot actor, Family family) => actor.Families.Single(f => f.Family == family);
    private static int Comparison(ActorSnapshot actor, Family family)
    {
        if (actor.ManagerPresence == Presence.VerifiedAbsent) return 0;
        if (actor.ManagerPresence != Presence.Present || GetFamily(actor, family).Comparison is not int comparison)
            throw new ArgumentException("The current original comparison is unknown.");
        return comparison;
    }
    private static void CheckManagerRead(ActorSnapshot actor, Presence presence, string? identity, int? comparison)
    {
        if (actor.ManagerPresence == Presence.Unknown || presence != actor.ManagerPresence || identity != actor.ManagerIdentity)
            throw new ArgumentException("Original manager identity/presence must be known and current.");
        if (presence == Presence.VerifiedAbsent && comparison is not null)
            throw new ArgumentException("Native maps known absent manager to zero without reading a comparison field.");
    }
    private static void CheckComparisonValue(ActorSnapshot actor, Family family, int? comparison)
    {
        if (actor.ManagerPresence == Presence.Present
            && (GetFamily(actor, family).Comparison is not int current || comparison != current))
            throw new ArgumentException("Original family comparison is unknown or stale.");
    }
    private static Transition ReplaceFamily(Transition state, Receiver receiver, FamilyState replacement)
    {
        var actor = receiver == Receiver.Attacker ? state.Attacker : state.Defender;
        actor = actor with { Families = Array.AsReadOnly(actor.Families.Select(f => f.Family == replacement.Family ? replacement : f).ToArray()) };
        return receiver == Receiver.Attacker ? state with { Attacker = actor } : state with { Defender = actor };
    }
    private static Transition Add(Transition state, string kind, Receiver receiver, Family? family,
        string before, string after, string source, string evidence) => state with
    { Operations = Array.AsReadOnly(state.Operations.Append(new Operation(kind, receiver, family, before, after, source, evidence)).ToArray()) };
    private static Transition Record(Transition state, string kind, Stamp stamp) => state with
    {
        NextOrdinal = checked(state.NextOrdinal + 1),
        Receipts = Array.AsReadOnly(state.Receipts.Append(new ReceiptReference(stamp.Ordinal, kind,
            stamp.Receiver, state.PendingFamily, stamp.Evidence)).ToArray())
    };
    private static void ValidateStamp(Transition state, Stamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp); ValidateIdentity(stamp.Source); Text(stamp.Evidence, nameof(stamp.Evidence));
        if (!SameIdentity(state.Source, stamp.Source) || stamp.Receiver != state.PendingReceiver || stamp.Ordinal != state.NextOrdinal
            || stamp.SnapshotId != Current(state).SnapshotId
            || stamp.ActorGeneration != (stamp.Receiver == Receiver.Attacker ? state.Source.AttackerGeneration : state.Source.DefenderGeneration))
            throw new ArgumentException("Original receipt scope, receiver, generation, snapshot or read order differs.");
    }
    private static void ValidateIdentity(Identity source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!string.Equals(source.MainSha256, RetailNpcNormalScope.MainBinarySha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.DatasetSha256, DatasetSha256, StringComparison.OrdinalIgnoreCase)
            || source.RegionContext != RegionContext)
            throw new ArgumentException("Unsupported/unverified Main, frozen source dataset or regional context.");
        Text(source.ActionId, nameof(source.ActionId)); Text(source.AttackerGeneration, nameof(source.AttackerGeneration));
        Text(source.DefenderGeneration, nameof(source.DefenderGeneration)); Text(source.Evidence, nameof(source.Evidence));
        if (source.AttackerObjectId == 0 || source.DefenderObjectId == 0 || source.AttackerObjectId == source.DefenderObjectId)
            throw new ArgumentException("Distinct original attacker/defender object identities are required.");
    }
    private static bool SameIdentity(Identity a, Identity b) =>
        string.Equals(a.MainSha256, b.MainSha256, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.DatasetSha256, b.DatasetSha256, StringComparison.OrdinalIgnoreCase)
        && a.RegionContext == b.RegionContext && a.ActionId == b.ActionId
        && a.AttackerObjectId == b.AttackerObjectId && a.DefenderObjectId == b.DefenderObjectId
        && a.AttackerGeneration == b.AttackerGeneration && a.DefenderGeneration == b.DefenderGeneration;
    private static ActorSnapshot CopySnapshot(ActorSnapshot actor)
    {
        ArgumentNullException.ThrowIfNull(actor); Text(actor.SnapshotId, nameof(actor.SnapshotId)); Text(actor.Evidence, nameof(actor.Evidence));
        Text(actor.ActorGeneration, nameof(actor.ActorGeneration));
        if (actor.ObjectId == 0) throw new ArgumentException("An original actor object identity is required.");
        if (!Enum.IsDefined(actor.ManagerPresence)) throw new ArgumentException("Unknown manager presence encoding.");
        if (actor.ManagerPresence == Presence.Present) Text(actor.ManagerIdentity, nameof(actor.ManagerIdentity));
        else if (actor.ManagerIdentity is not null) throw new ArgumentException("Absent/unverified manager cannot supply an identity.");
        ArgumentNullException.ThrowIfNull(actor.Families);
        if (actor.Families.Count != 6 || actor.Families.Any(f => f is null || !Enum.IsDefined(f.Family))
            || actor.Families.Select(f => f.Family).Distinct().Count() != 6)
            throw new ArgumentException("Provide each of the six source family channels exactly once.");
        foreach (var family in actor.Families)
        {
            ArgumentNullException.ThrowIfNull(family.Binding); Text(family.Binding.Evidence, nameof(family.Binding.Evidence));
            if (!Enum.IsDefined(family.Binding.Presence)) throw new ArgumentException("Unknown bound-skill presence encoding.");
            if (family.Binding.Presence == Presence.Present) Text(family.Binding.SkillKey, nameof(family.Binding.SkillKey));
            else if (family.Binding.SkillKey is not null) throw new ArgumentException("Absent/unverified binding cannot supply a skill pointer.");
            if (actor.ManagerPresence != Presence.Present && (family.Comparison is not null || family.Uses is not null))
                throw new ArgumentException("Absent/unverified manager cannot supply observed pair fields.");
        }
        return actor with { Families = Array.AsReadOnly(actor.Families.ToArray()) };
    }
    private static void ValidateActorIdentity(Identity source, ActorSnapshot actor, Receiver receiver)
    {
        if (actor.ObjectId != (receiver == Receiver.Attacker ? source.AttackerObjectId : source.DefenderObjectId)
            || actor.ActorGeneration != (receiver == Receiver.Attacker ? source.AttackerGeneration : source.DefenderGeneration))
            throw new ArgumentException("Actor snapshot belongs to another object/lifetime generation.");
    }
    private static void RequireCompletePostState(ActorSnapshot actor)
    {
        if (actor.ManagerPresence == Presence.Unknown || actor.AbnormalFlags1250 is null || actor.NativeTableIndex13d0 is null
            || actor.Families.Any(f => f.Binding.Presence == Presence.Unknown || f.Binding.EffectIdentity is null
                || (actor.ManagerPresence == Presence.Present && (f.Comparison is null || f.Uses is null))))
            throw new ArgumentException("Required complete raw post-callback state cannot contain unknown modeled fields.");
    }
    private static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "not-read";
    private static void Text(string? text, string name)
    { if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Explicit original/static evidence and identities are required.", name); }
    private static ArgumentException WrongRead() => new("This is not the next original read kind/family.");
    private static int Code(Family family) => family switch
    { Family.AlwaysDodge => 0, Family.AlwaysParry => 1, Family.AlwaysBlock => 2, Family.AlwaysResist => 3, _ => throw new ArgumentOutOfRangeException(nameof(family)) };
    private static int Reason(Family family) => family switch
    { Family.AlwaysHit => 27, Family.AlwaysDodge => 13, Family.AlwaysParry => 14, Family.AlwaysBlock => 15,
        Family.AlwaysNoResist => 24, Family.AlwaysResist => 25, _ => throw new ArgumentOutOfRangeException(nameof(family)) };
    private static string DecrementSource(Family family) => family switch
    { Family.AlwaysHit => "0x140098710", Family.AlwaysDodge => "0x1400987b0", Family.AlwaysParry => "0x140098850",
        Family.AlwaysBlock => "0x1400988f0", Family.AlwaysNoResist => "0x140098990", Family.AlwaysResist => "0x140098a30",
        _ => throw new ArgumentOutOfRangeException(nameof(family)) };
    private static string ConsumeSource(Family family) => family switch
    { Family.AlwaysHit => "0x1400b33a0", Family.AlwaysDodge => "0x1400b3460", Family.AlwaysParry => "0x1400b3520",
        Family.AlwaysBlock => "0x1400b35e0", Family.AlwaysNoResist => "0x1400b3cd0", Family.AlwaysResist => "0x1400b3d90",
        _ => throw new ArgumentOutOfRangeException(nameof(family)) };
}
