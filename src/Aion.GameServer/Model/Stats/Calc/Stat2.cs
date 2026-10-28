using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Stats.Calc;

/// <summary>
/// A single computed stat (base + bonus with rates) owned by a creature.
/// Java parity: model/stats/calc/Stat2 (abstract).
/// </summary>
public abstract class Stat2
{
    protected readonly StatEnum Stat;
    private readonly Creature _owner;
    protected float BaseField;
    protected float BaseRateField = 1f;
    protected float BonusField;
    protected float BonusRate = 1f;
    protected float FixedBonusRate;
    protected float FinalRate = 1f;
    private int? exactIntegerCurrent;

    protected Stat2(StatEnum stat, float @base, Creature owner)
    {
        Stat = stat;
        _owner = owner;
        BaseField = @base;
    }

    // Java parity: getStat()
    public StatEnum GetStat() => Stat;

    // Java parity: getBase()
    public int GetBase() => (int)(BaseField * GetBaseRate());

    // Java parity: getBaseWithoutBaseRate()
    public int GetBaseWithoutBaseRate() => (int)BaseField;

    // Java parity: getExactBaseWithoutBaseRate()
    public float GetExactBaseWithoutBaseRate() => BaseField;

    // Java parity: getExactBonus()
    public virtual float GetExactBonus() => BonusField;

    // Java parity: setBase(float)
    public void SetBase(float @base) { InvalidateExactIntegerCurrent(); BaseField = @base; }

    // Java parity: getBaseRate()
    public float GetBaseRate() => BaseRateField;

    // Java parity: setBaseRate(float)
    public void SetBaseRate(float rate)
    {
        // A unity siege/AI setting leaves the native NPC result unchanged.
        // Expire its integer precision only when the operand actually changes.
        if (BaseRateField != rate) InvalidateExactIntegerCurrent();
        BaseRateField = rate;
    }

    // Java parity: addToBase(float)
    public abstract void AddToBase(float @base);

    // Java parity: getBonus()
    public int GetBonus() => (int)BonusField;

    // Java parity: getCurrent()
    public int GetCurrent()
    {
        // A retail NPC scalar can exceed the precision of float. Preserve its
        // integer result only until a subsequent AI/stat hook changes it.
        if (exactIntegerCurrent is { } exact) return exact;
        return (int)GetExactCurrent();
    }

    internal void SetExactIntegerCurrent(int value) => exactIntegerCurrent = value;

    internal bool HasUnchangedExactIntegerCurrent() => exactIntegerCurrent.HasValue;

    protected void InvalidateExactIntegerCurrent() => exactIntegerCurrent = null;

    // Java parity: getExactCurrent()
    public float GetExactCurrent() => (BaseField * BaseRateField + BonusField * BonusRate + BaseField * FixedBonusRate) * FinalRate;

    // Java parity: getExactCurrentWithoutBonus() (pulled forward from upstream f6e9a1816 - b2e1bb57c calls it but only the next commit adds it)
    public float GetExactCurrentWithoutBonus() => (BaseField * BaseRateField + BaseField * FixedBonusRate) * FinalRate;

    // Java parity: getExactCurrentWithoutFixedBonus()
    public float GetExactCurrentWithoutFixedBonus() => (BaseField * BaseRateField + BonusField * BonusRate) * FinalRate;

    // Java parity: setBonus(float)
    public void SetBonus(float bonus) { InvalidateExactIntegerCurrent(); BonusField = bonus; }

    // Java parity: getBonusRate()
    public float GetBonusRate() => BonusRate;

    // Java parity: setBonusRate(float)
    public void SetBonusRate(float bonusRate) { InvalidateExactIntegerCurrent(); BonusRate = bonusRate; }

    // Java parity: addToBonus(float)
    public abstract void AddToBonus(float bonus);

    // Java parity: setFixedBonusRate(float)
    public void SetFixedBonusRate(float fixedBonusRate) { InvalidateExactIntegerCurrent(); FixedBonusRate = fixedBonusRate; }

    // Java parity: getFixedBonusRate()
    public float GetFixedBonusRate() => FixedBonusRate;

    /// <summary>
    /// Rate applied to the final value (base and bonus alike), meant for situational penalties which are not part of the stat itself, like the physical
    /// defense loss while flying. Must be set after all stat functions have been applied, since caps are calculated without it.
    /// </summary>
    public void SetFinalRate(float finalRate) { InvalidateExactIntegerCurrent(); FinalRate = finalRate; }

    public float GetFinalRate() => FinalRate;

    // Java parity: calculatePercent(int)
    public abstract float CalculatePercent(int delta);

    // Java parity: getOwner()
    public Creature GetOwner() => _owner;

    // Java parity: toString()
    public override string ToString() => "[" + Stat + " base=" + JavaString.ValueOf(BaseField) + ", bonus=" + JavaString.ValueOf(BonusField) + "]";
}
