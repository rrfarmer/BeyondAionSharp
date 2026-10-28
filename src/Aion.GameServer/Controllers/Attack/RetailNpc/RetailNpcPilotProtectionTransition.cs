using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Inactive pilot activation/end writes and admitted Shield/ordinary Reflector record transition.
/// Suspends for original ordered reads, draws, geometry and whole-skill removal observations.
/// Never calls an actor, RNG, timer, callback or HP owner, and never routes production combat.
/// </summary>
/// <remarks>
/// Main bd95e0/bd7360 activation; 34ca40 consumption; b2ae0 exhaustion; bb5370/68ed40 completion.
/// Pinned static evidence: external phase3/pilot-protection-consumers. Original outcomes unavailable.
/// A record amount, HP application cell and final HP are deliberately different channels.
/// </remarks>
internal static class RetailNpcPilotProtectionTransition
{
    internal const string Dataset = "33dfd001439620a102ccfca9b30765578efa88acb6f561f8c116ce8d98873335";
    internal const string Region = "generation-a-USA-base;loaded-context-unavailable";
    internal enum Presence { Unknown, VerifiedAbsent, PresentUnsupported }
    internal enum ManagerPresence { Unknown, Absent, Present }
    internal enum Actor { Attacker, Defender }
    internal enum CallKind { NormalBase, PhysicalSkill, MagicalSkill }
    internal enum Stage
    {
        InitialFlags, ShieldDraw, ShieldEntry, ShieldBudget, BoundSkill, OriginalRemoval,
        ShieldOutputFlags, MpFlags, ReflectionGate, Radii, MaximumGeometry, MinimumGeometry,
        ReflectionDraw, ReflectionChance, ReflectionValues, OverrideFirst, OverrideSecond,
        LaterFlags, Complete, Unsupported
    }
    internal sealed record Identity(string MainHash, string DatasetHash, string RegionContext,
        string ActionId, uint AttackerId, uint DefenderId, string AttackerLife, string DefenderLife,
        string Evidence);
    internal sealed record Context(RetailNpcNormalScope Scope, Identity Source, string Evidence);
    /// <summary>
    /// Declares original ordered scalar reads and no intervening writer through the grouped
    /// calculation/write block. Entry snapshots cannot substitute for later read intervals.
    /// This declaration is not evidence that a live interval has actually been captured.
    /// </summary>
    internal sealed record Stamp(Identity Source, string ManagerIdentity, string Epoch, int Ordinal,
        string Evidence, bool OriginalOrderedReadInterval);
    internal sealed record ShieldTriple(int Chance, int PerHit, int Mode);
    internal sealed record ReflectorEntry(uint SkillId, int Percent, int Flat, uint MinSquaredBits,
        uint MaxSquaredBits, int Chance);
    internal sealed record Bound(ManagerPresence Presence, string? SkillKey, int? InstanceIdentity, string Evidence);
    /// <summary>Complete raw original poststate, not a selected damage/HP/status answer.</summary>
    internal sealed record ManagerSnapshot(string ManagerIdentity, string Epoch, uint Flags,
        ShieldTriple Class1Shield, ShieldTriple Class2Shield, int Budget,
        ReflectorEntry Class1Reflector, ReflectorEntry Class2Reflector, Bound BoundSkill,
        Presence MpShield, Presence Protect, Presence Convert, uint ObjectId, string Life, string Evidence);
    internal sealed record Entry(Context Context, CallKind Kind, int Classification, int Damage,
        byte OutputFlags, int InitialReflectedAmount, uint InitialReflectedSkill,
        ManagerPresence Manager, string? ManagerIdentity, string? Epoch, int RawSkipLater,
        bool OriginalWrapperAdmitted, bool RoundNearestEvenAndNormalReturn, string Evidence);
    internal abstract record OriginalRead(Stamp Stamp);
    internal sealed record FlagsRead(Stamp Stamp, uint Flags, Presence MpShield,
        Presence Protect, Presence Convert) : OriginalRead(Stamp);
    internal sealed record DrawRead(Stamp Stamp, int Minimum, int Maximum, int Draw) : OriginalRead(Stamp);
    internal sealed record ShieldRead(Stamp Stamp, ShieldTriple Triple) : OriginalRead(Stamp);
    internal sealed record BudgetRead(Stamp Stamp, int Budget) : OriginalRead(Stamp);
    internal sealed record BoundRead(Stamp Stamp, Bound Binding) : OriginalRead(Stamp);
    internal sealed record OutputFlagsRead(Stamp Stamp, byte Flags) : OriginalRead(Stamp);
    internal sealed record GateRead(Stamp Stamp, uint Flags, int? AttackerNpcType) : OriginalRead(Stamp);
    internal sealed record RadiiRead(Stamp Stamp, uint MinimumBits, uint MaximumBits) : OriginalRead(Stamp);
    internal sealed record GeometryRead(Stamp Stamp, uint BoundBits, bool OriginalResult) : OriginalRead(Stamp);
    internal sealed record ChanceRead(Stamp Stamp, int Chance) : OriginalRead(Stamp);
    internal sealed record ValuesRead(Stamp Stamp, int Percent, int Flat, uint SkillId) : OriginalRead(Stamp);
    internal sealed record OverrideRead(Stamp Stamp, int Slot, int OriginalResult) : OriginalRead(Stamp);
    internal sealed record RemovalRequest(string SkillKey, int InstanceIdentity, int Param4, int Reason);
    internal sealed record CallbackObservation(int Order, string Event, string Evidence);
    internal sealed record ActorPostIdentity(uint ObjectId, string Life, RetailNpcNormalActorKind Kind,
        int NpcTemplateId, string Evidence);
    internal sealed record RemovalReceipt(Stamp Stamp, RemovalRequest Call, int OriginalReturn,
        bool CompleteOriginalOutcome, IReadOnlyList<CallbackObservation> Callbacks,
        ManagerSnapshot AfterOriginalReturn, ActorPostIdentity AttackerAfter, ActorPostIdentity DefenderAfter, string Evidence);
    internal sealed record Operation(string Name, string NativeSource, string Detail, string Evidence);
    internal sealed record ReadReference(int Ordinal, Stage Stage, string Epoch, string Evidence);
    internal sealed record RemovalObservation(RemovalRequest Call, int OriginalReturn,
        ReadOnlyCollection<CallbackObservation> Callbacks, ManagerSnapshot AfterOriginalReturn,
        ActorPostIdentity AttackerAfter, ActorPostIdentity DefenderAfter, string Evidence);
    internal sealed record Transition
    {
        internal Entry Input { get; init; } = null!;
        internal string ManagerIdentity { get; init; } = "";
        internal string Epoch { get; init; } = "";
        internal Stage Pending { get; init; }
        internal int NextOrdinal { get; init; } = 1;
        internal int IncomingBeforeShield { get; init; }
        internal int RemainingDamage { get; init; }
        internal int? MpDamage { get; init; }
        internal int? Budget { get; init; }
        internal int Draw { get; init; }
        internal ShieldTriple? Triple { get; init; }
        internal uint MinimumBits { get; init; }
        internal uint MaximumBits { get; init; }
        internal int ReflectedAmount { get; init; }
        internal uint ReflectedSkill { get; init; }
        internal byte OutputFlags { get; init; }
        internal RemovalRequest? Removal { get; init; }
        internal ManagerSnapshot? LastPoststate { get; init; }
        internal string? UnsupportedReason { get; init; }
        internal ReadOnlyCollection<Operation> Operations { get; init; } = Array.AsReadOnly(Array.Empty<Operation>());
        internal ReadOnlyCollection<ReadReference> Reads { get; init; } = Array.AsReadOnly(Array.Empty<ReadReference>());
        internal ReadOnlyCollection<RemovalObservation> Removals { get; init; } = Array.AsReadOnly(Array.Empty<RemovalObservation>());
        internal bool ReachedOriginalRemoval { get; init; }
        internal bool RetainsOriginalSideEffects => ReachedOriginalRemoval;
        internal int? PrimaryApplicationCell => null;
        internal int? FinalHpAfterCallbacks => null;
    }
    internal sealed record Activation(Context Context, Actor Caster, Actor Target, int SkillId,
        int EffectIndex, int SignedLevel, ManagerPresence Manager, uint CurrentFlags,
        uint? OriginalSecondGroupFlags, string SkillRawSpanHash, string SkillKey, int InstanceIdentity,
        bool OriginalDispatcherAdmitted, string Evidence);
    internal sealed record End(Context Context, Actor Target, int SkillId, int EffectIndex,
        ManagerPresence Manager, uint CurrentFlags, uint? OriginalSecondGroupFlags, string SkillRawSpanHash,
        int OriginalReason, bool OriginalDispatcherAdmitted, string Evidence);
    internal sealed record Write(Actor Target, bool ManagerCell, int Offset, int Width, ulong Bits,
        int OrderedGroup, string Evidence);
    internal sealed record WritePlan(ReadOnlyCollection<Write> Writes, int? PreparedHit,
        int? PreparedBudget, int? PreparedPercent, uint? MinimumBits, uint? MaximumBits)
    {
        internal bool AppliedToActor => false;
        internal BoundSkillWrite? BoundSkillWrite { get; init; }
    }

