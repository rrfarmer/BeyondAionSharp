using System.Collections.Immutable;
using System.Numerics;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Inactive selected NPC Heal_Instant replay, not a skill dispatcher or HP writer.
/// Java HealInstantEffect/AbstractHealEffect/HealEffectTemplate were read first.
/// Main CheckResist bc4e60, BeginEffect bc51e0, Npc Heal b22cc0/Heal2 b22ed0,
/// Creature Heal2 3737c0 and threshold 379600: 1350 instructions/6428 bytes checked.
/// External phase3/pilot-skill-effects retains original bytes, vtable/token bindings
/// and independent examples. Only the two pinned, no-boost pilot records enter Replay.
/// </summary>
internal static class RetailNpcInstantHealPlan
{
    internal const string MainSha256 = RetailNpcNormalScope.MainBinarySha256;
    internal const string DatasetSha256 = "33dfd001439620a102ccfca9b30765578efa88acb6f561f8c116ce8d98873335";
    internal const string SkillFileSha256 = "c0d8e5dc42c10c512c86c02cef37e95a6c214710feaddc95727b24c376110552";
    internal const string Region = "generation-a-USA-base;loaded-context-unavailable";
    internal const ulong OrdinaryNpcVtable = 0x1412b8470;

    internal enum ReadKind { CheckMaximum, Begin, HealEntry, CompareExchange, Threshold, ThresholdReturn, HealReturn }
    internal enum ThresholdCall { None, FireSystemSkill, RemoveSystemSkill }
    internal sealed record Actor(uint ObjectId, string Generation, ulong Vtable,
        uint? MasterObjectId, uint? SourceRelatedObjectId, bool? VirtualProfilePresent, string Evidence);
    internal sealed record Context(string MainSha256, string DatasetSha256, string Region,
        string ActionId, Actor Caster, Actor Target, string Evidence);
    internal sealed record Stamp(Context Context, int Ordinal, ReadKind Kind, string Evidence);
    internal sealed record SkillSource(int Id, string Devname, string FileSha256, string RawRecordSha256,
        long ByteStart, long ByteEndExclusive, int EffectIndex, string EffectType,
        int NoResist, int ApplyHealBoost, int R1, int R2, int R6, short Level, string Evidence);
    internal sealed record MaximumRead(Stamp Stamp, int MaximumHp);
    internal sealed record BeginRead(Stamp Stamp, int OldHp, float TargetMultiplier1050,
        int ActionMinimumRequested, int ActionMaximumRequested, bool NearestEvenArithmetic);
    internal sealed record EntryRead(Stamp Stamp, int Life728, int CurrentHp, uint Abnormal1250);
    /// <summary>Original CAS return is an observation; the attempted new HP is independently calculated.</summary>
    internal sealed record CasRead(Stamp Stamp, int CurrentHp, int MaximumHp, int CompareExchangeReturnedOldHp);
    /// <summary>ActorLevel240 is the adjacent actor read, not a field of the configured skill object.</summary>
    internal sealed record ThresholdSkill(string Identity, int SkillId148, ushort ActorLevel240, uint MetadataBb8, string Evidence);
    /// <summary>
    /// With flag bit0 clear, Configured238 must be null (not read). With bit0 set,
    /// null means an explicitly observed null pointer. Scalar null always means not read.
    /// </summary>
    internal sealed record ThresholdRead(Stamp Stamp, uint Flags12cc, ThresholdSkill? Configured238,
        bool? Active218, int? MaximumHp, int? Percent230, int? FirstHp, int? SecondHp);
    /// <summary>
    /// Explicit original callback boundary. Fire reloads configured+238 and its metadata
    /// AFTER return; the reloaded pointer need not be the pre-call pointer.
    /// </summary>
    internal sealed record ThresholdReturn(Stamp Stamp, ThresholdCall Call, bool OriginalCallReturned,
        ThresholdSkill? ReloadedConfigured238, string OriginalCallbackEvidence);
    internal sealed record HealReturn(Stamp Stamp, int RereadHp, int TargetRatioCurrentHp,
        int TargetRatioMaximumHp, int CasterRatioCurrentHp, int CasterRatioMaximumHp,
        byte CasterVirtual5e8, byte TargetVirtual5e8);
    internal sealed record Observation(Context Context, SkillSource Skill, MaximumRead Maximum,
        BeginRead Begin, EntryRead Entry, ImmutableArray<CasRead> Attempts,
        ThresholdRead? Threshold, ThresholdReturn? Callback, HealReturn? Returned);
    internal sealed record CasCalculation(int Ordinal, int ExpectedHp, int MaximumHp,
        int WrappedSum, int AttemptedHp, int ArgumentDelta, bool Succeeded);
    internal sealed record Operation(string Kind, int? Value, int? Type, uint? Metadata, string Evidence);
    internal sealed record StackArgumentWrite(int CallerStackOffset, int WidthBits, ulong Value);
    /// <summary>
    /// Exact call-site recipe, not a reconstructed callable ABI. Fire leaves outgoing
    /// stack+20 unwritten here; its role is not invented from a decompiler prototype.
    /// </summary>
    internal sealed record ThresholdInvocation(ulong Function, uint ThisObjectId, uint? TargetObjectId,
        int? SkillId, ushort? SkillLevel, ImmutableArray<StackArgumentWrite> StackWrites,
        bool OutgoingStack20Unwritten, uint? Edx);
    internal sealed record Record(int HpDelta, int SecondaryDelta, int RepeatedHpDelta,
        byte TargetRatio, byte CasterRatio, byte CasterVirtual, byte TargetVirtual);
    internal sealed record Result(int Reserved, int Requested, int HpArgumentCell,
        int PrimaryHp, bool HealReturnedTrue, int MinimumRequested, int MaximumRequested,
        ThresholdCall ThresholdAction, int? ThresholdValue, ThresholdInvocation? Invocation, string? ActivePointerWrite,
        uint? ActiveMetadataWrite, Record? EmittedRecord, ImmutableArray<CasCalculation> Attempts,
        ImmutableArray<Operation> Operations)
    {
        // Original post-callback reads are facts used to reconstruct the emitted record,
        // not a computation of the final actor state or authority to mutate that state.
        internal int? FinalHpAfterCallbacks => null;
        internal bool RuntimeVerified => false;
        internal bool ExecutesHpOrCallbacks => false;
    }

