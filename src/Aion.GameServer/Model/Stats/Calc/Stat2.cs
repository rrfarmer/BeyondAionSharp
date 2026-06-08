using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.Stats.Calc;

/// <summary>
/// A single computed stat (base + bonus with rates) owned by a creature.
/// Java parity: model/stats/calc/Stat2 (abstract).
/// </summary>
public abstract class Stat2
{
    protected float BonusRate;
    protected float BaseRateField = 1f;
    protected float BaseField;
    protected float BonusField;
    protected float FixedBonusRate;
    private readonly Creature _owner;
    protected readonly StatEnum Stat;

    protected Stat2(StatEnum stat, float @base, Creature owner) : this(stat, @base, owner, 1) { }

    protected Stat2(StatEnum stat, float @base, Creature owner, float bonusRate)
    {
        Stat = stat;
        BaseField = @base;
        _owner = owner;
        BonusRate = bonusRate;
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
    public void SetBase(float @base) => BaseField = @base;

    // Java parity: getBaseRate()
    public float GetBaseRate() => BaseRateField;

    // Java parity: setBaseRate(float)
    public void SetBaseRate(float rate) => BaseRateField = rate;

    // Java parity: addToBase(float)
    public abstract void AddToBase(float @base);

    // Java parity: getBonus()
    public int GetBonus() => (int)BonusField;

    // Java parity: getCurrent()
    public int GetCurrent() => (int)(BaseField * BaseRateField + BonusField * BonusRate + BaseField * FixedBonusRate);

    // Java parity: getExactCurrent()
    public float GetExactCurrent() => BaseField * BaseRateField + BonusField * BonusRate + BaseField * FixedBonusRate;

    // Java parity: getExactCurrentWithoutFixedBonus()
    public float GetExactCurrentWithoutFixedBonus() => BaseField * BaseRateField + BonusField * BonusRate;

    // Java parity: setBonus(float)
    public void SetBonus(float bonus) => BonusField = bonus;

    // Java parity: getBonusRate()
    public float GetBonusRate() => BonusRate;

    // Java parity: setBonusRate(float)
    public void SetBonusRate(float bonusRate) => BonusRate = bonusRate;

    // Java parity: addToBonus(float)
    public abstract void AddToBonus(float bonus);

    // Java parity: setFixedBonusRate(float)
    public void SetFixedBonusRate(float fixedBonusRate) => FixedBonusRate = fixedBonusRate;

    // Java parity: getFixedBonusRate()
    public float GetFixedBonusRate() => FixedBonusRate;

    // Java parity: calculatePercent(int)
    public abstract float CalculatePercent(int delta);

    // Java parity: getOwner()
    public Creature GetOwner() => _owner;

    // Java parity: toString()
    public override string ToString() => "[" + Stat + " base=" + BaseField + ", bonus=" + BonusField + "]";
}
