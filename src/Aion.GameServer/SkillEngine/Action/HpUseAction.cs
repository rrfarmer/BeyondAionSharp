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
        Creature effector = skill.GetEffector();
        int valueWithDelta = value + delta * skill.GetSkillLevel();
        int currentHp = effector.GetLifeStats().GetCurrentHp();
        if (ratio)
            valueWithDelta = (int)(valueWithDelta / 100f * skill.GetEffector().GetLifeStats().GetMaxHp());
        if (effector is Player)
        {
            if (currentHp <= 0 || currentHp < valueWithDelta)
            {
                PacketSendUtility.SendPacket((Player)effector, SM_SYSTEM_MESSAGE.STR_SKILL_NOT_ENOUGH_HP());
                return false;
            }
        }
        effector.GetLifeStats().ReduceHp(SmAttackStatus.TYPE.USED_HP, valueWithDelta, 0, SmAttackStatus.LOG.REGULAR, effector);
        return true;
    }
}