    internal static WritePlan PrepareActivation(Activation input)
    {
        ArgumentNullException.ThrowIfNull(input); Validate(input.Context); Text(input.Evidence); Text(input.SkillKey);
        EnumValue(input.Caster); EnumValue(input.Target);
        if (!input.OriginalDispatcherAdmitted) throw new ArgumentException("Original target/preeffect/dispatcher admission is required.");
        int npc = input.Caster == Actor.Attacker ? input.Context.Scope.AttackerNpcTemplateId : input.Context.Scope.DefenderNpcTemplateId;
        bool shield = input.SkillId == 16522 && input.EffectIndex == 1 && ((npc, input.SignedLevel) is (212777, 22) or (212778, 23) or (212780, 23) or (212876, 24));
        bool reflect = input.SkillId == 16699 && input.EffectIndex == 0 && (npc is 212764 or 212833) && input.SignedLevel == 22;
        if (!shield && !reflect) throw new ArgumentException("Unmatched pilot skill/index/signed16-level/caster binding.");
        ValidateSkillSpan(input.SkillId, input.SkillRawSpanHash);
        if (input.SignedLevel != (short)input.SignedLevel) throw new ArgumentException("Level must be original signed16.");
        Manager(input.Manager);
        var writes = new List<Write>();
        void W(bool manager, int offset, int width, ulong bits, int group) => writes.Add(new(input.Target, manager, offset, width, bits, group, input.Evidence));
        int hit = unchecked(2 * (int)(short)input.SignedLevel + (shield ? 25 : 16));
        if (input.Manager == ManagerPresence.Absent) return Plan(writes, null, null, null, null, null);
        if (shield)
        {
            foreach (int start in new[] { 0x2c88, 0x2c94 })
            { W(true, start, 8, 1000ul | ((ulong)Bits(hit) << 32), 1); W(true, start + 8, 4, 1, 1); }
            W(true, 0x2b18, 4, input.CurrentFlags | 0x40u, 1);
            W(true, 0x2cac, 4, 20000, 1);
            W(false, 0x127c, 4, Bits(input.InstanceIdentity), 2);
            // Pointer value is identified by SkillKey, never fabricated as a numeric address.
            return Plan(writes, hit, 20000, null, null, null) with { BoundSkillWrite = new(input.Target, input.SkillKey, input.InstanceIdentity, input.Evidence) };
        }
        if (input.OriginalSecondGroupFlags is null) throw new ArgumentException("Original second separately locked flag read required.");
        for (int group = 1; group <= 2; group++)
        {
            int start = group == 1 ? 0x2c34 : 0x2c4c;
            uint flags = (group == 1 ? input.CurrentFlags : input.OriginalSecondGroupFlags.Value) | (group == 1 ? 0x10u : 0x20u);
            W(true, 0x2b18, 4, flags, group);
            uint[] fields = { 16699, 25, (uint)hit, 0, 0x42800000, 1000 };
            for (int k = 0; k < fields.Length; k += 2) W(true, start + k * 4, 8, fields[k] | ((ulong)fields[k + 1] << 32), group);
        }
        return Plan(writes, hit, null, 25, 0, 0x42800000);
    }

