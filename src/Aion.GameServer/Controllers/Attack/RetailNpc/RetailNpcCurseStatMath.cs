using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Offline numerical projection of the ordinary retail User maximum-stat and
/// mode-2 current-stat branches. Inputs are explicitly supplied native-stage
/// values, not a claim that a 4.8 player's equipment or live state matches 5.8.
/// This calculation does not activate a gameplay effect.
/// </summary>
internal static class RetailNpcCurseStatMath
{
    internal enum Resource { Hp, Mp }
    internal readonly record struct ItemResourceBonus(int Points,int Percent);

    // MainServer64 User construction and ChangeClass copy the same base six
    // race values (100 in both supported races), then add the native class
    // table. These are initial values only, before any active modifiers.
    private static readonly sbyte[] InitialHealthDelta=[10,15,0,0,0,0,-10,-10,-10,-5,10,5,0,0,5,-5,0];
    private static readonly sbyte[] InitialWillDelta=[-10,-10,5,-10,-10,-10,15,10,15,10,10,10,-10,5,0,10,10];
    internal static (ushort Health,ushort Will) InitialClassPrimary(int nativeClass)
    {
        if((uint)nativeClass >= InitialHealthDelta.Length)throw new ArgumentOutOfRangeException(nameof(nativeClass));
        return ((ushort)(100+InitialHealthDelta[nativeClass]),(ushort)(100+InitialWillDelta[nativeClass]));
    }

    // Ordinary User::CalcFirstLevelAttr, before absolute overrides or special
    // state: signed percent is applied to the initial primary, then flat is
    // added and the result is clamped. Inputs remain explicitly supplied.
    internal static ushort OrdinaryPrimary(ushort initial,int percent,int flat)
        => (ushort)Math.Clamp((long)initial+(long)initial*percent/100+flat,80L,999L);

    // Explicit native-stage inputs only: the caller must establish that every
    // modifier belongs to an active, source-reviewed effect. This projection
    // does not read player state or admit a live effect. GetBonusAttrTo adds
    // both channels with 32-bit arithmetic before CalcFirstLevelAttr runs.
    internal enum PrimaryAttribute { Health, Will, All, StrengthHealth, KnowledgeWill }
    internal readonly record struct PrimaryModifier(PrimaryAttribute Attribute,int Percent,int Flat);
    internal static (ushort Health,ushort Will) OrdinaryPrimaries(ushort initialHealth,ushort initialWill,
        ReadOnlySpan<PrimaryModifier> modifiers)
    {
        int healthPercent=0,healthFlat=0,willPercent=0,willFlat=0;
        foreach(var modifier in modifiers)
        {
            bool health=modifier.Attribute is PrimaryAttribute.Health or PrimaryAttribute.All or PrimaryAttribute.StrengthHealth;
            bool will=modifier.Attribute is PrimaryAttribute.Will or PrimaryAttribute.All or PrimaryAttribute.KnowledgeWill;
            if(!health&&!will)throw new ArgumentOutOfRangeException(nameof(modifiers),"Unknown retail primary attribute");
            if(health) { healthPercent=unchecked(healthPercent+modifier.Percent);healthFlat=unchecked(healthFlat+modifier.Flat); }
            if(will) { willPercent=unchecked(willPercent+modifier.Percent);willFlat=unchecked(willFlat+modifier.Flat); }
        }
        return (OrdinaryPrimary(initialHealth,healthPercent,healthFlat),OrdinaryPrimary(initialWill,willPercent,willFlat));
    }

    /// <summary>Project the native item-bonus revision for HP or MP when the
    /// caller has already established the item level, actor level and revision
    /// branch. Points and percentages are revised separately. This does not
    /// establish equipment eligibility or activate any live player effect.</summary>
    internal static ItemResourceBonus ReviseItemBonus(Resource resource,ItemResourceBonus bonus,ushort itemLevel,
        ushort playerLevel,bool revisionEnabled)
    {
        if(resource is not (Resource.Hp or Resource.Mp))throw new ArgumentOutOfRangeException(nameof(resource));
        int difference=itemLevel-playerLevel;
        if(!revisionEnabled||difference<=0)return bonus;
        difference=Math.Min(difference,49);
        float ratio=RetailNpcUserMagicalInputs.Div(RetailNpcUserMagicalInputs.FromInt(difference*2),100f);
        int Revise(int value)
        {
            float original=RetailNpcUserMagicalInputs.FromInt(value);
            return RetailNpcUserMagicalInputs.Cvtt(RetailNpcUserMagicalInputs.Sub(original,
                RetailNpcUserMagicalInputs.Mul(ratio,original)));
        }
        return new(Revise(bonus.Points),Revise(bonus.Percent));
    }

