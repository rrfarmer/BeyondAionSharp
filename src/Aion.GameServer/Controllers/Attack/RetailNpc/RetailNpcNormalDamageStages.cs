using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

internal readonly record struct RetailNpcBaseDamageStages(
    float SummedRate, int SummedFlat, float BeforeMinimum, float AfterMinimum, float AfterMovement);

internal readonly record struct RetailNpcPhysicalDamageStages(
    float AfterLevel, int MovementAdjustedDefense, float ArmorTerm, float BeforeClamp, float Damage);

internal readonly record struct RetailNpcElementalDamageStages(
    float AfterLevel, int MovementAdjustedMagicalDefense, float AfterMagicalDefense,
    int NormalizedElementalDefense, int ClampedElementalDefense, float ElementalDenominator,
    float ElementalMultiplier, float Damage);

/// <summary>
/// Pure, unrouted arithmetic stages from the pinned Main binary; not a full attack calculator.
/// Source: Creature::CalcNormalAttackDamage 0x140369e10 and
/// CheckNormalAttackDamageResist 0x14036a5b0; helpers 0x14036b890/0x14036bbb0/0x14036beb0.
/// Exact saved source, instruction bytes, static vectors and limits:
/// C:/Users/ryanf/Documents/Aion-NPC-Discovery/implementation/phase3/normal-calculator/.
///
/// Callers must supply actual effective fields and loaded movement/table entries. These
/// methods do not construct stats, resolve overrides, choose status, draw RNG, apply effects,
/// emit packets, truncate final attack damage or change HP. Grouping arithmetic here does
/// not move native status calls: retain the original consumer's branch/call order at routing.
/// </summary>
internal static class RetailNpcNormalDamageStages
{
    /// <summary>
    /// Ordinary NPC per-hand base with no weapon-booster endpoint branch. Native adds
    /// flat terms as int32 (wrapping), then converts to binary32. The selected profile
    /// rate is already attack_mod/100 plus its effective functions, not raw attack_mod.
    /// movementAttackPercent is the loaded DAT_141069ed0 row value, not a movement enum.
    /// </summary>
    internal static RetailNpcBaseDamageStages CalculateNpcBase(RetailNpcNormalScope scope,
        RetailNpcNormalRange range, int? originalSample, float effectiveProfileRate,
        float effectiveAdditiveRate, ushort flatWord, int flatInt, int movementAttackPercent)
    {
        RequireScope(scope);
        scope.RequireNpcAttacker();
        ArgumentNullException.ThrowIfNull(range);
        RequireFinite(effectiveProfileRate, nameof(effectiveProfileRate));
        RequireFinite(effectiveAdditiveRate, nameof(effectiveAdditiveRate));
        int sample = range.ResolveOriginalSample(originalSample);
        float rate = Add(effectiveAdditiveRate, effectiveProfileRate);
        int flat = unchecked(flatInt + flatWord);
        float beforeMinimum = Add(Multiply(sample, rate), flat);
        float afterMinimum = beforeMinimum < 1f ? 1f : beforeMinimum;
        float movementBonus = Multiply(Divide(movementAttackPercent, 100f), afterMinimum);
        return new(rate, flat, beforeMinimum, afterMinimum, Add(movementBonus, afterMinimum));
    }

    /// <summary>
    /// Full arithmetic of 0x14036b890 for supported ownerless NPC-facing contexts and
    /// player levels1..100 and NPC levels0..100. playerFlag8024 is the explicit raw native player flag, not an inferred
    /// game-mode label. It must be false for an NPC attacker. Owned/scaled targets are rejected
    /// by the scope rather than approximated. This helper does not determine hit status.
    /// </summary>
    internal static float ApplyLevelRule(RetailNpcNormalScope scope, float damage,
        int attackerLevel, int defenderLevel, bool playerFlag8024)
    {
        RequireScope(scope);
        RequireFinite(damage, nameof(damage));
        RequireLevel(attackerLevel, nameof(attackerLevel),scope.Attacker);
        RequireLevel(defenderLevel, nameof(defenderLevel),scope.Defender);
        if (scope.Attacker != RetailNpcNormalActorKind.Player && playerFlag8024)
            throw new ArgumentException("The player-only native flag is invalid for an NPC attacker.", nameof(playerFlag8024));
        if (scope.Defender != RetailNpcNormalActorKind.OrdinaryOwnerlessNpc)
            return damage;
        int grace = playerFlag8024 ? 3 : 2;
        int excess = defenderLevel - attackerLevel - grace;
        if (excess <= 0)
            return damage;
        float factor = Subtract(1f, Multiply(excess, 0.1f));
        return factor < 0f ? 0f : Multiply(damage, factor);
    }

