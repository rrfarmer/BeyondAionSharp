using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/RideRobotEffect (Rolandas, Cheatkiller) : EffectTemplate (no @XmlType→class name). applyEffect→addToEffectedController; startEffect: setRobotId from mainHand skin, SM_RIDE_ROBOT, anonymous UNEQUIP observer→nested UnequipObserver (WEAPON→endEffect); endEffect: setRobotId(0), SM_RIDE_ROBOT, end all rideRobotCondition effects. EquipType/SM_RIDE_ROBOT red-tolerated.</summary>
[XmlType("RideRobotEffect")]
public class RideRobotEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        Player player = (Player)effect.GetEffected();
        player.SetRobotId(player.GetEquipment().GetMainHandWeapon().GetItemSkinTemplate().GetRobotId());
        PacketSendUtility.BroadcastPacketAndReceive(player, new SM_RIDE_ROBOT(player));

        effect.AddObserver(player, new UnequipObserver(effect));
    }

    public override void EndEffect(Effect effect)
    {
        Player player = (Player)effect.GetEffected();
        player.SetRobotId(0);
        PacketSendUtility.BroadcastPacketAndReceive(player, new SM_RIDE_ROBOT(player));
        foreach (Effect ef in player.GetEffectController().GetAbnormalEffects())
        {
            if (ef.GetSkillTemplate().GetRideRobotCondition() != null)
                ef.EndEffect();
        }
    }

    private sealed class UnequipObserver : ActionObserver
    {
        private readonly Effect effect;

        public UnequipObserver(Effect effect)
            : base(ObserverType.UNEQUIP)
        {
            this.effect = effect;
        }

        public override void Unequip(Item item, Player owner)
        {
            if (item.GetEquipmentType() == EquipType.WEAPON)
            {
                effect.EndEffect();
            }
        }
    }
}
