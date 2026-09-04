using System.Xml.Serialization;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>Java parity: skillengine/condition/MpCondition (ATracer) : Condition. @XmlAttribute value/delta/ratio; validate: v=value+delta*lvl, ratio→maxMp*v/100, boostSkillCost!=0→v-=(v/(100/changeMpPercent)); if currentMp>v→reduceMp(USED_MP); return currentMp>v. SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("MpCondition")]
public class MpCondition : Condition
{
    [XmlAttribute]
    public int value;

    [XmlAttribute]
    public int delta;

    [XmlAttribute]
    public bool ratio;

    public override bool Validate(Skill skill)
    {
        if (!CanValidate(skill))
            return false;
        skill.GetEffector().GetLifeStats().ReduceMp(SmAttackStatus.TYPE.USED_MP, GetCost(skill), 0, SmAttackStatus.LOG.REGULAR);
        return true;
    }

    public override bool CanValidate(Skill skill)
    {
        // npcs have no mp, so they must not be blocked by an mp cost
        if (skill.GetEffector() is Aion.GameServer.Model.GameObjects.Players.Player player && player.GetLifeStats().GetCurrentMp() < GetCost(skill))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_NOT_ENOUGH_MP());
            return false;
        }
        return true;
    }

    private int GetCost(Skill skill)
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
        return valueWithDelta;
    }
}