    internal sealed record BoundSkillWrite(Actor Target, string SkillKey, int InstanceIdentity, string Evidence)
    {
        internal int Offset => 0x188;
        internal int Width => 8;
        internal int OrderAfterSevenScalarWrites => 8;
    }

    internal static WritePlan PrepareEnd(End input)
    {
        ArgumentNullException.ThrowIfNull(input); Validate(input.Context); Text(input.Evidence); EnumValue(input.Target); Manager(input.Manager);
        if (!input.OriginalDispatcherAdmitted || !((input.SkillId == 16522 && input.EffectIndex == 1) || (input.SkillId == 16699 && input.EffectIndex == 0)))
            throw new ArgumentException("Original pilot End dispatch required; no whole-skill callback is invented.");
        ValidateSkillSpan(input.SkillId, input.SkillRawSpanHash);
        var writes = new List<Write>();
        if (input.Manager == ManagerPresence.Absent) return Plan(writes, null, null, null, null, null);
        void W(int offset, uint bits, int group) => writes.Add(new(input.Target, true, offset, 4, bits, group, input.Evidence));
        if (input.SkillId == 16522)
        { W(0x2cac, 0, 1); W(0x2c88, 0, 1); W(0x2c94, 0, 1); W(0x2b18, input.CurrentFlags & ~0x40u, 1); }
        else
        {
            if (input.OriginalSecondGroupFlags is null) throw new ArgumentException("Original second End flag read required.");
            for (int group = 1; group <= 2; group++)
            {
                uint flags = (group == 1 ? input.CurrentFlags : input.OriginalSecondGroupFlags.Value) & ~(group == 1 ? 0x10u : 0x20u); W(0x2b18, flags, group);
                int start = group == 1 ? 0x2c34 : 0x2c4c;
                for (int k = 0; k < 3; k++) writes.Add(new(input.Target, true, start + 8 * k, 8, 0, group, input.Evidence));
            }
        }
        return Plan(writes, null, null, null, null, null);
    }

