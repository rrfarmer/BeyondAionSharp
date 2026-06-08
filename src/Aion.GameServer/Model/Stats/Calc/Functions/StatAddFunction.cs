using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>Java parity: model/stats/calc/functions/StatAddFunction.</summary>
public class StatAddFunction : StatFunction
{
    public StatAddFunction() { }

    public StatAddFunction(StatEnum name, int value, bool bonus) : base(name, value, bonus) { }

    // Java parity: apply(Stat2, CalculationType...)
    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (IsBonus())
            stat.AddToBonus(GetValue());
        else
            stat.AddToBase(GetValue());
    }

    // Java parity: getPriority()
    public override int GetPriority() => IsBonus() ? 60 : 30;

    public override string ToString() => "StatAddFunction [" + base.ToString() + "]";
}
