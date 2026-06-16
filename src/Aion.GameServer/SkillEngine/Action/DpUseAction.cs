using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Action;

/// <summary>Java parity: skillengine/action/DpUseAction (ATracer) : Action. @XmlAttribute value; act: currentDp<=0||currentDp<value→STR_SKILL_NOT_ENOUGH_DP false; else setDp(current-value) true. SM_SYSTEM_MESSAGE red-tolerated.</summary>
[XmlType("DpUseAction")]
public class DpUseAction : Action
{
    [XmlAttribute]
    public int value;

    public override bool Act(Skill skill)
    {
        Player effector = (Player)skill.GetEffector();
        int currentDp = effector.GetCommonData().GetDp();

        if (currentDp <= 0 || currentDp < value)
        {
            PacketSendUtility.SendPacket(effector, SM_SYSTEM_MESSAGE.STR_SKILL_NOT_ENOUGH_DP());
            return false;
        }
        effector.GetCommonData().SetDp(currentDp - value);
        return true;
    }
}