    // MainServer User::CalcSecondLevelAttr 0x140353189..35325c. These are
    // the native 17-entry class multiplier tables, not a mapping from a
    // live 4.8 player or a complete equipped-stat calculation.
    private static readonly float[] InitialHpRatios =
        [1f,1.1f,1.15f,0.9f,0.9f,0.7f,0.65f,0.65f,0.7f,0.9f,0.8f,0.9f,0.9f,1.05f,0.9f,0.8f,0.8f];
    private static readonly float[] InitialMpRatios =
        [1f,1f,1f,1f,1f,1f,1.5f,1.5f,1.5f,1.5f,1.5f,1.5f,1f,1.2f,1f,1.5f,1.3f];

    /// <summary>Native initial percentage base before any equipment or other
    /// source contributions. The class index and primary stat are explicit
    /// native inputs; this method does not assert player-state equivalence.</summary>
    internal static int InitialBase(Resource resource,ushort level,ushort primary,int nativeClassIndex)
    {
        if ((uint)nativeClassIndex >= InitialHpRatios.Length)
            throw new ArgumentOutOfRangeException(nameof(nativeClassIndex));
        var (quadratic,linear,constant,ratio) = resource switch
        {
            Resource.Hp => (1.15f,43f,120f,InitialHpRatios[nativeClassIndex]),
            Resource.Mp => (0.005f,70f,60f,InitialMpRatios[nativeClassIndex]),
            _ => throw new ArgumentOutOfRangeException(nameof(resource))
        };
        float n=RetailNpcUserMagicalInputs.FromInt(level);
        float square=RetailNpcUserMagicalInputs.Mul(n,n);
        float value=RetailNpcUserMagicalInputs.Mul(square,quadratic);
        value=RetailNpcUserMagicalInputs.Add(value,RetailNpcUserMagicalInputs.Mul(n,linear));
        value=RetailNpcUserMagicalInputs.Add(value,constant);
        value=RetailNpcUserMagicalInputs.Add(value,RetailNpcUserMagicalInputs.FromInt(Math.Max(0,primary-80)*4));
        return RetailNpcUserMagicalInputs.Cvtt(RetailNpcUserMagicalInputs.Mul(value,ratio));
    }

    /// <summary>Combine explicit native revised-armor HP values with the
    /// initial base. The ordinary MP percentage base does not add them. The
    /// caller is responsible for item identity, eligibility and revision.</summary>
    internal static int PercentageBase(Resource resource,int initial,ReadOnlySpan<int> revisedArmorHp)
    {
        if(resource==Resource.Mp)return initial;
        if(resource!=Resource.Hp)throw new ArgumentOutOfRangeException(nameof(resource));
        int result=initial;
        foreach(int contribution in revisedArmorHp)result=unchecked(result+contribution);
        return result;
    }

    internal static int Maximum(Resource resource, int percentageBase, int unscaledTotal, int aggregatePercent)
    {
        if (percentageBase < 0 || unscaledTotal < 0)
            throw new ArgumentOutOfRangeException(nameof(percentageBase), "Nonnegative explicit native-stage inputs required.");
        long scaled = (long)percentageBase * aggregatePercent / 100;
        long sum = scaled + unscaledTotal;
        return (int)Math.Clamp(sum, resource == Resource.Hp ? 100L : 1L, 2_000_000_000L);
    }

    // Retail User::CalcSecondLevelAttr 0x1403575ce/0x1403578db. Only the
    // mode-2 ratio branch, with its suppression flag clear, is modeled here.
    internal static int CurrentMode2(Resource resource, int oldMaximum, int newMaximum, int current)
    {
        if (oldMaximum < 0 || newMaximum <= 0 || current < 0)
            throw new ArgumentOutOfRangeException(nameof(oldMaximum), "Nonnegative current and valid maxima required.");
        if (oldMaximum == 0 || oldMaximum == newMaximum) return current;
        if (resource == Resource.Hp && current == 0) return 0;
        float ratio = Divide(unchecked(newMaximum - oldMaximum), oldMaximum);
        float delta = Multiply(current, ratio);
        if (!float.IsFinite(delta) || delta < -2147483648f || delta >= 2147483648f)
            throw new ArithmeticException("Unrepresentable retail conversion is outside this projection.");
        int result = unchecked(current + (int)delta);
        return Math.Clamp(result, resource == Resource.Hp ? 1 : 0, newMaximum);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float numerator, float denominator) => numerator / denominator;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float left, float right) => left * right;
}
