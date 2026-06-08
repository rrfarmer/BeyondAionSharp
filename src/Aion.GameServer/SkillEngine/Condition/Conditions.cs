using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.condition;

/// <summary>
/// Java parity: skillengine/condition/Conditions (ATracer). Polymorphic XML list of conditions.
/// Java @XmlElements → C# multiple [XmlElement(name, typeof(Sub))] on the list member.
/// Subclass types are forward-refs until the condition cone is ported (WIP).
/// </summary>
public class Conditions
{
    [XmlElement("abnormal", typeof(AbnormalStateCondition))]
    [XmlElement("target", typeof(TargetCondition))]
    [XmlElement("mp", typeof(MpCondition))]
    [XmlElement("hp", typeof(HpCondition))]
    [XmlElement("dp", typeof(DpCondition))]
    [XmlElement("move_casting", typeof(PlayerMovedCondition))]
    [XmlElement("ride_robot", typeof(RideRobotCondition))]
    [XmlElement("onfly", typeof(OnFlyCondition))]
    [XmlElement("weapon", typeof(WeaponCondition))]
    [XmlElement("noflying", typeof(NoFlyingCondition))]
    [XmlElement("lefthandweapon", typeof(LeftHandCondition))]
    [XmlElement("charge", typeof(ItemChargeCondition))]
    [XmlElement("chargeweapon", typeof(ChargeWeaponCondition))]
    [XmlElement("chargearmor", typeof(ChargeArmorCondition))]
    [XmlElement("polishchargeweapon", typeof(PolishChargeCondition))]
    [XmlElement("skillcharge", typeof(SkillChargeCondition))]
    [XmlElement("targetflying", typeof(TargetFlyingCondition))]
    [XmlElement("selfflying", typeof(SelfFlyingCondition))]
    [XmlElement("combatcheck", typeof(CombatCheckCondition))]
    [XmlElement("chain", typeof(ChainCondition))]
    [XmlElement("front", typeof(FrontCondition))]
    [XmlElement("back", typeof(BackCondition))]
    [XmlElement("form", typeof(FormCondition))]
    [XmlElement("race", typeof(RaceCondition))]
    protected List<Condition>? conditions;

    /// <summary>
    /// Gets the conditions (live list; lazily created). Java parity: getConditions().
    /// </summary>
    public List<Condition> GetConditions()
    {
        if (conditions == null)
        {
            conditions = new List<Condition>();
        }
        return conditions;
    }

    public bool Validate(Skill skill)
    {
        if (conditions != null)
        {
            foreach (Condition condition in GetConditions())
            {
                if (!condition.Validate(skill))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public bool Validate(Stat2 stat, IStatFunction statFunction)
    {
        if (conditions != null)
        {
            foreach (Condition condition in GetConditions())
            {
                if (!condition.Validate(stat, statFunction))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public bool Validate(Effect effect)
    {
        if (conditions != null)
        {
            foreach (Condition condition in GetConditions())
            {
                if (!condition.Validate(effect))
                {
                    return false;
                }
            }
        }
        return true;
    }
}