    internal static Transition Begin(Entry input)
    {
        ArgumentNullException.ThrowIfNull(input); Validate(input.Context); Text(input.Evidence); EnumValue(input.Kind); Manager(input.Manager);
        if (input.Classification is not (1 or 2) || input.Damage < 0 || input.InitialReflectedAmount != 0 || input.InitialReflectedSkill != 0)
            throw new ArgumentException("Known class1/2, nonnegative damage and initialized zero reflection cells required.");
        if ((input.Kind == CallKind.PhysicalSkill && input.Classification != 1) || (input.Kind == CallKind.MagicalSkill && input.Classification != 2))
            throw new ArgumentException("Skill call classification mismatch.");
        if (input.RawSkipLater is not (0 or 1) || !input.OriginalWrapperAdmitted || !input.RoundNearestEvenAndNormalReturn)
            throw new ArgumentException("Original caller/rounding/normal-return evidence required.");
        if (input.Manager == ManagerPresence.Present) { Text(input.ManagerIdentity); Text(input.Epoch); }
        else if (input.ManagerIdentity is not null || input.Epoch is not null) throw new ArgumentException("Absent manager has no invented identity.");
        var state = new Transition { Input = input, ManagerIdentity = input.ManagerIdentity ?? "", Epoch = input.Epoch ?? "",
            IncomingBeforeShield = input.Damage, RemainingDamage = input.Damage, OutputFlags = input.OutputFlags,
            Pending = input.Manager == ManagerPresence.Absent || input.Damage == 0 ? Stage.Complete : Stage.InitialFlags };
        state = Add(state, state.Pending == Stage.Complete ? "WrapperSkipped" : "OriginalManagerEntry", "b40f0", input.Evidence);
        return state.Pending == Stage.Complete ? state : Add(state with { MpDamage = 0 }, "ResetMpOutputBeforeFlagRead", "34cb72", input.Evidence);
    }