    /// <summary>
    /// Fixed order: level helper, movement-adjusted PDef truncated to int32, /10,
    /// subtraction, divide the whole remainder by defender stat_ratio/1000, clamp at zero.
    /// effectivePhysicalDefense must already resolve native player overrides when applicable.
    /// Does not execute the subsequent active-defense/status/critical consumer branches.
    /// </summary>
    internal static RetailNpcPhysicalDamageStages CalculatePhysicalDefense(RetailNpcNormalScope scope,
        float damageBeforeLevel, int attackerLevel, int defenderLevel, bool playerFlag8024,
        uint effectivePhysicalDefense, int movementDefensePercent, uint defenderStatRatio)
    {
        float afterLevel = ApplyLevelRule(scope, damageBeforeLevel, attackerLevel, defenderLevel, playerFlag8024);
        RequireStatRatio(defenderStatRatio, nameof(defenderStatRatio));
        int adjustedDefense = MovementAdjustedDefense(effectivePhysicalDefense, movementDefensePercent);
        float armor = Divide(adjustedDefense, 10f);
        float remainder = Subtract(afterLevel, armor);
        float beforeClamp = Divide(remainder, Divide(defenderStatRatio, 1000f));
        return new(afterLevel, adjustedDefense, armor, beforeClamp, beforeClamp < 0f ? 0f : beforeClamp);
    }

    /// <summary>
    /// Elemental normal defense: level, truncated movement MDef /10, clamp zero;
    /// signed-word element plus movement element entry (int32 wrapping), normalized by
    /// attacker stat_ratio, truncated and floored, then linear resistance multiplication.
    /// For a player target minimumElementalDefense is the actual loaded
    /// DAT_145a28930[defenderLevel]; for an ordinary NPC it must be explicitly -1300.
    /// This method does not choose a floor/default for an unresolved player table.
    /// </summary>
    internal static RetailNpcElementalDamageStages CalculateElementalDefense(RetailNpcNormalScope scope,
        float damageBeforeLevel, int attackerLevel, int defenderLevel, bool playerFlag8024,
        uint effectiveMagicalDefense, int movementDefensePercent, short effectiveElementalDefense,
        int movementElementalEntry, uint attackerStatRatio, int minimumElementalDefense)
    {
        float afterLevel = ApplyLevelRule(scope, damageBeforeLevel, attackerLevel, defenderLevel, playerFlag8024);
        RequireStatRatio(attackerStatRatio, nameof(attackerStatRatio));
        if (scope.Defender == RetailNpcNormalActorKind.OrdinaryOwnerlessNpc && minimumElementalDefense != -1300)
            throw new ArgumentException("The ordinary NPC branch requires its explicit -1300 floor.", nameof(minimumElementalDefense));
        int adjustedDefense = MovementAdjustedDefense(effectiveMagicalDefense, movementDefensePercent);
        float afterMagicalDefense = Subtract(afterLevel, Divide(adjustedDefense, 10f));
        if (afterMagicalDefense < 0f)
            afterMagicalDefense = 0f;
        int elementSum = unchecked(effectiveElementalDefense + movementElementalEntry);
        int normalizedElement = TruncateInt32(Divide(elementSum, Divide(attackerStatRatio, 1000f)));
        int clampedElement = Math.Max(normalizedElement, minimumElementalDefense);
        int extraDenominator = scope.Defender == RetailNpcNormalActorKind.Player && attackerLevel > 50 && defenderLevel > 50
            ? (Math.Min(attackerLevel, defenderLevel) - 50) * 10 : 0;
        float denominator = Add(extraDenominator, 1300f);
        float multiplier = Subtract(1f, Divide(clampedElement, denominator));
        return new(afterLevel, adjustedDefense, afterMagicalDefense, normalizedElement, clampedElement,
            denominator, multiplier, Multiply(multiplier, afterMagicalDefense));
    }

