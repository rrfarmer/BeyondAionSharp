using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.Stats.Calc;

/// <summary>
/// Stat whose bonus subtracts and percent scales down (clamped at 0).
/// Java parity: model/stats/calc/ReverseStat.
/// </summary>
public class ReverseStat : Stat2
{
    public ReverseStat(StatEnum stat, float @base, Creature owner) : base(stat, @base, owner) { }

    public ReverseStat(StatEnum stat, float @base, Creature owner, float bonusRate) : base(stat, @base, owner, bonusRate) { }

    // Java parity: addToBase(float)
    public override void AddToBase(float @base)
    {
        BaseField -= @base;
        if (BaseField < 0)
            BaseField = 0;
    }

    // Java parity: addToBonus(float)
    public override void AddToBonus(float bonus) => BonusField -= BonusRate * bonus;

    // Java parity: calculatePercent(int)
    public override float CalculatePercent(int delta)
    {
        float percent = (100 - delta) / 100f;
        // TODO need double check here for negatives
        return percent < 0 ? 0 : percent;
    }
}
