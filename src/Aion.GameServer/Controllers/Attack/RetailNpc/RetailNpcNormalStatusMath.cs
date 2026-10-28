using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Scalar stages from pinned Main normal status arithmetic. This is not status
/// selection: callers still own original forced-effect consumption, eligibility, draw order,
/// shared hand state, callbacks, shield state, transport and HP application.
/// </summary>
/// <remarks>
/// Creature::CheckNormalAttackActiveDefend 0x14036c490; normal critical threshold in
/// CheckNormalAttackDamageResist 0x14036a5b0; ownerless level helpers 0x14036b970,
/// 0x14036ba30, 0x14036baf0 and 0x14036bc60. Effective inputs and source declarations
/// are required. The ordinary normal adapter supplies current host inputs; this
/// module itself does not look up actors or apply damage.
/// </remarks>
internal static class RetailNpcNormalStatusMath
{
    /// <summary>Calculation stages only, not native ordinals or packet status values.</summary>
    internal enum ChanceStage
    {
        Dodge,
        Block,
        Parry,
        MagicalResist,
        MainCritical
    }

    /// <summary>
    /// Declared effective-input provenance and supported scope. Validation does not attest
    /// a live actor, snapshot, native eligibility result or captured RNG stream.
    /// </summary>
    internal sealed class Context
    {
        internal RetailNpcNormalScope Scope { get; }
        internal string SourceEvidence { get; }

        private Context(RetailNpcNormalScope scope, string sourceEvidence)
        {
            Scope = scope;
            SourceEvidence = sourceEvidence;
        }

        internal static Context Create(RetailNpcNormalScope scope, string sourceMainSha256,
            string sourceEvidence, bool attackerSpecial8024)
        {
            ArgumentNullException.ThrowIfNull(scope);
            RequireMainIdentity(sourceMainSha256);
            RequireEvidence(sourceEvidence, nameof(sourceEvidence));
            if (attackerSpecial8024)
                throw new ArgumentException("The special +0x8024 paths are outside this ordinary scalar unit.", nameof(attackerSpecial8024));
            // Scope's private constructor already excludes PvP and owned/scaled/unverified actors.
            return new Context(scope, sourceEvidence);
        }
    }

    /// <summary>
    /// Actual chance operand after the original getter/level helper. Evasion must already
    /// be narrowed to low16; block/parry may retain the NPC level helper's full int32 result.
    /// No eligibility or weapon/group/element decision is inferred from these numbers.
    /// </summary>
    internal readonly record struct PhysicalInput(int AdjustedDefense, int PhysicalAccuracy,
        int DefenderMovementAddition, uint AttackerStatRatio);

    internal readonly record struct MagicalInput(int MagicalResistance, int MagicalAccuracy,
        uint AttackerStatRatio, int AttackerLevel, int DefenderLevel, byte DefenderRawCode13c);

    internal readonly record struct CriticalInput(int Critical, int CriticalResistance,
        int AttackerMovementCritical, uint DefenderStatRatio, byte DefenderRawCode13c);

    /// <summary>
    /// One original Main range-wrapper invocation. The inclusive adapter is required for
    /// these unequal endpoints; this does not specify its underlying engine-word count.
    /// </summary>
    internal readonly record struct OriginalMainDrawReceipt(string SourceMainSha256,
        ChanceStage Stage, int Minimum, int Maximum, int Result, bool InclusiveAdapterInvoked,
        string SourceEvidence);

    private static readonly float EvasionLevelStep = BitConverter.Int32BitsToSingle(0x3dcccccd);
    private static readonly float BlockParryLevelStep = BitConverter.Int32BitsToSingle(0x3e4ccccd);

    /// <summary>
    /// Complete supported ownerless-NPC defender helper and its consuming narrowing.
    /// Evasion: CVTTSS2SI then low16. Block/parry: full adjusted int32. Native does not
    /// cap this level difference at twelve. NPC constructor level0 is retained;
    /// players require1..100. Native helpers do not replace zero with level1.
    /// </summary>
    internal static int AdjustOwnerlessNpcDefense(Context context, ChanceStage stage,
        ushort effectiveDefenseWord, int attackerLevel, int defenderLevel)
    {
        RequireContext(context);
        RequirePhysicalStage(stage);
        if (context.Scope.Defender != RetailNpcNormalActorKind.OrdinaryOwnerlessNpc)
            throw new ArgumentException("This level helper requires an ordinary ownerless NPC defender.", nameof(context));
        RequireLevel(attackerLevel, nameof(attackerLevel),context.Scope.Attacker);
        RequireLevel(defenderLevel, nameof(defenderLevel),context.Scope.Defender);
        int excess = defenderLevel - attackerLevel - 2;
        if (excess <= 0)
            return effectiveDefenseWord;

        float step = stage == ChanceStage.Dodge ? EvasionLevelStep : BlockParryLevelStep;
        float multiplier = Add(Multiply(excess, step), 1f);
        int adjusted = TruncateInt32(Multiply(multiplier, effectiveDefenseWord));
        return stage == ChanceStage.Dodge ? unchecked((ushort)adjusted) : adjusted;
    }