    internal static Transition Advance(Transition state, OriginalRead? read = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (read is null) return state;
        if (state.Pending is Stage.Complete or Stage.Unsupported or Stage.OriginalRemoval) throw new ArgumentException("Not a pending read boundary.");
        CheckStamp(state, read.Stamp);
        var next = state with { NextOrdinal = state.NextOrdinal + 1, Reads = Append(state.Reads, new ReadReference(state.NextOrdinal, state.Pending, state.Epoch, read.Stamp.Evidence)) };
        switch (state.Pending)
        {
            case Stage.InitialFlags:
            {
                var r = Need<FlagsRead>(read);
                if (r.Flags == 0) return Set(next, Stage.Complete);
                if ((r.Flags & 0x1000) != 0) return Stop(next, "Whole-skill reflection is outside this ordinary consumer.");
                next = Add(next, "LatchIncomingBeforeShield", "34cbb5", r.Stamp.Evidence);
                return Set(next, (r.Flags & 0x40) != 0 && next.RemainingDamage > 0 ? Stage.ShieldDraw : Stage.MpFlags);
            }
            case Stage.ShieldDraw:
            case Stage.ReflectionDraw:
            {
                var r = Need<DrawRead>(read); if (r.Minimum != 1 || r.Maximum != 1000 || r.Draw is < 1 or > 1000) throw new ArgumentException("Original inclusive1..1000 receipt required.");
                return Set(Add(next with { Draw = r.Draw }, "OriginalDrawConsumed", state.Pending == Stage.ShieldDraw ? "34cbdf" : "34cedc", r.Stamp.Evidence), state.Pending == Stage.ShieldDraw ? Stage.ShieldEntry : Stage.ReflectionChance);
            }
            case Stage.ShieldEntry:
            {
                var r = Need<ShieldRead>(read); ArgumentNullException.ThrowIfNull(r.Triple);
                if (r.Triple.Mode != 1 || r.Triple.PerHit is not (69 or 71 or 73)) return Stop(next, "Unverified nonpilot effective Shield mode/perHit; general arithmetic is separate.");
                return Set(next with { Triple = r.Triple }, state.Draw <= r.Triple.Chance ? Stage.ShieldBudget : Stage.MpFlags);
            }
            case Stage.ShieldBudget:
            {
                var r = Need<BudgetRead>(read);
                if (r.Budget < 0) return Stop(next, "Negative effective Shield budget is outside the admitted domain.");
                int amount;
                try { amount = ShieldAmount(state.RemainingDamage, state.Triple!.PerHit, state.Triple.Mode); }
                catch (ArithmeticException e) { return Stop(next, e.Message); }
                if (amount < 0) return Stop(next, "Negative flat Shield absorption is unsupported.");
                int absorbed = Math.Min(amount, r.Budget);
                next = next with { Budget = r.Budget - absorbed, RemainingDamage = Math.Max(0, unchecked(state.RemainingDamage - absorbed)) };
                next = Add(next, "ShieldHpAndCommonBudgetWrite", "34cc34..34cc5c", r.Stamp.Evidence);
                return Set(next, next.Budget < 1 ? Stage.BoundSkill : Stage.ShieldOutputFlags);
            }
            case Stage.BoundSkill:
            {
                var r = Need<BoundRead>(read); ArgumentNullException.ThrowIfNull(r.Binding);
                if (r.Binding.Presence == ManagerPresence.Unknown) return Stop(next, "Original exhausted Shield bound pointer is unknown.");
                ValidateBound(r.Binding);
                if (r.Binding.Presence == ManagerPresence.Absent) return Set(Add(next, "BoundPointerAbsentNoRemoval", "b2ae0", r.Stamp.Evidence), Stage.ShieldOutputFlags);
                return Set(next with { Removal = new(r.Binding.SkillKey!, r.Binding.InstanceIdentity!.Value, 1, 12) }, Stage.OriginalRemoval);
            }
            case Stage.ShieldOutputFlags:
            {
                var r = Need<OutputFlagsRead>(read);
                return Set(Add(next with { OutputFlags = (byte)(r.Flags | 2) }, "OrShieldFlagEvenZeroAbsorption", "34cc77", r.Stamp.Evidence), Stage.MpFlags);
            }
            case Stage.MpFlags:
            {
                var r = Need<FlagsRead>(read);
                if ((r.Flags & 0x1000) != 0) return Stop(next, "Whole-skill reflection introduced after Shield is unsupported.");
                if (next.RemainingDamage > 0 && ((r.Flags & 0x800) != 0 || r.MpShield != Presence.VerifiedAbsent)) return Stop(next, "MPShield present or unknown at original post-Shield read.");
                return Set(next, state.Input.RawSkipLater != 0 ? Stage.Complete : Stage.ReflectionGate);
            }
            case Stage.ReflectionGate:
            {
                var r = Need<GateRead>(read); uint mask = state.Input.Classification == 1 ? 0x10u : 0x20u;
                if ((r.Flags & mask) == 0) return Later(next);
                next = Add(next, "PinnedSlot28ConstantOne", "b1940;34ce60", r.Stamp.Evidence);
                if (state.Input.Context.Scope.Attacker == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc)
                {
                    if (r.AttackerNpcType is null) return Stop(next, "Original NPC raw type is unknown.");
                    if (r.AttackerNpcType == 0x40) return Later(next);
                }
                else if (r.AttackerNpcType is not null) throw new ArgumentException("Player has no invented NPC raw type.");
                return Set(next, Stage.Radii);
            }
            case Stage.Radii:
            {
                var r = Need<RadiiRead>(read);
                if (!Radius(r.MinimumBits) || !Radius(r.MaximumBits)) return Stop(next, "Malformed or nonfinite squared-radius domain.");
                if (r.MinimumBits != 0 || r.MaximumBits != 0x42800000) return Stop(next, "Unverified nonpilot effective radii.");
                return Set(next with { MinimumBits = r.MinimumBits, MaximumBits = r.MaximumBits }, Stage.MaximumGeometry);
            }
            case Stage.MaximumGeometry:
            case Stage.MinimumGeometry:
            {
                var r = Need<GeometryRead>(read); uint expected = state.Pending == Stage.MaximumGeometry ? state.MaximumBits : state.MinimumBits;
                if (r.BoundBits != expected) throw new ArgumentException("Original geometry call bound/order mismatch.");
                next = Add(next, "OriginalGeometryPredicate", state.Pending == Stage.MaximumGeometry ? "34ceab" : "34cec7", r.Stamp.Evidence);
                return state.Pending == Stage.MaximumGeometry ? r.OriginalResult ? Set(next, Stage.MinimumGeometry) : Later(next)
                    : r.OriginalResult ? Later(next) : Set(next, Stage.ReflectionDraw);
            }
            case Stage.ReflectionChance:
            {
                var r = Need<ChanceRead>(read); return state.Draw <= r.Chance ? Set(next, Stage.ReflectionValues) : Later(next);
            }
            case Stage.ReflectionValues:
            {
                var r = Need<ValuesRead>(read); int amount;
                if (r.Percent != 25 || r.Flat != 60 || r.SkillId != 16699) return Stop(next, "Unverified nonpilot effective Reflector values/identity.");
                try { amount = ReflectionAmount(state.IncomingBeforeShield, r.Percent, r.Flat); }
                catch (ArithmeticException e) { return Stop(next, e.Message); }
                if (amount <= 0) return Later(next);
                next = Add(next with { ReflectedAmount = amount, ReflectedSkill = r.SkillId, OutputFlags = (byte)(state.OutputFlags | 1) }, "ReflectionAmountMetadataFlag", "34ceee..34cf35", r.Stamp.Evidence);
                next = Add(next, "PinnedSlot28ConstantOneAfterReflection", "34cf3c", r.Stamp.Evidence);
                return Set(next, Stage.OverrideFirst);
            }
            case Stage.OverrideFirst:
            case Stage.OverrideSecond:
            {
                var r = Need<OverrideRead>(read); if (r.Slot != 188) throw new ArgumentException("Both class1/2 require original attacker slot188.");
                if (state.Input.Context.Scope.Attacker == RetailNpcNormalActorKind.Player && r.OriginalResult != 0) throw new ArgumentException("Pinned player slot188 returns zero.");
                next = Add(next, "OriginalAttackerSlot188Read", "34cf67..34cf8c", r.Stamp.Evidence);
                if (state.Pending == Stage.OverrideFirst) return r.OriginalResult == 0 ? Later(next) : Set(next, Stage.OverrideSecond);
                return Later(next with { ReflectedAmount = r.OriginalResult });
            }
            case Stage.LaterFlags:
            {
                var r = Need<FlagsRead>(read); uint pm = state.Input.Classification == 1 ? 1u : 2u, cm = state.Input.Classification == 1 ? 4u : 8u;
                return (r.Flags & (pm | cm)) != 0 || r.Protect != Presence.VerifiedAbsent || r.Convert != Presence.VerifiedAbsent
                    ? Stop(next, "Protect/Convert present or unknown at original later read.") : Set(next, Stage.Complete);
            }
            default: throw new ArgumentException("Unsupported read stage.");
        }
    }