    internal static Result Replay(Observation input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateContext(input.Context); ValidateSkill(input.Skill);
        int ordinal = 0;
        void Read(Stamp stamp, ReadKind kind)
        {
            if (stamp.Context != input.Context || stamp.Ordinal != ++ordinal || stamp.Kind != kind)
                throw new ArgumentException("Stale identity, reordered read, or incorrect original boundary.");
            Text(stamp.Evidence);
        }
        Read(input.Maximum.Stamp, ReadKind.CheckMaximum);
        int reserved = CalculateNoBoostReserved(input.Skill.Level, input.Skill.R1, input.Skill.R2,
            input.Skill.R6, input.Maximum.MaximumHp);
        Read(input.Begin.Stamp, ReadKind.Begin);
        if (!input.Begin.NearestEvenArithmetic)
            throw new ArgumentException("The selected binary32 rounding contract is unresolved.");
        int requested = CalculateRequested(reserved, input.Begin.TargetMultiplier1050);
        Read(input.Entry.Stamp, ReadKind.HealEntry);
        if (input.Attempts.IsDefault || input.Attempts.Length > 1024)
            throw new ArgumentException("Complete bounded CAS evidence is required; this is not a native retry limit.");
        var calculations = ImmutableArray.CreateBuilder<CasCalculation>();
        var operations = ImmutableArray.CreateBuilder<Operation>();
        int hp = input.Entry.CurrentHp, cell = requested;
        bool eligible = input.Entry.Life728 == 0 && hp > 0 && requested != 0
            && (input.Entry.Abnormal1250 & 0x80) == 0;
        bool succeeded = false;
        foreach (var attempt in input.Attempts)
        {
            if (!eligible || succeeded) throw new ArgumentException("Unconsumed CAS observations.");
            Read(attempt.Stamp, ReadKind.CompareExchange);
            int sum = unchecked(attempt.CurrentHp + requested);
            int next = sum, delta = requested;
            if (attempt.MaximumHp <= sum)
            {
                next = attempt.MaximumHp;
                delta = unchecked(attempt.MaximumHp - attempt.CurrentHp);
            }
            succeeded = attempt.CompareExchangeReturnedOldHp == attempt.CurrentHp;
            calculations.Add(new(attempt.Stamp.Ordinal, attempt.CurrentHp, attempt.MaximumHp,
                sum, next, delta, succeeded));
            if (succeeded) { hp = next; cell = delta; }
        }
        if (eligible && !succeeded) throw new ArgumentException("CAS evidence ended before a successful exchange.");
        bool healed = succeeded && cell != 0;
        ThresholdCall action = ThresholdCall.None;
        ThresholdInvocation? invocation = null;
        int? threshold = null;
        string? activeWrite = null;
        uint? metadataWrite = null;
        if (healed)
        {
            // Creature Heal2 branch param7==0. These events are separate from the record.
            operations.Add(new("SendHP", null, null, null, "373973/SendHP before HPChange"));
            operations.Add(new("SendHPChange", 0, 5, 0xc5, "3737c0: flag0, source caster ID, separate notification"));
            if (input.Threshold is null) throw new ArgumentException("Fresh post-notification threshold reads are required.");
            var state = input.Threshold;
            Read(state.Stamp, ReadKind.Threshold);
            if ((state.Flags12cc & 1) == 0 && state.Configured238 is not null)
                throw new ArgumentException("Flag short circuit never consumes configured+238.");
            if ((state.Flags12cc & 1) != 0 && state.Configured238 is not null)
            {
                Skill(state.Configured238);
                if (state.Active218 is null || state.MaximumHp is null || state.Percent230 is null || state.FirstHp is null)
                    throw new ArgumentException("Required threshold fields are unknown.");
                threshold = CalculateThreshold(state.MaximumHp.Value, state.Percent230.Value);
                if (!state.Active218.Value)
                {
                    if (state.SecondHp is not null) throw new ArgumentException("Inactive branch has no second HP getter.");
                    if (state.FirstHp <= threshold) action = ThresholdCall.FireSystemSkill;
                }
                else
                {
                    if (state.SecondHp is null) throw new ArgumentException("Active branch calls a second original HP getter.");
                    if (state.SecondHp > threshold) action = ThresholdCall.RemoveSystemSkill;
                }
            }
            else if (state.Active218 is not null || state.MaximumHp is not null || state.Percent230 is not null
                || state.FirstHp is not null || state.SecondHp is not null)
                throw new ArgumentException("Threshold short circuit contains unconsumed scalar reads.");
            if (action != ThresholdCall.None)
            {
                var callback = input.Callback ?? throw new ArgumentException("Original threshold callback return is required.");
                Read(callback.Stamp, ReadKind.ThresholdReturn);
                Text(callback.OriginalCallbackEvidence);
                if (!callback.OriginalCallReturned || callback.Call != action)
                    throw new ArgumentException("Incorrect or incomplete original callback.");
                invocation = action == ThresholdCall.FireSystemSkill
                    ? new(0x140360d70, input.Context.Target.ObjectId, input.Context.Target.ObjectId,
                        state.Configured238!.SkillId148, state.Configured238.ActorLevel240,
                        [new(0x28, 32, 1), new(0x30, 16, 0x32), new(0x38, 8, 1),
                            new(0x40, 8, 0), new(0x48, 8, 1), new(0x50, 64, 0)], true, null)
                    : new(0x1400b3e30, input.Context.Target.ObjectId, null, null, null, [], false, 0);
                operations.Add(new(action.ToString(), action == ThresholdCall.FireSystemSkill
                    ? state.Configured238!.SkillId148 : 0, null, null, callback.OriginalCallbackEvidence));
                if (action == ThresholdCall.FireSystemSkill)
                {
                    var reload = callback.ReloadedConfigured238 ??
                        throw new ArgumentException("Post-fire configured pointer reload is unavailable.");
                    Skill(reload); activeWrite = reload.Identity; metadataWrite = reload.MetadataBb8;
                    operations.Add(new("WriteActivePointer218FromReloaded238", null, null, reload.MetadataBb8, reload.Evidence));
                    operations.Add(new("WriteActiveMetadata12c4", null, null, reload.MetadataBb8, reload.Evidence));
                }
                else if (callback.ReloadedConfigured238 is not null)
                    throw new ArgumentException("Remove branch has no post-fire pointer reload.");
            }
            else if (input.Callback is not null) throw new ArgumentException("Unconsumed threshold callback.");
        }
        else if (input.Threshold is not null || input.Callback is not null)
            throw new ArgumentException("No notifications/threshold callback occur without nonzero committed delta.");

        Record? record = null;
        if (healed)
        {
            var returned = input.Returned ?? throw new ArgumentException("True Heal return requires original post-callback getters.");
            Read(returned.Stamp, ReadKind.HealReturn);
            int damage = unchecked(-(returned.RereadHp - input.Begin.OldHp));
            record = new(damage, 0, damage, Ratio(returned.TargetRatioCurrentHp, returned.TargetRatioMaximumHp),
                Ratio(returned.CasterRatioCurrentHp, returned.CasterRatioMaximumHp),
                returned.CasterVirtual5e8, returned.TargetVirtual5e8);
            operations.Add(new("AppendSkillDamageInfo", damage, 0, null, returned.Stamp.Evidence));
        }
        else if (input.Returned is not null) throw new ArgumentException("False Heal return has no record getter.");
        // BeginEffect records requested amount even when Heal returns false.
        int minimum = Math.Min(input.Begin.ActionMinimumRequested, requested);
        int maximum = Math.Max(input.Begin.ActionMaximumRequested, requested);
        if (input.Begin.ActionMaximumRequested < requested)
            operations.Add(new("UpdateRequestedMaximum", maximum, null, null, input.Begin.Stamp.Evidence));
        if (requested < input.Begin.ActionMinimumRequested)
            operations.Add(new("UpdateRequestedMinimum", minimum, null, null, input.Begin.Stamp.Evidence));
        return new(reserved, requested, cell, hp, healed, minimum, maximum, action, threshold,
            invocation, activeWrite, metadataWrite, record, calculations.ToImmutable(), operations.ToImmutable());
    }