    /// <summary>
    /// Normalize an already eligible physical-stage operand by attacker stat_ratio, then
    /// clamp to dodge 300/block 500/parry 400. No probability comparison or draw occurs here.
    /// Integer wrap outside the supported arithmetic domain is rejected, not normalized.
    /// </summary>
    internal static int CalculatePhysicalThreshold(Context context, ChanceStage stage, PhysicalInput input)
    {
        RequireContext(context);
        RequirePhysicalStage(stage);
        RequireNonnegative(input.AdjustedDefense, nameof(input.AdjustedDefense));
        RequireNonnegative(input.PhysicalAccuracy, nameof(input.PhysicalAccuracy));
        if ((stage == ChanceStage.Dodge || context.Scope.Defender == RetailNpcNormalActorKind.Player)
            && input.AdjustedDefense > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(input.AdjustedDefense), "Supply the original low16 player/evasion chance operand.");
        RequireNpcWord(context.Scope.Attacker, input.PhysicalAccuracy, nameof(input.PhysicalAccuracy));
        RequireRatio(input.AttackerStatRatio, nameof(input.AttackerStatRatio));

        // Preserve each native integer expression before its signed conversion to float.
        int numerator = stage == ChanceStage.Dodge
            ? AddInt(SubtractInt(input.AdjustedDefense, input.PhysicalAccuracy), input.DefenderMovementAddition)
            : AddInt(input.AdjustedDefense, SubtractInt(input.DefenderMovementAddition, input.PhysicalAccuracy));
        int normalized = TruncateInt32(Divide(numerator, Ratio(input.AttackerStatRatio)));
        return Math.Clamp(normalized, 0, MaximumThreshold(stage));
    }

    /// <summary>
    /// Magical weapon/group resistance branch, not a test of the selected damage element.
    /// Preserve the inner conversion before ratio normalization, clamp base 0..500, add the
    /// ordinary level offset even when resistance is zero, then clamp 0..900.
    /// </summary>
    internal static int CalculateMagicalResistThreshold(Context context, MagicalInput input)
    {
        RequireContext(context);
        RequireNonnegative(input.MagicalResistance, nameof(input.MagicalResistance));
        RequireNonnegative(input.MagicalAccuracy, nameof(input.MagicalAccuracy));
        RequireNpcWord(context.Scope.Defender, input.MagicalResistance, nameof(input.MagicalResistance));
        RequireNpcWord(context.Scope.Attacker, input.MagicalAccuracy, nameof(input.MagicalAccuracy));
        RequireRatio(input.AttackerStatRatio, nameof(input.AttackerStatRatio));
        RequireLevel(input.AttackerLevel, nameof(input.AttackerLevel),context.Scope.Attacker);
        RequireLevel(input.DefenderLevel, nameof(input.DefenderLevel),context.Scope.Defender);
        RequireRawCode(input.DefenderRawCode13c);

        int difference = SubtractInt(input.MagicalResistance, input.MagicalAccuracy);
        float factor = input.DefenderRawCode13c == 5 ? 0.5f : 1f;
        int inner = TruncateInt32(Multiply(difference, factor));
        int normalized = TruncateInt32(Divide(inner, Ratio(input.AttackerStatRatio)));
        int baseThreshold = Math.Clamp(normalized, 0, 500);
        int differenceInLevels = input.DefenderLevel - input.AttackerLevel;
        int levelOffset = differenceInLevels > 4 ? (differenceInLevels - 4) * 100
            : differenceInLevels < -4 ? (differenceInLevels + 4) * 100 : 0;
        return Math.Clamp(AddInt(baseThreshold, levelOffset), 0, 900);
    }

    /// <summary>
    /// Main-hand normal critical chance uses defender stat_ratio. Preserve its signed
    /// threshold (no native lower clamp) and upper cap 500. Critical damage reduction and
    /// multiplier composition remain the separate frozen damage-stage unit.
    /// </summary>
    internal static int CalculateMainCriticalThreshold(Context context, CriticalInput input)
    {
        RequireContext(context);
        if (input.Critical < 0 || input.Critical > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(input.Critical), "Normal critical source is an unsigned word.");
        RequireNonnegative(input.CriticalResistance, nameof(input.CriticalResistance));
        RequireNpcWord(context.Scope.Defender, input.CriticalResistance, nameof(input.CriticalResistance));
        RequireRatio(input.DefenderStatRatio, nameof(input.DefenderStatRatio));
        RequireRawCode(input.DefenderRawCode13c);
        int criticalWithMovement = AddInt(input.Critical, input.AttackerMovementCritical);
        int numerator = SubtractInt(AddInt(criticalWithMovement, input.DefenderRawCode13c == 5 ? 500 : 0),
            input.CriticalResistance);
        int threshold = TruncateInt32(Divide(numerator, Ratio(input.DefenderStatRatio)));
        return Math.Min(threshold, 500);
    }

