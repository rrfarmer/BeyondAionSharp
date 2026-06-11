using System.Xml.Serialization;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>Java parity: skillengine/condition/HpCondition (Tomate) : Condition. @XmlAttribute value/delta/ratio; validate: valueWithDelta=value+delta*lvl, ratio→maxHp*v/100; if currentHp>v→reduceHp(USED_HP, ..., effector); return currentHp>=v. getHpValue getter. SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("HpCondition")]
public class HpCondition : Condition
{
    [XmlAttribute]
    protected int value;
    [XmlAttribute]
    protected int delta;
    [XmlAttribute]
    protected bool ratio;

    public override bool Validate(Skill skill)
    {
        int valueWithDelta = value + delta * skill.GetSkillLevel();
        if (ratio)
            valueWithDelta = (skill.GetEffector().GetLifeStats().GetMaxHp() * valueWithDelta) / 100;
        if (skill.GetEffector().GetLifeStats().GetCurrentHp() > valueWithDelta)
            skill.GetEffector().GetLifeStats()
                .ReduceHp(SM_ATTACK_STATUS.TYPE.USED_HP, valueWithDelta, 0, SM_ATTACK_STATUS.LOG.REGULAR, skill.GetEffector());
        return skill.GetEffector().GetLifeStats().GetCurrentHp() >= valueWithDelta;
    }

    public int GetHpValue()
    {
        return value;
    }
}
