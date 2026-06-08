using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>
/// Wraps a stat function with an owner (so the same function instance is tracked per owner).
/// Java parity: model/stats/calc/functions/StatFunctionProxy.
/// </summary>
public class StatFunctionProxy : IStatFunction
{
    private readonly IStatOwner _owner;
    private readonly IStatFunction _proxiedFunction;

    public StatFunctionProxy(IStatOwner owner, IStatFunction statFunction)
    {
        _owner = owner;
        _proxiedFunction = statFunction;
    }

    // Java parity: getProxiedFunction()
    public IStatFunction GetProxiedFunction() => _proxiedFunction;

    // Java parity: getOwner()
    public IStatOwner GetOwner() => _owner;

    // Java parity: getName()
    public StatEnum GetName() => _proxiedFunction.GetName();

    // Java parity: isBonus()
    public bool IsBonus() => _proxiedFunction.IsBonus();

    // Java parity: getPriority()
    public int GetPriority() => _proxiedFunction.GetPriority();

    // Java parity: getValue()
    public int GetValue() => _proxiedFunction.GetValue();

    // Java parity: validate(Stat2) — delegates to the proxied StatFunction's (internal) validate.
    public bool Validate(Stat2 stat) => ((StatFunction)_proxiedFunction).Validate(stat, this);

    // Java parity: apply(Stat2, CalculationType...)
    public void Apply(Stat2 stat, params CalculationType[] calculationTypes) => _proxiedFunction.Apply(stat, calculationTypes);

    // Java parity: hasConditions()
    public bool HasConditions() => _proxiedFunction.HasConditions();

    // Java parity: compareTo (by priority)
    public int CompareTo(IStatFunction? o) => GetPriority() - o!.GetPriority();

    public override string ToString() =>
        "Proxy [name=" + _proxiedFunction.GetName() + ", bonus=" + IsBonus() + ", value=" + GetValue() + ", priority=" + GetPriority() + ", owner=" + _owner + "]";
}
