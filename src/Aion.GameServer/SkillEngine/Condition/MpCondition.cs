using System.Xml.Serialization;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Condition;

/// <summary>Java parity: skillengine/condition/MpCondition (ATracer) : Condition. @XmlAttribute value/delta/ratio; validate: v=value+delta*lvl, ratio→maxMp*v/100, boostSkillCost!=0→v-=(v/(100/changeMpPercent)); if currentMp>v→reduceMp(USED_MP); return currentMp>v. SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("MpCondition")]
public class MpCondition : Condition
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
            valueWithDelta = (skill.GetEffector().GetLifeStats().GetMaxMp() * valueWithDelta) / 100;
        int changeMpPercent = skill.GetBoostSkillCost();
        if (changeMpPercent != 0)
        {
            // changeMpPercent is negative
            valueWithDelta = valueWithDelta - ((valueWithDelta / ((100 / changeMpPercent))));
        }
        if (skill.GetEffector().GetLifeStats().GetCurrentMp() > valueWithDelta)
            skill.GetEffector().GetLifeStats().ReduceMp(SM_ATTACK_STATUS.TYPE.USED_MP, valueWithDelta, 0, SM_ATTACK_STATUS.LOG.REGULAR);
        return skill.GetEffector().GetLifeStats().GetCurrentMp() > valueWithDelta;
    }
}
