using Aion.GameServer.Model.Stats.Calc.Functions;

namespace Aion.GameServer.Model.Stats.Calc;

/// <summary>
/// Validates whether a stat function should be applied to a stat.
/// Java parity: model/stats/calc/StatCondition.
/// </summary>
public interface StatCondition
{
    // Java parity: validate(Stat2, IStatFunction)
    bool Validate(Stat2 stat, IStatFunction statFunction);
}