    internal static Transition ResumeRemoval(Transition state, RemovalReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(receipt);
        if (state.Pending != Stage.OriginalRemoval || state.Removal is null || receipt.Call != state.Removal) throw new ArgumentException("Wrong original removal boundary.");
        CheckStamp(state, receipt.Stamp); Text(receipt.Evidence);
        if (!receipt.CompleteOriginalOutcome)
            return Stop(Add(state with { ReachedOriginalRemoval = true, NextOrdinal = state.NextOrdinal + 1,
                Reads = Append(state.Reads, new ReadReference(state.NextOrdinal, Stage.OriginalRemoval, state.Epoch, receipt.Stamp.Evidence)) },
                "OriginalRemovalOutcomeUnavailable", "b2ae0", receipt.Evidence), "Reached original removal lacks complete callback/poststate evidence.");
        ArgumentNullException.ThrowIfNull(receipt.Callbacks); ArgumentNullException.ThrowIfNull(receipt.AfterOriginalReturn);
        ArgumentNullException.ThrowIfNull(receipt.AttackerAfter); ArgumentNullException.ThrowIfNull(receipt.DefenderAfter);
        Text(receipt.AttackerAfter.Evidence); Text(receipt.DefenderAfter.Evidence);
        var callbacks = receipt.Callbacks.Select(x => { ArgumentNullException.ThrowIfNull(x); Text(x.Event); Text(x.Evidence); return x with { }; }).ToArray();
        if (callbacks.Where((x, i) => x.Order != i + 1).Any()) throw new ArgumentException("Original callback order must be explicit and contiguous.");
        var post = receipt.AfterOriginalReturn with { }; ValidatePost(post);
        var next = state with { NextOrdinal = state.NextOrdinal + 1, Removal = null,
            ReachedOriginalRemoval = true,
            LastPoststate = post, Removals = Append(state.Removals, new RemovalObservation(receipt.Call, receipt.OriginalReturn, Array.AsReadOnly(callbacks), post,
                receipt.AttackerAfter with { }, receipt.DefenderAfter with { }, receipt.Evidence)),
            Reads = Append(state.Reads, new ReadReference(state.NextOrdinal, Stage.OriginalRemoval, state.Epoch, receipt.Stamp.Evidence)) };
        next = Add(next, "OriginalWholeSkillRemovalReturnedNoOuterClear", "b2ae0;360920..0371c0", receipt.Evidence);
        var source = state.Input.Context.Source; var scope = state.Input.Context.Scope;
        if (receipt.AttackerAfter.ObjectId != source.AttackerId || receipt.AttackerAfter.Life != source.AttackerLife || receipt.AttackerAfter.Kind != scope.Attacker || receipt.AttackerAfter.NpcTemplateId != scope.AttackerNpcTemplateId ||
            receipt.DefenderAfter.ObjectId != source.DefenderId || receipt.DefenderAfter.Life != source.DefenderLife || receipt.DefenderAfter.Kind != scope.Defender || receipt.DefenderAfter.NpcTemplateId != scope.DefenderNpcTemplateId)
            return Stop(next, "Original callback changed or left unknown actor identity/life/domain; reached outcomes retained.");
        if (post.ManagerIdentity != state.ManagerIdentity || post.ObjectId != state.Input.Context.Source.DefenderId || post.Life != state.Input.Context.Source.DefenderLife)
            return Stop(next, "Original callback changed cached manager/object/life identity; reached outcomes retained.");
        if (post.BoundSkill.Presence == ManagerPresence.Unknown)
            return Stop(next, "Original post-removal bound state is unknown; reached outcomes retained.");
        return Set(next with { Epoch = post.Epoch }, Stage.ShieldOutputFlags);
    }

    internal static int ShieldAmount(int damage, int perHit, int mode)
    {
        if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
        if (mode == 0) return Math.Min(damage, perHit);
        int product = unchecked(damage * perHit);
        return Math.Max(0, Truncate(Divide(Convert(product), 100f)));
    }
    internal static int ReflectionAmount(int incomingBeforeShield, int percent, int flat)
    {
        if (incomingBeforeShield < 0) throw new ArgumentOutOfRangeException(nameof(incomingBeforeShield));
        float ratio = Divide(Convert(percent), 100f);
        return Truncate(AddFloat(Multiply(ratio, Convert(incomingBeforeShield)), Convert(flat)));
    }