    // Complete selected no-player-special/no-caster-boost arithmetic. These helpers do
    // not bind arbitrary records to a supported skill or authorize their activation.
    internal static int CalculateNoBoostBase(short level, int r1, int r2) =>
        F32.Cvtt(F32.Integer(unchecked(level * r1 + r2)));
    internal static int CalculateNoBoostReserved(short level, int r1, int r2, int r6, int maximum)
    {
        int value = CalculateNoBoostBase(level, r1, r2);
        return r6 == 1 ? F32.Cvtt(F32.Multiply(F32.Divide100(F32.Integer(maximum)), F32.Integer(value))) : value;
    }
    internal static int CalculateRequested(int reserved, float multiplier)
    {
        if (!float.IsFinite(multiplier) || float.IsSubnormal(multiplier))
            throw new ArgumentException("Unknown/nonfinite/denormal target multiplier is outside the admitted arithmetic domain.");
        return F32.Cvtt(F32.Multiply(F32.Integer(reserved), BitConverter.SingleToUInt32Bits(multiplier)));
    }
    internal static int CalculateThreshold(int maximum, int percentage) =>
        F32.Cvtt(F32.Divide100(F32.Integer(unchecked(maximum * percentage))));
    private static byte Ratio(int hp, int maximum) => hp > 0 && maximum > 0
        ? unchecked((byte)((long)hp * 99 / maximum + 1)) : (byte)0;

