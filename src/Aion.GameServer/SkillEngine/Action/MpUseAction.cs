using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Action;

/// <summary>Java parity: skillengine/action/MpUseAction (ATracer) : Action. @XmlAttribute value/delta/ratio; act: valueWithDelta=value+delta*lvl; ratio→maxMp*v/100; boostSkillCost!=0→v-=(v/(100/changeMpPercent)) (negative pct); Player current check→STR_SKILL_NOT_ENOUGH_MP false; reduceMp(USED_MP). SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("MpUseAction")]
public class MpUseAction : Action
{
    [XmlAttribute]
    public int value;

    [XmlAttribute]
    public int delta;

    [XmlAttribute]
    public bool ratio;

    public override bool Act(Skill skill)
    {
        if (!CanAct(skill))
            return false;
        skill.GetEffector().GetLifeStats().ReduceMp(SmAttackStatus.TYPE.USED_MP, GetCost(skill), 0, SmAttackStatus.LOG.REGULAR);
        return true;
    }

    public override bool CanAct(Skill skill)
    {
        // npcs have no mp, so they must not be blocked by an mp cost
        if (skill.GetEffector() is Player player && player.GetLifeStats().GetCurrentMp() < GetCost(skill))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_NOT_ENOUGH_MP());
            return false;
        }
        return true;
    }

    private int GetCost(Skill skill)
    {
        int valueWithDelta = value + delta * skill.GetSkillLevel();
        if (ratio)
            valueWithDelta = skill.GetEffector().GetLifeStats().GetMaxMp() * valueWithDelta / 100;
        int changeMpPercent = skill.GetBoostSkillCost();
        if (changeMpPercent != 0)
        {
            // changeMpPercent is negative
            valueWithDelta = valueWithDelta - ((valueWithDelta / ((100 / changeMpPercent))));
        }
        return valueWithDelta;
    }
}