    /// <summary>
    /// Ordinary NPC normal critical multiplier only, after a caller has resolved the
    /// critical branch. Physical base is 1.5; elemental base is 1. Skill critical
    /// coefficients and player weapon tables are outside this method. The reduction
    /// input is the unsigned-word field domain; wider player override values are not
    /// silently narrowed and require a separately verified adapter/consumer extension.
    /// </summary>
    internal static float CalculateNpcCriticalMultiplier(RetailNpcNormalScope scope,
        bool physical, ushort effectiveCriticalDamageReduction, uint attackerStatRatio)
    {
        RequireScope(scope);
        scope.RequireNpcAttacker();
        RequireStatRatio(attackerStatRatio, nameof(attackerStatRatio));
        int normalizedReduction = TruncateInt32(Divide(effectiveCriticalDamageReduction, Divide(attackerStatRatio, 1000f)));
        float factor = Subtract(physical ? 1.5f : 1f, Divide(normalizedReduction, 1000f));
        return factor <= 1f ? 1f : factor;
    }

    /// <summary>
    /// Complete pure helper 0x14036beb0: clamp each signed input to +/-10000, divide
    /// separately by 1000, add attack to one, subtract defense, then clamp [0.1,12].
    /// Callers still own classification, typed/generic stat construction and application order.
    /// </summary>
    internal static float CalculatePveRatio(RetailNpcNormalScope scope, int effectiveAttackRatio, int effectiveDefenseRatio)
    {
        RequireScope(scope);
        float attack = Divide(Math.Clamp(effectiveAttackRatio, -10000, 10000), 1000f);
        float defense = Divide(Math.Clamp(effectiveDefenseRatio, -10000, 10000), 1000f);
        float ratio = Subtract(Add(attack, 1f), defense);
        if (ratio < 0.1f) return 0.1f;
        return ratio > 12f ? 12f : ratio;
    }

    private static int MovementAdjustedDefense(uint defense, int percent)
    {
        float defenseFloat = defense;
        return TruncateInt32(Add(Multiply(Divide(percent, 100f), defenseFloat), defenseFloat));
    }

    private static int TruncateInt32(float value)
    {
        // CVTTSS2SI's indefinite/out-of-range behavior is outside this supported domain.
        // int.MaxValue rounds to 2^31 as float, so comparison must use the exclusive bound.
        if (!float.IsFinite(value) || value < -2147483648f || value >= 2147483648f)
            throw new ArithmeticException("The native conversion is outside the supported representable int32 domain.");
        return (int)value;
    }

    private static void RequireScope(RetailNpcNormalScope scope) => ArgumentNullException.ThrowIfNull(scope);

    private static void RequireStatRatio(uint ratio, string name)
    {
        if (ratio < 1000 || ratio > 65000)
            throw new ArgumentOutOfRangeException(name, ratio, "Effective stat_ratio must be in its verified construction range 1000..65000.");
    }

    private static void RequireLevel(int level, string name,RetailNpcNormalActorKind actor)
    {
        int minimum=actor==RetailNpcNormalActorKind.OrdinaryOwnerlessNpc?0:1;
        if (level < minimum || level > 100)
            throw new ArgumentOutOfRangeException(name, level, "Player levels1..100 or source NPC levels0..100 required.");
    }

    private static void RequireFinite(float value, string name)
    {
        if (!float.IsFinite(value))
            throw new ArgumentOutOfRangeException(name, value, "Non-finite native arithmetic is outside this unit.");
    }

    private static float FiniteResult(float value)
    {
        if (!float.IsFinite(value))
            throw new ArithmeticException("Intermediate arithmetic exceeds the supported finite binary32 domain.");
        return value;
    }

    // Separate non-inlined scalar operations preserve the recovered SSE rounding points;
    // callers must not replace these stages with reassociated expressions or fused multiply-add.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static float Add(float left, float right) => FiniteResult(left + right);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Subtract(float left, float right) => FiniteResult(left - right);
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static float Multiply(float left, float right) => FiniteResult(left * right);
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static float Divide(float left, float right) => FiniteResult(left / right);
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static float FromInt(int value) => FiniteResult(value);
}
