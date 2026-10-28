using System.Runtime.CompilerServices;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPeriodicSpellParameters.Family;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Pure periodic arithmetic children, not Check/Begin, a tick owner or HP writer.
/// bbe750/bbe990/bbeb40/bbed40 and Bleed bdb9f0/bdbb80/bdbcf0 are complete PE-checked
/// bodies. Actual source/current getter selection, call order, critical/resist RNG,
/// current level reads, PvE/HP2, exclusives, lifecycle and dispatch remain owned by
/// the later full consumer. These helpers never certify caller-supplied stats.</summary>
internal static class RetailNpcPeriodicDamageStages
{
    internal sealed record Boost(int? Difference, int? CappedDifference, int? Normalized,
        int? CappedNormalized, float? Multiplier, float? Product, int Damage);
    internal sealed record Element(int? LevelContribution, float? Denominator, float? Multiplier, int Damage);
    internal sealed record Packed(uint Word, bool Critical, int DistributionCount, int BaseDamage);

    internal static int BaseDamage(short skillLevel, int r8, int r9) => unchecked(skillLevel * r8 + r9);

    internal static Boost SkillBoost(F kind, int signedBoost, int signedSuppression,
        uint defenderRatio1514, int rawCapGlobal, int damage)
    {
        Family(kind);
        if (kind is F.Bleed or F.Poison) return new(null, null, null, null, null, null, damage);
        int difference = unchecked(signedBoost - signedSuppression);
        int nonnegative = difference < 0 ? 0 : difference;
        int cap = rawCapGlobal == 0 ? 3400 : 2900;
        int capped = nonnegative > cap ? cap : nonnegative;
        // bbe8d8/bbe8dc DIVSS, bbe8e0 CVTT; the later /1000 remains floating.
        float denominator = Div(defenderRatio1514, 1000f);
        int normalized = Cvtt(Div(capped, denominator));
        int positive = normalized < 0 ? 0 : normalized;
        int cappedNormalized = positive > cap ? cap : positive;
        float multiplier = Add(Div(cappedNormalized, 1000f), 1f);
        float product = Mul(multiplier, damage);
        int result = Cvtt(product);
        return new(difference, capped, normalized, cappedNormalized, multiplier, product, result < 1 ? 1 : result);
    }

    internal static int MagicalDefense(F kind, int signedMagicalDefense1498, int damage)
    {
        Family(kind);
        if (kind is F.Bleed or F.Poison) return damage;
        int sum = unchecked(damage + Cvtt(Div(signedMagicalDefense1498, -10f)));
        return sum < 0 ? 0 : sum;
    }

    internal static Element ElementalDefense(F kind, RetailNpcNormalScope scope, short elementalDefense,
        int damage, int sourceLevel, int targetLevel)
    {
        Family(kind);
        ArgumentNullException.ThrowIfNull(scope); scope.RequireNpcAttacker();
        if (kind is F.Bleed or F.Poison) return new(null, null, null, damage);
        int contribution = 0;
        if (scope.Defender == RetailNpcNormalActorKind.Player)
        {
            Level(sourceLevel); Level(targetLevel);
            if (sourceLevel > 50 && targetLevel > 50)
                contribution = (System.Math.Min(sourceLevel, targetLevel) - 50) * 10;
        }
        float denominator = Add(contribution, 1300f);
        float multiplier = Sub(1f, Div(elementalDefense, denominator));
        return new(contribution, denominator, multiplier, Cvtt(Mul(multiplier, damage)));
    }

    internal static int NpcCritical(int damage, short skillLevel, float currentMod1,
        float currentMod2, ushort currentFortitude14cc)
    {
        Finite(currentMod1); Finite(currentMod2);
        float coefficient = Add(Mul(skillLevel, currentMod1), currentMod2);
        float multiplier = Sub(Add(coefficient, 1f), Div(currentFortitude14cc, 1000f));
        if (multiplier <= 1f) multiplier = 1f;
        return Cvtt(Mul(damage, multiplier));
    }

    internal static int SharedDamage(int damage, byte currentSharing4cc, int distributionCount)
    {
        if (distributionCount is < 0 or > 127) throw new ArgumentOutOfRangeException(nameof(distributionCount));
        return currentSharing4cc != 0 && distributionCount > 1 ? Cvtt(Div(damage, distributionCount)) : damage;
    }

    internal static Packed Pack(int rawDamage, bool calculatedCritical, ushort originalTargetCount)
    {
        // Actual Check ORs, never masks the damage. Count128 aliases bit31; retain it.
        uint word = unchecked((uint)rawDamage) | (calculatedCritical ? 0x80000000u : 0u)
            | ((uint)System.Math.Min(originalTargetCount, (ushort)128) << 24);
        return Unpack(word);
    }
    internal static Packed Unpack(uint word)
    {
        bool critical = unchecked((int)word) < 0;
        uint cleared = critical ? word & 0x7fffffffu : word;
        return new(word, critical, (int)(cleared >> 24) & 127, unchecked((int)(cleared & 0x80ffffffu)));
    }

    private static void Family(F kind)
    {
        if (kind is not F.SpellAttack and not F.Bleed and not F.Poison) throw new ArgumentOutOfRangeException(nameof(kind));
    }
    private static void Level(int level)
    {
        if (level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(level));
    }
    private static void Finite(float value)
    {
        if (!float.IsFinite(value)) throw new ArithmeticException("Exceptional floating operand is outside this finite periodic child");
    }
    private static int Cvtt(float value)
    {
        if (!float.IsFinite(value) || value < -2147483648f || value >= 2147483648f)
            throw new ArithmeticException("Unrepresentable native CVTTSS2SI is outside this finite periodic child");
        return (int)value;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sub(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Div(float a, float b) => a / b;
}