    private static void ValidateContext(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.MainSha256 != MainSha256 || context.DatasetSha256 != DatasetSha256 || context.Region != Region)
            throw new ArgumentException("Pinned Main, dataset and region identities are required.");
        Text(context.ActionId); Text(context.Evidence); ActorIdentity(context.Caster); ActorIdentity(context.Target);
        if (context.Caster.ObjectId == context.Target.ObjectId && context.Caster != context.Target)
            throw new ArgumentException("One object ID cannot carry two actor lifetimes or domain receipts.");
    }
    private static void ActorIdentity(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        Text(actor.Generation); Text(actor.Evidence);
        if (actor.ObjectId == 0 || actor.Vtable != OrdinaryNpcVtable || actor.MasterObjectId != 0
            || actor.SourceRelatedObjectId != 0 || actor.VirtualProfilePresent != false)
            throw new ArgumentException("Explicit ordinary NPC vtable and independent owner/profile absence are required.");
    }
    private static void ValidateSkill(SkillSource source)
    {
        ArgumentNullException.ThrowIfNull(source); Text(source.Evidence);
        bool identity = source.Id switch
        {
            16836 => source.Devname == "NCL_HealSS_Tree" && source.R2 == 20
                && source.RawRecordSha256 == "4211edff9c4d97c5a2e36f844c607df06709404d1766344a0f4cda499f0ca6d9"
                && source.ByteStart == 47345450 && source.ByteEndExclusive == 47349608,
            16588 => source.Devname == "NEL_PetHeal_Nr" && source.R2 == 25
                && source.RawRecordSha256 == "b1827888d8d816f7be2305c3ea45ae93f9a37b0ba7c241b3459aa421b74a8825"
                && source.ByteStart == 45891980 && source.ByteEndExclusive == 45896144,
            _ => false
        };
        if (!identity || source.FileSha256 != SkillFileSha256 || source.EffectIndex != 0
            || source.EffectType != "Heal_Instant" || source.NoResist != 1 || source.ApplyHealBoost != 0
            || source.R1 != 0 || source.R6 != 1)
            throw new ArgumentException("Selected no-boost pilot source record is missing, conflicting or unsupported.");
    }
    private static void Skill(ThresholdSkill skill)
    {
        Text(skill.Identity); Text(skill.Evidence);
        if (skill.SkillId148 <= 0) throw new ArgumentException("Configured original skill identity is required.");
    }
    private static void Text(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Explicit evidence/identity required.");
    }

    // Small bounded software implementation of only the recovered binary32 stages.
    // Integer conversion, /100, multiplication and CVTT use nearest-even bits without
    // touching process MXCSR, relying on ambient host rounding, or allowing FMA.
    private static class F32
    {
        internal static uint Integer(int value) => Round(new BigInteger(value), BigInteger.One);
        internal static uint Divide100(uint bits)
        {
            if ((bits & 0x7f800000) == 0x7f800000) return bits;
            var (n, d) = Fraction(bits); return Round(n, d * 100);
        }
        internal static uint Multiply(uint left, uint right)
        {
            uint sign = (left ^ right) & 0x80000000;
            bool li = (left & 0x7f800000) == 0x7f800000, ri = (right & 0x7f800000) == 0x7f800000;
            if (li || ri)
                return (left & 0x7fffffff) == 0 || (right & 0x7fffffff) == 0
                    ? 0x7fc00000 : sign | 0x7f800000;
            var (ln, ld) = Fraction(left); var (rn, rd) = Fraction(right);
            return ln.IsZero || rn.IsZero ? sign : Round(ln * rn, ld * rd);
        }
        internal static int Cvtt(uint bits)
        {
            if ((bits & 0x7f800000) == 0x7f800000) return int.MinValue;
            var (n, d) = Fraction(bits); BigInteger value = n / d;
            return value < int.MinValue || value > int.MaxValue ? int.MinValue : (int)value;
        }
        private static (BigInteger N, BigInteger D) Fraction(uint bits)
        {
            int e = (int)((bits >> 23) & 255), shift = e == 0 ? -149 : e - 150;
            BigInteger n = e == 0 ? bits & 0x7fffff : (bits & 0x7fffff) | 0x800000;
            if ((bits & 0x80000000) != 0) n = -n;
            return shift >= 0 ? (n << shift, BigInteger.One) : (n, BigInteger.One << -shift);
        }
        private static uint Round(BigInteger n, BigInteger d)
        {
            if (n.IsZero) return 0;
            uint sign = n.Sign < 0 ? 0x80000000u : 0;
            n = BigInteger.Abs(n);
            int e = checked((int)(n.GetBitLength() - d.GetBitLength()));
            if (e >= 0 ? n < (d << e) : (n << -e) < d) e--;
            int shift = e < -126 ? 149 : 23 - e;
            BigInteger numerator = shift >= 0 ? n << shift : n;
            BigInteger denominator = shift >= 0 ? d : d << -shift;
            BigInteger q = BigInteger.DivRem(numerator, denominator, out BigInteger r);
            int compare = (r << 1).CompareTo(denominator);
            if (compare > 0 || compare == 0 && !q.IsEven) q++;
            if (e < -126) return sign | (uint)q;
            if (q >= 0x1000000) { q >>= 1; e++; }
            if (e > 127) return sign | 0x7f800000;
            return sign | ((uint)(e + 127) << 23) | ((uint)q & 0x7fffff);
        }
    }
}
