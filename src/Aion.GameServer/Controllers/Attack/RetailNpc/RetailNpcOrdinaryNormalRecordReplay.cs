using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using StatusMath = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalStatusMath;
using Chance = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalStatusMath.ChanceStage;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Inactive ordinary ownerless NPC single-main-record replay. Composes pinned Main
/// arithmetic and original wrapper receipts without RNG, live lookup, effects, scheduling,
/// packets, HP mutation or owner callbacks. Declarations do not attest a live actor.
/// </summary>
/// <remarks>
/// Main 0x140369e10 / 0x14036a5b0 / 0x14036c490; primary arithmetic
/// 0x140372cd0..0x140372d0a. Source-bound domain/receipts and independent vectors:
/// Aion-NPC-Discovery/implementation/phase3/normal-replay and normal-replay-fixtures.
/// Final HP after dispatch/callbacks remains unavailable, including lethal arithmetic.
/// </remarks>
internal static class RetailNpcOrdinaryNormalRecordReplay
{
    internal enum FeatureState { Unknown, VerifiedAbsent, PresentUnsupported }
    internal enum UnsupportedFeature
    {
        ForcedDefense, Blind, Special8024, WeaponBooster, FixedDamageOverride,
        ExclusiveAttribute, LimitAttributeBonus, PreEmissionProtector, EmbeddedAttackSkill,
        AttackerEffectProc, DefenderEffectProc, ItemProc, BreakableEffects, CastingAndCancel,
        OnHitAndHpThresholdEffects, NonlethalOrDuel, SpecialNpcCompletion
    }
    internal enum DrawStage { Base, Dodge, Block, Parry, MagicalResist, MainCritical }
    internal readonly record struct AbsenceReceipt(UnsupportedFeature Feature, FeatureState State, string Evidence);

    /// <summary>Original wrapper endpoints before sorting, not a generated draw.</summary>
    internal readonly record struct OriginalInvocation(string MainSha256, string ActionId, DrawStage Stage,
        int FirstEndpoint, int SecondEndpoint, int Result, bool InclusiveAdapterInvoked, string Evidence);
    internal sealed record Identity(string MainSha256, string DatasetSha256, string RegionContext,
        string ActionId, uint AttackerObjectId, uint DefenderObjectId, string AttackerGeneration,
        string DefenderGeneration, string Evidence);
    internal sealed record Profile(int NpcTemplateId, int SourceIndex, byte CombinedIndex,
        byte MainHitCount, byte OffhandHitCount, ushort MinimumDamage, ushort MaximumDamage,
        float EffectiveProfileRate, float AdditiveRate, ushort FlatWord, int FlatInt,
        int DamageElement, string ElementMappingEvidence, uint PhysicalFlag8e4, int WeaponGroup8ec,
        sbyte NormalAttackExclusiveType450, string Evidence);
    internal sealed record Attacker(int Level, uint StatRatio1514, ushort PhysicalAccuracy14aa,
        ushort MagicalAccuracy14c4, ushort PhysicalCritical14bc, ushort MagicalCritical14c0,
        uint AbnormalFlags1250, int ForcedCompareCounter, string Evidence);
    internal sealed record Defender(int Level, uint StatRatio1514, uint PhysicalDefense,
        uint MagicalDefense, short SelectedElementalDefense, int MinimumElementalDefense,
        int Evasion14ae, int Parry14b4, int Block14b6, int MagicalResistance149c,
        int PhysicalCriticalReduceRate14c6, int MagicalCriticalReduceRate14c8,
        int PhysicalCriticalDamageReduce14ca, int MagicalCriticalDamageReduce14cc,
        byte RawCode13c, uint PhysicalDamageOverride, uint ElementalDamageOverride, string Evidence);
    internal sealed record Movement(int AttackerAttackPercent, int DefenderDefensePercent,
        int DefenderElementalAddition, int DefenderEvasionAddition, int DefenderBlockAddition,
        int DefenderParryAddition, int AttackerCriticalAddition, string Evidence);
    internal sealed record Eligibility(bool DodgeTableAllows, bool ParryTableAllows,
        int BlockSlot129Result, string Evidence);
    /// <summary>Actual composite stat operands, not callbacks returning desired damage.</summary>
    internal sealed record Classification(int AttackerSlot127Result, int DefenderSlot127Result,
        int PhysicalPveDefenseOperand, int ElementalPveDefenseOperand, string Evidence);
    internal sealed record EffectiveAction(RetailNpcNormalScope Scope, Identity Source,
        Profile SelectedProfile, Attacker EffectiveAttacker, Defender EffectiveDefender,
        Movement LoadedMovement, Eligibility NativeEligibility, Classification NativeClassification,
        bool OriginalWorldAndActorGuardsPassed, string GuardEvidence,
        IReadOnlyList<AbsenceReceipt> FeatureAbsences);

