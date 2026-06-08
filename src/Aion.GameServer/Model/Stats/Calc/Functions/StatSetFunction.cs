using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>Java parity: model/stats/calc/functions/StatSetFunction.</summary>
public class StatSetFunction : StatFunction
{
    public StatSetFunction() { }

    public StatSetFunction(StatEnum name, int value) : base(name, value, false) { }

    // Java parity: apply(Stat2, CalculationType...)
    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (IsBonus())
            stat.SetBonus(GetValue());
        else
            stat.SetBase(GetValue());
    }

    // Java parity: final getPriority()
    public sealed override int GetPriority() => IsBonus() ? 70 : 40;

    public override string ToString() => "StatSetFunction [" + base.ToString() + "]";
}
