using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>Java parity: model/stats/calc/functions/StatSubFunction.</summary>
public class StatSubFunction : StatFunction
{
    // Java parity: apply(Stat2, CalculationType...)
    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (IsBonus())
            stat.AddToBonus(-GetValue());
        else
            stat.AddToBase(-GetValue());
    }

    // Java parity: final getPriority()
    public sealed override int GetPriority() => IsBonus() ? 60 : 30;
}
