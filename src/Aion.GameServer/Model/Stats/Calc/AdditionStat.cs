using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.Stats.Calc;

/// <summary>
/// Stat whose bonus adds and percent scales up.
/// Java parity: model/stats/calc/AdditionStat.
/// </summary>
public class AdditionStat : Stat2
{
    public AdditionStat(StatEnum stat, float @base, Creature owner) : base(stat, @base, owner) { }

    public AdditionStat(StatEnum stat, float @base, Creature owner, float bonusRate) : base(stat, @base, owner, bonusRate) { }

    // Java parity: addToBase(float)
    public sealed override void AddToBase(float @base) => BaseField += @base;

    // Java parity: addToBonus(float)
    public sealed override void AddToBonus(float bonus) => BonusField += BonusRate * bonus;

    // Java parity: calculatePercent(int)
    public override float CalculatePercent(int delta) => (100 + delta) / 100f;
}