    internal sealed record Completion(Context Context, CallKind Kind, int Classification, uint SkillId,
        int HpDamage, int MpDamage, int HpHeal, int MpHeal, int ReflectedAggregate,
        uint ReflectionMetadataSkillId, int RawSkillMode, Presence OptionalSystemSkill,
        bool OriginalAggregateOrderVerified, bool OriginalCompletionBranchVerified, string Evidence);
    internal sealed record Dispatch(string Operation, Actor Receiver, Actor Source, int? InputDamage,
        int? MpDamage, int? RawThreat, int? ContextKind, uint? ContextSkillId, uint? ContextFlagC,
        bool OriginalCallbackOutcomeRequired)
    {
        internal string? ReflectionContextPointer0 => ContextKind == 9 ? "original-null" : null;
        internal uint? ReflectionContextField8 => ContextKind == 9 ? 0u : null;
        internal uint? ReflectionContextField10 => ContextKind == 9 ? 0u : null;
        internal uint? ReflectionContextField1C => ContextKind == 9 ? 0xc5u : null;
        internal ushort? ReflectionContextField20 => ContextKind == 9 ? (ushort)0 : null;
        internal bool? PreEmissionProtectorReentry => ContextKind == 9 ? false : null;
        internal bool? CastCancelProbabilityDraw => ContextKind == 9 ? false : null;
    }
    internal sealed record CompletionPlan(int PrimarySignedInput, int PrimaryMpInput,
        int RecordReflectedAmount, uint RecordReflectionSkillId, ReadOnlyCollection<Dispatch> Ordered)
    {
        internal int? PrimaryApplicationCell => null;
        internal int? ReflectedApplicationCell => null;
        internal int? FinalAttackerHp => null;
        internal int? FinalDefenderHp => null;
        internal bool Executed => false;
    }
    internal static CompletionPlan DescribeCompletion(Completion input)
    {
        ArgumentNullException.ThrowIfNull(input); Validate(input.Context); Text(input.Evidence); EnumValue(input.Kind);
        EnumValue(input.OptionalSystemSkill);
        if (input.Classification is not (1 or 2) || !input.OriginalAggregateOrderVerified || !input.OriginalCompletionBranchVerified || input.OptionalSystemSkill == Presence.Unknown)
            throw new ArgumentException("Known original completion/order/optional-system branch required.");
        if ((input.Kind == CallKind.PhysicalSkill && input.Classification != 1) || (input.Kind == CallKind.MagicalSkill && input.Classification != 2)) throw new ArgumentException("Completion class mismatch.");
        bool normal = input.Kind == CallKind.NormalBase;
        if (normal && input.OptionalSystemSkill != Presence.VerifiedAbsent) throw new ArgumentException("Normal completion has no skill optional-system stage.");
        int hp = unchecked(input.HpDamage - input.HpHeal), mp = unchecked(input.MpDamage - input.MpHeal);
        var list = new List<Dispatch>
        {
            new("OriginalPreActionAndMovement", Actor.Defender, Actor.Attacker, null, null, null, null, null, null, true),
            new("PrimaryGetHPMPDamageAndHeal", Actor.Defender, Actor.Attacker, hp, mp, null, !normal && input.RawSkillMode == 3 ? 7 : 2, normal ? 0u : input.SkillId, 0, true),
            new("AttackerSendAttackToNpcServerAfterPrimaryCell", Actor.Attacker, Actor.Defender, null, null, null, null, null, null, true)
        };
        if (normal) list.Add(new("TargetSendHP", Actor.Defender, Actor.Attacker, null, null, null, null, null, null, true));
        if (input.ReflectedAggregate != 0)
        {
            list.Add(new("ReflectedGetHitDamageDirectNoPreEmissionProtector", Actor.Attacker, Actor.Defender, input.ReflectedAggregate, 0, unchecked(input.ReflectedAggregate * 10 + 2), 9, 0, 1, true));
            list.Add(new("AttackerSendHP", Actor.Attacker, Actor.Defender, null, null, null, null, null, null, true));
        }
        if (!normal && input.OptionalSystemSkill == Presence.PresentUnsupported) list.Add(new("OriginalOptionalSystemSkillOutcome", Actor.Attacker, Actor.Defender, null, null, null, null, null, null, true));
        list.Add(new("OriginalProtectedRecipientResolutionAndDamage", Actor.Defender, Actor.Attacker, null, null, null, null, null, null, true));
        list.Add(new("OriginalConditionalEffectItemTargetProcs", Actor.Attacker, Actor.Defender, null, null, null, null, null, null, true));
        return new(hp, mp, input.ReflectedAggregate, input.ReflectionMetadataSkillId, Array.AsReadOnly(list.ToArray()));
    }

