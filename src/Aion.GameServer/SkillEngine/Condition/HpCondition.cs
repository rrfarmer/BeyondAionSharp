using System.Xml.Serialization;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>Java parity: skillengine/condition/HpCondition (Tomate) : Condition. @XmlAttribute value/delta/ratio; validate: valueWithDelta=value+delta*lvl, ratio→maxHp*v/100; if currentHp>v→reduceHp(USED_HP, ..., effector); return currentHp>=v. getHpValue getter. SM_ATTACK_STATUS red-tolerated.</summary>
[XmlType("HpCondition")]
public class HpCondition : Condition
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
        Aion.GameServer.Model.GameObjects.Creature effector = skill.GetEffector();
        // npcs pass the check even when they cannot afford it and then pay what they have, down to 1 hp (example: skillId 18304)
        effector.GetLifeStats().ReduceHp(SmAttackStatus.TYPE.USED_HP, GetCost(skill), 0, SmAttackStatus.LOG.REGULAR, effector);
        return true;
    }

    public override bool CanValidate(Skill skill)
    {
        // npcs are never blocked by an hp cost, they pay what they have, see Validate()
        if (skill.GetEffector() is Aion.GameServer.Model.GameObjects.Players.Player player && player.GetLifeStats().GetCurrentHp() <= GetCost(skill)) // the cast may never be lethal
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_NOT_ENOUGH_HP());
            return false;
        }
        return true;
    }

    private int GetCost(Skill skill)
    {
        int valueWithDelta = value + delta * skill.GetSkillLevel();
        if (ratio)
            valueWithDelta = (skill.GetEffector().GetLifeStats().GetMaxHp() * valueWithDelta) / 100;
        return valueWithDelta;
    }

    public int GetHpValue()
    {
        return value;
    }
}
