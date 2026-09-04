using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Action;

/// <summary>Java parity: skillengine/action/HpUseAction (ATracer) : Action. @XmlAttribute value/delta/ratio; act: valueWithDelta=value+delta*lvl; ratio→/100f*maxHp; Player current check→STR_SKILL_NOT_ENOUGH_HP false; reduceHp(USED_HP, ..., effector). SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("HpUseAction")]
public class HpUseAction : Action
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
        Creature effector = skill.GetEffector();
        // npcs pass the check even when they cannot afford it and then pay what they have, down to 1 hp
        effector.GetLifeStats().ReduceHp(SmAttackStatus.TYPE.USED_HP, GetCost(skill), 0, SmAttackStatus.LOG.REGULAR, effector);
        return true;
    }

    public override bool CanAct(Skill skill)
    {
        // npcs are never blocked by an hp cost, they pay what they have, see Act()
        if (skill.GetEffector() is Player player && player.GetLifeStats().GetCurrentHp() <= GetCost(skill)) // the cast may never be lethal
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_NOT_ENOUGH_HP());
            return false;
        }
        return true;
    }

    private int GetCost(Skill skill)
    {
        int valueWithDelta = value + delta * skill.GetSkillLevel();
        if (ratio)
            valueWithDelta = (int)(valueWithDelta / 100f * skill.GetEffector().GetLifeStats().GetMaxHp());
        return valueWithDelta;
    }
}
