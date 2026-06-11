using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>Java parity: skillengine/condition/LeftHandCondition (Cheatkiller) : Condition. @XmlAttribute(name="type") LeftHandSlot; validate Player: switch(type) DUAL→offHand weapon || mainHand two-hand (assignment-in-condition preserved) else STR_SKILL_NEED_DUAL_WEAPON; SHIELD→isShieldEquipped else STR_SKILL_NEED_SHIELD; default false. LeftHandSlot/Item red-tolerated.</summary>
[XmlType("LeftHandCondition")]
public class LeftHandCondition : Condition
{
    [XmlAttribute("type")]
    private LeftHandSlot type;

    public override bool Validate(Skill env)
    {
        if (env.GetEffector() is Player)
        {
            Player player = (Player)env.GetEffector();
            switch (type)
            {
                case LeftHandSlot.DUAL:
                    Item offHandWeapon = player.GetEquipment().GetOffHandWeapon();
                    Item mainHandWeapon = null;
                    if (offHandWeapon != null && offHandWeapon.GetItemTemplate().IsWeapon()
                        || (mainHandWeapon = player.GetEquipment().GetMainHandWeapon()) != null && mainHandWeapon.GetItemTemplate().IsTwoHandWeapon())
                        return true;
                    else
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_NEED_DUAL_WEAPON());
                        return false;
                    }
                case LeftHandSlot.SHIELD:
                    if (player.GetEquipment().IsShieldEquipped())
                        return true;
                    else
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_NEED_SHIELD());
                        return false;
                    }
            }
        }
        return false;
    }
}
