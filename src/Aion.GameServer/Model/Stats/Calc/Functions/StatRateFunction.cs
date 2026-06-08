using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>Java parity: model/stats/calc/functions/StatRateFunction.</summary>
public class StatRateFunction : StatFunction
{
    public StatRateFunction() { }

    public StatRateFunction(StatEnum name, int value, bool bonus) : base(name, value, bonus) { }

    // Java parity: apply(Stat2, CalculationType...)
    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (IsBonus())
        {
            int baseValue = stat.GetBaseWithoutBaseRate();
            if (GetName() == StatEnum.SPEED && GetValue() < 0 && stat.GetBonus() < 0) // fix to avoid run speed <= 0%
            {
                // calculate relative to current resultValue if negative, otherwise we end up with <= 0% on multiple stat functions
                baseValue = stat.GetCurrent();
            }
            stat.AddToBonus(baseValue * GetValue() / 100f);
        }
        else
        {
            stat.SetBase(stat.GetBaseWithoutBaseRate() * stat.CalculatePercent(GetValue()));
        }
    }

    // Java parity: final getPriority()
    public sealed override int GetPriority() => IsBonus() ? 50 : 20;

    public override string ToString() => "StatRateFunction [" + base.ToString() + "]";
}
