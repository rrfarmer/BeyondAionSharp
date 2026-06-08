using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>Java parity: model/stats/calc/functions/StatAbsFunction.</summary>
public class StatAbsFunction : StatFunction
{
    private readonly bool _debuff;

    public StatAbsFunction() { }

    public StatAbsFunction(StatEnum name, int value, bool debuff) : base(name, value, false)
    {
        _debuff = debuff;
    }

    // Java parity: apply(Stat2, CalculationType...)
    public override void Apply(Stat2 stat, params CalculationType[] calculationTypes)
    {
        if (!IsBonus())
        {
            stat.SetBase(GetValue());
            stat.SetBonus(0);
            stat.SetBaseRate(1f);
        }
        // what to do with bonus?
    }

    // Java parity: final getPriority()
    public sealed override int GetPriority()
    {
        if (_debuff)
            return IsBonus() ? 110 : 90;
        return IsBonus() ? 100 : 80;
    }

    public override string ToString() => "StatAbsFunction [" + base.ToString() + "]";
}