    /// <summary>Intent only: no tick or deadline is manufactured or written.</summary>
    internal readonly record struct CounterWindowIntent(uint Mask)
    {
        internal uint? OriginalTick => null;
        internal uint? Deadline => null;
    }
    internal sealed record Stages(RetailNpcBaseDamageStages Base,
        RetailNpcPhysicalDamageStages? PhysicalDefense, RetailNpcElementalDamageStages? ElementalDefense,
        float AfterActiveDefense, int? DodgeThreshold, int? BlockThreshold, int? ParryThreshold,
        int? MagicalResistThreshold, int? CriticalThreshold, float? CriticalMultiplier,
        float? AfterCritical, float? PveMultiplier, float? AfterPve);
    internal sealed class Result
    {
        internal Identity Source { get; }
        internal RetailNpcNormalActorKind DefenderKind { get; }
        internal int SelectedSourceIndex { get; }
        internal byte SelectedCombinedIndex { get; }
        internal int RecordDamage { get; }
        internal float PreprotectorFloat { get; }
        internal byte BaseStatus { get; }
        internal byte CriticalStatus { get; }
        internal uint AggregateFlags { get; }
        internal bool TerminalMiss { get; }
        internal Stages Arithmetic { get; }
        internal ReadOnlyCollection<OriginalInvocation> OriginalInvocations { get; }
        internal ReadOnlyCollection<CounterWindowIntent> CounterIntents { get; }
        // Inner resist finalization and outer CalcNormal each consult the zero override.
        internal int RequiredZeroOverrideConsultations => TerminalMiss ? 0 : 2;
        internal int? FinalHpAfterCallbacks => null;
        internal Result(EffectiveAction x, int damage, float stored, byte status, byte critical,
            uint flags, bool terminal, Stages stages, IEnumerable<OriginalInvocation> calls,
            IEnumerable<CounterWindowIntent> counters)
        {
            Source = x.Source; DefenderKind = x.Scope.Defender;
            SelectedSourceIndex = x.SelectedProfile.SourceIndex; SelectedCombinedIndex = x.SelectedProfile.CombinedIndex;
            RecordDamage = damage; PreprotectorFloat = stored; BaseStatus = status; CriticalStatus = critical;
            AggregateFlags = flags; TerminalMiss = terminal; Arithmetic = stages;
            OriginalInvocations = Array.AsReadOnly(calls.ToArray());
            CounterIntents = Array.AsReadOnly(counters.ToArray());
        }
    }
    internal sealed record PrimaryCommitSnapshot(Identity Source, int CurrentHpBeforePrimaryOperation,
        bool OriginalActorDispatchGuardsPassed, int NativeDeathPolicySlot179Result, uint RawNoKillFlag,
        FeatureState InterveningWrites, bool HasSkillEffectManager,
        IReadOnlyList<AbsenceReceipt> FeatureAbsencesAtCommit, string Evidence);
    internal sealed record ReceiptRequirement(string Boundary, string Receiver, string NativeEvidence);
    internal sealed record PrimaryPrediction(int RequestedApplicationCell, int CellAfterCap,
        int HpAfterPrimaryArithmetic, int PrimaryDelta, bool LethalArithmeticBranch,
        ReadOnlyCollection<ReceiptRequirement> RequiredOriginalBoundaries)
    {
        internal int? FinalHpAfterCallbacks => null;
    }
    internal static Result Replay(EffectiveAction action, IReadOnlyList<OriginalInvocation> originalInvocations)
    {
        ValidateAction(action);
        ArgumentNullException.ThrowIfNull(originalInvocations);
        OriginalInvocation[] supplied = originalInvocations.ToArray();
        int next = 0;
        var p = action.SelectedProfile; var a = action.EffectiveAttacker;
        var d = action.EffectiveDefender; var m = action.LoadedMovement;
        var eligibility = action.NativeEligibility;
        var context = StatusMath.Context.Create(action.Scope, action.Source.MainSha256, action.Source.Evidence, false);
        var range = RetailNpcNormalRange.Create(action.Scope, p.MinimumDamage, p.MaximumDamage);
        var baseCall = Take(DrawStage.Base, p.MinimumDamage, p.MaximumDamage, range.RequiresRandomDraw);
        int? sample = range.RequiresRandomDraw ? baseCall.Result : null;
        if (!range.RequiresRandomDraw && baseCall.Result != range.Minimum)
            throw new ArgumentException("The singleton wrapper must return its endpoint.", nameof(originalInvocations));
        var baseStages = RetailNpcNormalDamageStages.CalculateNpcBase(action.Scope, range, sample,
            p.EffectiveProfileRate, p.AdditiveRate, p.FlatWord, p.FlatInt, m.AttackerAttackPercent);
        RetailNpcPhysicalDamageStages? physical = null;
        RetailNpcElementalDamageStages? elemental = null;
        float damage;
        bool physicalElement = p.DamageElement == 0;
        if (physicalElement)
        {
            physical = RetailNpcNormalDamageStages.CalculatePhysicalDefense(action.Scope, baseStages.AfterMovement,
                a.Level, d.Level, false, d.PhysicalDefense, m.DefenderDefensePercent, d.StatRatio1514);
            damage = physical.Value.Damage;
        }
        else
        {
            elemental = RetailNpcNormalDamageStages.CalculateElementalDefense(action.Scope, baseStages.AfterMovement,
                a.Level, d.Level, false, d.MagicalDefense, m.DefenderDefensePercent, d.SelectedElementalDefense,
                m.DefenderElementalAddition, a.StatRatio1514, d.MinimumElementalDefense);
            damage = elemental.Value.Damage;
        }
        byte status = 10;
        uint flags = 0;
        bool terminal = false;
        int? dodge = null, block = null, parry = null, resist = null;
        var counters = new List<CounterWindowIntent>();
        // This classifier is deliberately independent of selected damage element/flag.
        bool physicalGroup = ((1u << (p.WeaponGroup8ec & 31)) & 0x3e07fu) != 0;
        if (physicalGroup)
        {
            if (d.RawCode13c != 5 && (a.AbnormalFlags1250 & 0x649f04cu) == 0)
            {
                if (eligibility.DodgeTableAllows)
                {
                    dodge = PhysicalThreshold(Chance.Dodge, d.Evasion14ae, m.DefenderEvasionAddition);
                    if (Roll(Chance.Dodge, dodge.Value)) { status = 0; flags = 0x80; terminal = true; }
                }
                if (!terminal && eligibility.BlockSlot129Result != 0)
                {
                    block = PhysicalThreshold(Chance.Block, d.Block14b6, m.DefenderBlockAddition);
                    if (Roll(Chance.Block, block.Value))
                    {
                        // Player shield-helper eligibility is rejected before calculation.
                        damage = Multiply(damage, 0.9f); status = 4; flags = 0x20;
                    }
                }
                if (!terminal && status != 4 && eligibility.ParryTableAllows)
                {
                    parry = PhysicalThreshold(Chance.Parry, d.Parry14b4, m.DefenderParryAddition);
                    if (Roll(Chance.Parry, parry.Value))
                    {
                        damage = Multiply(damage, action.Scope.Defender == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc ? 0.9f : 0.6f);
                        status = 2; flags = 0x40;
                    }
                }
            }
        }
        else
        {
            resist = StatusMath.CalculateMagicalResistThreshold(context, new(d.MagicalResistance149c,
                a.MagicalAccuracy14c4, a.StatRatio1514, a.Level, d.Level, d.RawCode13c));
            if (Roll(Chance.MagicalResist, resist.Value)) { status = 6; flags = 0x100; terminal = true; }
        }
        if (flags != 0) counters.Add(new(flags));
        if (terminal)
        {
            RequireAllConsumed();
            // World's per-hand stored float and record critical byte start at zero. The
            // terminal resist return skips finalization and its stored-float write.
            return new(action, 0, 0f, status, 0, flags, true,
                new(baseStages, physical, elemental, 0f, dodge, block, parry, resist,
                    null, null, null, null, null), supplied, counters);
        }
        float afterActive = damage;
        // Native requests this wrapper before composing the immutable critical operands.
        var criticalCall = Take(DrawStage.MainCritical, 1, 1000, true);
        int criticalThreshold = StatusMath.CalculateMainCriticalThreshold(context, new(
            physicalElement ? a.PhysicalCritical14bc : a.MagicalCritical14c0,
            physicalElement ? d.PhysicalCriticalReduceRate14c6 : d.MagicalCriticalReduceRate14c8,
            m.AttackerCriticalAddition, d.StatRatio1514, d.RawCode13c));
        bool criticalHit = StatusMath.CompareOriginalMainRoll(context, Chance.MainCritical,
            criticalThreshold, ToStatusReceipt(Chance.MainCritical, criticalCall));
        float? criticalMultiplier = null;
        byte criticalStatus = 0;
        if (criticalHit)
        {
            criticalMultiplier = RetailNpcNormalDamageStages.CalculateNpcCriticalMultiplier(action.Scope,
                physicalElement, (ushort)(physicalElement ? d.PhysicalCriticalDamageReduce14ca : d.MagicalCriticalDamageReduce14cc), a.StatRatio1514);
            damage = Multiply(criticalMultiplier.Value, damage); criticalStatus = 12;
        }
        float afterCritical = damage;
        float pve = 1f;
        if (action.NativeClassification.DefenderSlot127Result != 0)
        {
            int operand = physicalElement ? action.NativeClassification.PhysicalPveDefenseOperand
                : action.NativeClassification.ElementalPveDefenseOperand;
            pve = RetailNpcNormalDamageStages.CalculatePveRatio(action.Scope, 0, operand);
            damage = Multiply(pve, damage);
        }
        float afterPve = damage;
        if (damage < 1f) damage = 1f;
        int recordDamage = TruncateInt32(damage);
        RequireAllConsumed();
        return new(action, recordDamage, damage, status, criticalStatus, flags, false,
            new(baseStages, physical, elemental, afterActive, dodge, block, parry, resist,
                criticalThreshold, criticalMultiplier, afterCritical, pve, afterPve), supplied, counters);

        OriginalInvocation Take(DrawStage stage, int first, int second, bool adapter)
        {
            if (next >= supplied.Length)
                throw new ArgumentException($"Missing original {stage} wrapper invocation.", nameof(originalInvocations));
            var r = supplied[next++];
            RequireIdentity(r.MainSha256); RequireEvidence(r.Evidence, nameof(r.Evidence));
            if (r.ActionId != action.Source.ActionId || r.Stage != stage || r.FirstEndpoint != first
                || r.SecondEndpoint != second || r.InclusiveAdapterInvoked != adapter)
                throw new ArgumentException("Different original action, stage, endpoints or adapter use.", nameof(originalInvocations));
            if (r.Result < Math.Min(first, second) || r.Result > Math.Max(first, second))
                throw new ArgumentOutOfRangeException(nameof(originalInvocations), r.Result, "Original result is outside its endpoints.");
            return r;
        }
        bool Roll(Chance stage, int threshold)
        {
            var r = Take(stage switch
            {
                Chance.Dodge => DrawStage.Dodge, Chance.Block => DrawStage.Block,
                Chance.Parry => DrawStage.Parry, Chance.MagicalResist => DrawStage.MagicalResist,
                _ => throw new ArgumentOutOfRangeException(nameof(stage))
            }, 1, 1000, true);
            return StatusMath.CompareOriginalMainRoll(context, stage, threshold, ToStatusReceipt(stage, r));
        }
        int PhysicalThreshold(Chance stage, int defense, int movement)
        {
            int adjusted = action.Scope.Defender == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc
                ? StatusMath.AdjustOwnerlessNpcDefense(context, stage, (ushort)defense, a.Level, d.Level) : defense;
            return StatusMath.CalculatePhysicalThreshold(context, stage,
                new(adjusted, a.PhysicalAccuracy14aa, movement, a.StatRatio1514));
        }
        void RequireAllConsumed()
        {
            if (next != supplied.Length)
                throw new ArgumentException("Extra original invocation after this branch completed.", nameof(originalInvocations));
        }
    }
    /// <summary>
    /// Primary cap/subtraction only under serial, positive-HP, ordinary kill-allowed and
    /// absent-effect conditions at commit. No atomic race, dispatch, death or callback is
    /// simulated. Zero damage still requires original last-attacker/owner receipts.
    /// </summary>
    internal static PrimaryPrediction PredictSerialPrimaryCommit(Result result, PrimaryCommitSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(result); ArgumentNullException.ThrowIfNull(snapshot);
        ValidateIdentity(snapshot.Source); RequireEvidence(snapshot.Evidence, nameof(snapshot.Evidence));
        if (snapshot.Source != result.Source)
            throw new ArgumentException("Commit provenance/action/actor generations differ from replay.", nameof(snapshot));
        ValidateAbsences(snapshot.FeatureAbsencesAtCommit);
        if (!snapshot.OriginalActorDispatchGuardsPassed || snapshot.NativeDeathPolicySlot179Result != 0
            || snapshot.RawNoKillFlag != 0 || snapshot.InterveningWrites != FeatureState.VerifiedAbsent)
            throw new ArgumentException("Explicit ordinary kill-allowed serial dispatch conditions are required.", nameof(snapshot));
        if (snapshot.CurrentHpBeforePrimaryOperation <= 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Positive commit-time HP is required.");
        if (result.RecordDamage < 0)
            throw new ArgumentException("This normal-record branch has no signed heal channel.", nameof(result));
        int cell = Math.Min(result.RecordDamage, snapshot.CurrentHpBeforePrimaryOperation);
        int hp = snapshot.CurrentHpBeforePrimaryOperation - cell;
        bool lethal = result.RecordDamage >= snapshot.CurrentHpBeforePrimaryOperation;
        var boundaries = new List<ReceiptRequirement>();
        if (result.RecordDamage != 0)
            boundaries.Add(new("PreDispatchActorCallback", "defender", "World 0x14068ee9c: slot161(2,0,0)"));
        boundaries.Add(new("SignedDamageDispatcher", "defender", "World 0x14068efaf -> 0x140373200"));
        boundaries.Add(new("ActorGetHitDamageEntryAndLastAttacker", "defender", "slot136; Creature 0x140372a70"));
        if (result.RecordDamage > 0)
        {
            boundaries.Add(new("BreakEffects", "defender", "Creature 0x140372aa1; effects explicitly absent"));
            if (snapshot.HasSkillEffectManager)
                boundaries.Add(new("ByValueOnHitCallback", "defender", "0x140372ac7; no mutable damage pointer"));
            boundaries.Add(new("CancelAndDecMp", "defender", "0x140372aef / 0x140372cc9; no casting, MP channel0"));
            boundaries.Add(new("PrimaryHpOperation", "defender", "0x140372cd0..0x140372d0a; arithmetic prediction only"));
            if (!lethal)
            {
                if (snapshot.HasSkillEffectManager)
                    boundaries.Add(new("OnHitPass2", "defender", "0x140372eca; threshold effects absent"));
                boundaries.Add(new("HpConditionedEffectCheck", "defender", "0x140372ed2; effect absent"));
            }
            else
                boundaries.Add(new("OriginalDeathPolicyAndOwnerWork", "defender", "0x140372dff; consequences unknown"));
            boundaries.Add(new("SendHpInsideBaseAndHpChange", "defender", "Creature 0x140372edc..0x140372faa"));
            boundaries.Add(new("BasePostHpCallbacksAndState", "defender", "slot159, +788 clear, slot161; original receipts required"));
        }
        boundaries.Add(new("ActorGetHitDamageExit", "defender", "Npc 0x140b232b0 / User 0x140541d20; owner consequences unknown"));
        boundaries.Add(new("AttackerResultSync", "attacker", "World 0x14068efd7: slot55 with mutable cell"));
        boundaries.Add(new("SendHpAfterPrimary", "defender", "World 0x14068efe4; retain even when base already sends HP"));
        boundaries.Add(new("RemainingPass2Callbacks", "attacker/defender", "World 0x14068f07a..0x14068f0d5; no callbacks replayed"));
        return new(result.RecordDamage, cell, hp, cell, lethal, Array.AsReadOnly(boundaries.ToArray()));
    }

    private static void ValidateAction(EffectiveAction x)
    {
        ArgumentNullException.ThrowIfNull(x); ArgumentNullException.ThrowIfNull(x.Scope);
        x.Scope.RequireNpcAttacker(); ValidateIdentity(x.Source);
        ArgumentNullException.ThrowIfNull(x.SelectedProfile); ArgumentNullException.ThrowIfNull(x.EffectiveAttacker);
        ArgumentNullException.ThrowIfNull(x.EffectiveDefender); ArgumentNullException.ThrowIfNull(x.LoadedMovement);
        ArgumentNullException.ThrowIfNull(x.NativeEligibility); ArgumentNullException.ThrowIfNull(x.NativeClassification);
        ValidateAbsences(x.FeatureAbsences); RequireEvidence(x.GuardEvidence, nameof(x.GuardEvidence));
        if (!x.OriginalWorldAndActorGuardsPassed)
            throw new ArgumentException("Original World/actor guards must have passed; no geometry is generated.", nameof(x));
        var p = x.SelectedProfile; var a = x.EffectiveAttacker; var d = x.EffectiveDefender;
        RequireEvidence(p.Evidence, nameof(p.Evidence)); RequireEvidence(p.ElementMappingEvidence, nameof(p.ElementMappingEvidence));
        RequireEvidence(a.Evidence, nameof(a.Evidence)); RequireEvidence(d.Evidence, nameof(d.Evidence));
        RequireEvidence(x.LoadedMovement.Evidence, nameof(x.LoadedMovement.Evidence));
        RequireEvidence(x.NativeEligibility.Evidence, nameof(x.NativeEligibility.Evidence));
        RequireEvidence(x.NativeClassification.Evidence, nameof(x.NativeClassification.Evidence));
        if (p.NpcTemplateId != x.Scope.AttackerNpcTemplateId || p.SourceIndex < 0 || p.MainHitCount != 1 || p.OffhandHitCount != 0)
            throw new ArgumentException("Supply one matched ordinary main-hand record and its source index.", nameof(x));
        if (p.DamageElement < 0 || p.DamageElement > 6 || p.PhysicalFlag8e4 != (p.DamageElement == 0 ? 1u : 0u))
            throw new ArgumentException("Unsupported element or inconsistent physical flag; group remains independent.", nameof(x));
        RequireFinite(p.EffectiveProfileRate); RequireFinite(p.AdditiveRate);
        RequireLevel(a.Level); RequireLevel(d.Level); RequireRatio(a.StatRatio1514); RequireRatio(d.StatRatio1514);
        if (a.ForcedCompareCounter != 0 || (a.AbnormalFlags1250 & 0x20u) != 0 || p.NormalAttackExclusiveType450 != 0
            || d.PhysicalDamageOverride != 0 || d.ElementalDamageOverride != 0)
            throw new ArgumentException("Forced/blind/fixed/exclusive branches are outside this replay.", nameof(x));
        if (d.RawCode13c > 15)
            throw new ArgumentOutOfRangeException(nameof(x), "Supply the original low nibble without an inferred enum.");
        RequireWord(d.Evasion14ae); RequireWord(d.Parry14b4); RequireWord(d.Block14b6);
        RequireNonnegative(d.MagicalResistance149c);
        RequireNonnegative(d.PhysicalCriticalReduceRate14c6); RequireNonnegative(d.MagicalCriticalReduceRate14c8);
        RequireWord(d.PhysicalCriticalDamageReduce14ca); RequireWord(d.MagicalCriticalDamageReduce14cc);
        if (x.Scope.Defender == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc)
        {
            RequireWord(d.MagicalResistance149c); RequireWord(d.PhysicalCriticalReduceRate14c6); RequireWord(d.MagicalCriticalReduceRate14c8);
            if (d.MinimumElementalDefense != -1300 || x.NativeEligibility.BlockSlot129Result != d.Block14b6)
                throw new ArgumentException("NPC floor and slot129 must match their verified effective construction.", nameof(x));
        }
        else if (x.NativeEligibility.BlockSlot129Result != 0)
            throw new ArgumentException("Player shield/block-helper branches are not admitted.", nameof(x));
        if (x.NativeClassification.AttackerSlot127Result != 0
            || x.NativeClassification.DefenderSlot127Result != (x.Scope.Defender == RetailNpcNormalActorKind.Player ? 1 : 0))
            throw new ArgumentException("Only ordinary ownerless NPC attacker classification is supported.", nameof(x));
    }
    private static void ValidateAbsences(IReadOnlyList<AbsenceReceipt> receipts)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        var all = Enum.GetValues<UnsupportedFeature>();
        if (receipts.Count != all.Length)
            throw new ArgumentException("Every unsupported feature needs exactly one explicit absence receipt.", nameof(receipts));
        var seen = new HashSet<UnsupportedFeature>();
        foreach (var r in receipts)
        {
            if (!Enum.IsDefined(r.Feature) || !seen.Add(r.Feature) || r.State != FeatureState.VerifiedAbsent)
                throw new ArgumentException("Unknown, present or duplicate unsupported features cannot be replayed.", nameof(receipts));
            RequireEvidence(r.Evidence, nameof(r.Evidence));
        }
    }
    private static void ValidateIdentity(Identity x)
    {
        ArgumentNullException.ThrowIfNull(x); RequireIdentity(x.MainSha256);
        if (x.DatasetSha256 is null || x.DatasetSha256.Length != 64 || x.DatasetSha256.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Supply the declared source dataset SHA256.", nameof(x));
        RequireEvidence(x.RegionContext, nameof(x.RegionContext)); RequireEvidence(x.ActionId, nameof(x.ActionId));
        RequireEvidence(x.AttackerGeneration, nameof(x.AttackerGeneration)); RequireEvidence(x.DefenderGeneration, nameof(x.DefenderGeneration));
        RequireEvidence(x.Evidence, nameof(x.Evidence));
        if (x.AttackerObjectId == 0 || x.DefenderObjectId == 0 || x.AttackerObjectId == x.DefenderObjectId)
            throw new ArgumentException("Distinct original actor identities are required; self/unverified actors are excluded.", nameof(x));
    }
    private static StatusMath.OriginalMainDrawReceipt ToStatusReceipt(Chance stage, OriginalInvocation r) =>
        new(r.MainSha256, stage, r.FirstEndpoint, r.SecondEndpoint, r.Result, r.InclusiveAdapterInvoked, r.Evidence);
    private static void RequireIdentity(string sha)
    {
        if (!string.Equals(sha, RetailNpcNormalScope.MainBinarySha256, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Unsupported Main source identity.", nameof(sha));
    }
    private static void RequireEvidence(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Explicit original-source evidence is required.", name);
    }
    private static void RequireLevel(int n)
    {
        if (n < 1 || n > 100) throw new ArgumentOutOfRangeException(nameof(n), "Verified level domain is 1..100.");
    }
    private static void RequireRatio(uint n)
    {
        if (n < 1000 || n > 65000) throw new ArgumentOutOfRangeException(nameof(n), "Verified stat_ratio domain is 1000..65000.");
    }
    private static void RequireWord(int n)
    {
        if (n < 0 || n > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(n), "Supply the actual unsigned-word/low16 operand.");
    }
    private static void RequireNonnegative(int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
    }
    private static void RequireFinite(float n)
    {
        if (!float.IsFinite(n)) throw new ArgumentOutOfRangeException(nameof(n), "Nonfinite arithmetic is unsupported.");
    }
    private static int TruncateInt32(float n)
    {
        if (!float.IsFinite(n) || n < -2147483648f || n >= 2147483648f)
            throw new ArithmeticException("Final CVTTSS2SI is outside the supported representable int32 domain.");
        return (int)n;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Multiply(float left, float right)
    {
        float n = left * right;
        if (!float.IsFinite(n)) throw new ArithmeticException("Intermediate binary32 overflow is unsupported.");
        return n;
    }
}
