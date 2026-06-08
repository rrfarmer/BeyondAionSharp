using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Model.Stats.Calc.Functions;

/// <summary>
/// Base stat modifier (a "SimpleModifier" in skill/item XML).
/// Java parity: model/stats/calc/functions/StatFunction (@XmlType("SimpleModifier")).
/// </summary>
[XmlType("SimpleModifier")]
public class StatFunction : IStatFunction
{
    [XmlAttribute("name")] protected StatEnum Stat;
    [XmlAttribute("bonus")] private bool _bonus;
    [XmlAttribute("value")] protected int Value;
    [XmlElement("conditions")] private Conditions? _conditions;

    public StatFunction() { }

    public StatFunction(StatEnum stat, int value, bool bonus)
    {
        Stat = stat;
        Value = value;
        _bonus = bonus;
    }

    // Java parity: getOwner()
    public virtual IStatOwner? GetOwner() => null;

    // Java parity: final getName()
    public StatEnum GetName() => Stat;

    // Java parity: final isBonus()
    public bool IsBonus() => _bonus;

    /// <remarks>
    /// priorities RATE 20 ADD 30 SUB 30 SET 40 RATE bonus 50 ADD bonus 60 SUB bonus 60 SET bonus 70
    /// ABS 80 ABS debuff 90 ABS bonus 100 ABS debuff bonus 110
    /// </remarks>
    // Java parity: getPriority()
    public virtual int GetPriority() => 0x10;

    // Java parity: getValue()
    public int GetValue() => Value;

    // Java parity: validate(Stat2)
    public bool Validate(Stat2 stat) => _conditions == null || _conditions.Validate(stat, this);

    // Java parity: protected validate(Stat2, IStatFunction) — same-package -> internal.
    internal bool Validate(Stat2 stat, IStatFunction statFunction) => _conditions == null || _conditions.Validate(stat, statFunction);

    // Java parity: apply(Stat2, CalculationType...)
    public virtual void Apply(Stat2 stat, params CalculationType[] calculationTypes) { }

    // Java parity: toString()
    public override string ToString() => "stat=" + Stat + ", bonus=" + _bonus + ", value=" + Value + ", priority=" + GetPriority();

    // Java parity: withConditions(Conditions)
    public StatFunction WithConditions(Conditions conditions)
    {
        _conditions = conditions;
        return this;
    }

    // Java parity: hasConditions()
    public bool HasConditions() => _conditions != null;

    // Java parity: default compareTo
    public int CompareTo(IStatFunction? o) => GetPriority() - o!.GetPriority();
}