    /// <summary>
    /// Compare a scalar threshold with one supplied original Main 1..1000 result, including
    /// zero/negative thresholds. This never requests randomness or proves stage eligibility.
    /// Callers must retain every original invocation and its branch/side-effect order.
    /// </summary>
    internal static bool CompareOriginalMainRoll(Context context, ChanceStage stage, int threshold,
        OriginalMainDrawReceipt receipt)
    {
        RequireContext(context);
        int maximum = MaximumThreshold(stage);
        if (threshold > maximum || (stage != ChanceStage.MainCritical && threshold < 0))
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Threshold is outside this stage's supported native domain.");
        RequireMainIdentity(receipt.SourceMainSha256);
        RequireEvidence(receipt.SourceEvidence, nameof(receipt.SourceEvidence));
        if (receipt.Stage != stage || receipt.Minimum != 1 || receipt.Maximum != 1000 || !receipt.InclusiveAdapterInvoked)
            throw new ArgumentException("Supply this stage's original Main 1..1000 invocation and inclusive-adapter receipt.", nameof(receipt));
        if (receipt.Result < 1 || receipt.Result > 1000)
            throw new ArgumentOutOfRangeException(nameof(receipt), receipt.Result, "Original result is outside 1..1000.");
        return receipt.Result <= threshold;
    }

    private static int MaximumThreshold(ChanceStage stage) => stage switch
    {
        ChanceStage.Dodge => 300,
        ChanceStage.Block => 500,
        ChanceStage.Parry => 400,
        ChanceStage.MagicalResist => 900,
        ChanceStage.MainCritical => 500,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown status calculation stage.")
    };

    private static void RequirePhysicalStage(ChanceStage stage)
    {
        if (stage is not (ChanceStage.Dodge or ChanceStage.Block or ChanceStage.Parry))
            throw new ArgumentOutOfRangeException(nameof(stage), stage, "This operation accepts only dodge, block or parry.");
    }

    private static void RequireContext(Context context) => ArgumentNullException.ThrowIfNull(context);
    private static void RequireMainIdentity(string identity)
    {
        if (!string.Equals(identity, RetailNpcNormalScope.MainBinarySha256, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Unsupported or unverified retail Main identity.", nameof(identity));
    }
    private static void RequireEvidence(string evidence, string name)
    {
        if (string.IsNullOrWhiteSpace(evidence))
            throw new ArgumentException("Explicit effective-input or original-call evidence is required.", name);
    }
    private static void RequireNonnegative(int value, string name)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(name, value, "The effective stat source must be nonnegative.");
    }
    private static void RequireNpcWord(RetailNpcNormalActorKind actor, int value, string name)
    {
        if (actor == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc && value > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(name, value, "Ordinary NPC effective source is an unsigned word.");
    }
    private static void RequireRatio(uint value, string name)
    {
        if (value < 1000 || value > 65000)
            throw new ArgumentOutOfRangeException(name, value, "Supply the verified effective stat_ratio in 1000..65000.");
    }
    private static void RequireLevel(int value, string name,RetailNpcNormalActorKind actor)
    {
        int minimum=actor==RetailNpcNormalActorKind.OrdinaryOwnerlessNpc?0:1;
        if (value < minimum || value > 100)
            throw new ArgumentOutOfRangeException(name, value, "Player levels1..100 or source NPC levels0..100 required.");
    }
    private static void RequireRawCode(byte value)
    {
        if (value > 15)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Supply the original low nibble, without an inferred state label.");
    }
    private static int AddInt(int left, int right) => RepresentableInt((long)left + right);
    private static int SubtractInt(int left, int right) => RepresentableInt((long)left - right);
    private static int RepresentableInt(long value)
    {
        if (value < int.MinValue || value > int.MaxValue)
            throw new ArithmeticException("Native int32 wrap is outside this supported scalar domain.");
        return (int)value;
    }
    private static int TruncateInt32(float value)
    {
        if (!float.IsFinite(value) || value < -2147483648f || value >= 2147483648f)
            throw new ArithmeticException("Native CVTTSS2SI is outside the supported representable int32 domain.");
        return (int)value;
    }
    private static float FiniteResult(float value)
    {
        if (!float.IsFinite(value))
            throw new ArithmeticException("Intermediate arithmetic exceeds the supported finite binary32 domain.");
        return value;
    }
    private static float Ratio(uint value) => Divide(value, 1000f);

    // Keep the recovered scalar rounding points separate; no reassociation or fused operations.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Add(float left, float right) => FiniteResult(left + right);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Multiply(float left, float right) => FiniteResult(left * right);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Divide(float left, float right) => FiniteResult(left / right);
}
