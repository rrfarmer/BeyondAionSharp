using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>
/// A stat modifier function applied to a <see cref="Stat2"/>.
/// Java parity: model/stats/calc/functions/IStatFunction (extends Comparable&lt;IStatFunction&gt;).
/// </summary>
public interface IStatFunction : IComparable<IStatFunction>
{
    // Java parity: getName()
    StatEnum GetName();

    // Java parity: isBonus()
    bool IsBonus();

    // Java parity: getPriority()
    int GetPriority();

    // Java parity: getValue()
    int GetValue();

    // Java parity: validate(Stat2)
    bool Validate(Stat2 stat);

    // Java parity: apply(Stat2, CalculationType...)
    void Apply(Stat2 stat, params CalculationType[] calculationTypes);

    // Java parity: getOwner()
    IStatOwner? GetOwner();

    // Java parity: hasConditions()
    bool HasConditions();

    // Java parity: default compareTo(IStatFunction) — by priority.
    int IComparable<IStatFunction>.CompareTo(IStatFunction? o) => GetPriority() - o!.GetPriority();
}