    private static WritePlan Plan(List<Write> w, int? hit, int? budget, int? percent, uint? min, uint? max) => new(Array.AsReadOnly(w.ToArray()), hit, budget, percent, min, max);
    private static Transition Later(Transition s) => Set(s, s.RemainingDamage > 0 ? Stage.LaterFlags : Stage.Complete);
    private static Transition Set(Transition s, Stage stage) => s with { Pending = stage };
    private static Transition Stop(Transition s, string reason) => s with { Pending = Stage.Unsupported, UnsupportedReason = reason };
    private static Transition Add(Transition s, string name, string source, string evidence) => s with
    {
        Operations = Append(s.Operations, new Operation(name, source,
            FormattableString.Invariant($"incoming={s.IncomingBeforeShield};remaining={s.RemainingDamage};budget={s.Budget};reflection={s.ReflectedAmount};flags={s.OutputFlags}"), evidence))
    };
    private static ReadOnlyCollection<T> Append<T>(IEnumerable<T> old, T next) => Array.AsReadOnly(old.Append(next).ToArray());
    private static T Need<T>(OriginalRead r) where T : OriginalRead => r as T ?? throw new ArgumentException("Wrong typed original read for pending stage.");
    private static uint Bits(int value) => unchecked((uint)value);
    private static bool Radius(uint bits) { float v = BitConverter.UInt32BitsToSingle(bits); return float.IsFinite(v) && v >= 0; }
    private static void Manager(ManagerPresence p) { EnumValue(p); if (p == ManagerPresence.Unknown) throw new ArgumentException("Unknown manager presence."); }
    private static void EnumValue<T>(T e) where T : struct, Enum { if (!Enum.IsDefined(e)) throw new ArgumentException("Unknown typed enum value."); }
    private static void Text(string? value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Explicit original/source evidence and identity required."); }
    private static void ValidateSkillSpan(int skillId, string hash)
    {
        string expected = skillId == 16522 ? "3900fc5afa4cf949c4a81fc0dafabf485e935768a623e601c366b20961e7c1d4"
            : "8d278399cd188dafbd2307e0d625580b9c3349994129f4845037e873b677c35e";
        if (hash != expected) throw new ArgumentException("Pinned original skill raw span required.");
    }
    private static void Validate(Context c)
    {
        ArgumentNullException.ThrowIfNull(c); ArgumentNullException.ThrowIfNull(c.Scope); ArgumentNullException.ThrowIfNull(c.Source); Text(c.Evidence);
        var i = c.Source;
        if (!string.Equals(i.MainHash, RetailNpcNormalScope.MainBinarySha256, StringComparison.OrdinalIgnoreCase) || i.DatasetHash != Dataset || i.RegionContext != Region)
            throw new ArgumentException("Pinned Main/dataset/region context required.");
        Text(i.ActionId); Text(i.AttackerLife); Text(i.DefenderLife); Text(i.Evidence);
        if (i.AttackerId == 0 || i.DefenderId == 0) throw new ArgumentException("Actual actor identity required.");
    }
    private static void CheckStamp(Transition s, Stamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp); Text(stamp.Evidence);
        if (stamp.Source != s.Input.Context.Source || stamp.ManagerIdentity != s.ManagerIdentity || stamp.Epoch != s.Epoch || stamp.Ordinal != s.NextOrdinal || !stamp.OriginalOrderedReadInterval)
            throw new ArgumentException("Original identity/generation/read-order receipt mismatch.");
    }
    private static void ValidateBound(Bound b)
    {
        ArgumentNullException.ThrowIfNull(b); Manager(b.Presence); Text(b.Evidence);
        if (b.Presence == ManagerPresence.Present) { Text(b.SkillKey); if (b.InstanceIdentity is null) throw new ArgumentException("Original instance identity required."); }
        else if (b.SkillKey is not null || b.InstanceIdentity is not null) throw new ArgumentException("Absent pointer cannot carry invented dereferenced fields.");
    }
    private static void ValidatePost(ManagerSnapshot p)
    {
        Text(p.ManagerIdentity); Text(p.Epoch); Text(p.Life); Text(p.Evidence); ArgumentNullException.ThrowIfNull(p.BoundSkill);
        EnumValue(p.BoundSkill.Presence);
        if (p.BoundSkill.Presence != ManagerPresence.Unknown) ValidateBound(p.BoundSkill);
        ArgumentNullException.ThrowIfNull(p.Class1Shield); ArgumentNullException.ThrowIfNull(p.Class2Shield);
        ArgumentNullException.ThrowIfNull(p.Class1Reflector); ArgumentNullException.ThrowIfNull(p.Class2Reflector);
        EnumValue(p.MpShield); EnumValue(p.Protect); EnumValue(p.Convert);
    }
    private static int Truncate(float f)
    {
        if (!float.IsFinite(f) || f < -2147483648f || f >= 2147483648f) throw new ArithmeticException("Unmodeled CVTTSS2SI nonrepresentable result.");
        return (int)f;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Convert(int v) => v;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float AddFloat(float a, float b) => a + b;
}
