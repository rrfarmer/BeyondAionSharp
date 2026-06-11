using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.SkillEngine.Effects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/RideAction (Rolandas, ginho1).</summary>
[XmlType("RideAction")]
public class RideAction : AbstractItemAction
{
    [XmlAttribute("npc_id")] protected int npcId;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (!player.IsInPlayerMode(PlayerMode.RIDE)) // RideAction is for mounting and dismounting, canAct should never forbid dismounting
        {
            if (parentItem == null)
                return false;

            if (Aion.GameServer.Configs.Main.CustomConfig.ENABLE_RIDE_RESTRICTION)
            {
                foreach (Aion.GameServer.World.Zone.ZoneInstance zone in player.FindZones())
                {
                    if (!zone.CanRide())
                    {
                        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_CANNOT_RIDE_INVALID_LOCATION());
                        return false;
                    }
                }
            }
            if (player.IsInState(CreatureState.Resting))
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_CANT_RIDE(Aion.GameServer.Utils.ChatUtil.L10n(1400057)));
                return false;
            }
            if (player.GetEffectController().IsInAnyAbnormalState(AbnormalState.DISMOUNT_RIDE))
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_CANNOT_RIDE_ABNORMAL_STATE());
                return false;
            }
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        player.GetController().CancelUseItem();
        if (player.IsInPlayerMode(PlayerMode.RIDE))
        {
            player.UnsetPlayerMode(PlayerMode.RIDE);
            return;
        }

        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 3000, 0, 0), true);
        ItemUseObserver observer = new RideUseObserver(player, parentItem);

        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.UnsetState(CreatureState.Active);
            player.SetState(CreatureState.Resting);
            if (player.IsInFlyingState())
                player.SetState(CreatureState.FloatingCorpse);
            player.GetObserveController().RemoveObserver(observer);
            Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate = parentItem.GetItemTemplate();
            player.SetPlayerMode(PlayerMode.RIDE, GetRideInfo());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmEmotion(player, EmotionType.ChangeSpeed, 0, 0), true);
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmEmotion(player, EmotionType.Ride, 0, GetRideInfo().GetNpcId()), true);
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 1), true);
            player.GetController().CancelTask(TaskId.ITEM_USE);
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().RideAction(new Aion.GameServer.QuestEngine.Model.QuestEnv(null, player, 0), itemTemplate.GetTemplateId());
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(3000)));

        ActionObserver rideObserver = new RideAbnormalObserver(player);
        player.GetObserveController().AddObserver(rideObserver);
        player.SetRideObservers(rideObserver);

        // TODO some mounts have lower chance of dismounting
        ActionObserver attackedObserver = new RideAttackedObserver(player);
        player.GetObserveController().AddObserver(attackedObserver);
        player.SetRideObservers(attackedObserver);

        ActionObserver dotAttackedObserver = new RideDotAttackedObserver(player);
        player.GetObserveController().AddObserver(dotAttackedObserver);
        player.SetRideObservers(dotAttackedObserver);
    }

    public Aion.GameServer.Model.Templates.Ride.RideInfo GetRideInfo()
    {
        return DataManager.RIDE_DATA.GetRideInfo(npcId);
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class RideUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;

        public RideUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(TaskId.ITEM_USE);
            player.RemoveItemCoolDown(parentItem.GetItemTemplate().GetUseLimits().GetDelayId());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 3, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }

    // Java parity: anonymous ActionObserver(ObserverType.ABNORMALSETTED) in act().
    private sealed class RideAbnormalObserver : ActionObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;

        public RideAbnormalObserver(Aion.GameServer.Model.GameObjects.Players.Player player)
            : base(ObserverType.ABNORMALSETTED)
        {
            this.player = player;
        }

        public override void Abnormalsetted(AbnormalState state)
        {
            if ((state.GetId() & AbnormalState.DISMOUNT_RIDE.GetId()) != 0)
            {
                player.UnsetPlayerMode(PlayerMode.RIDE);
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_UNRIDE_ABNORMAL_STATE());
            }
        }
    }

    // Java parity: anonymous ActionObserver(ObserverType.ATTACKED) in act().
    private sealed class RideAttackedObserver : ActionObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;

        public RideAttackedObserver(Aion.GameServer.Model.GameObjects.Players.Player player)
            : base(ObserverType.ATTACKED)
        {
            this.player = player;
        }

        public override void Attacked(Creature creature, int skillId)
        {
            if (Aion.GameServer.Commons.Utils.Rnd.Chance() < 20) // 20% from client action file
                player.UnsetPlayerMode(PlayerMode.RIDE);
        }
    }

    // Java parity: anonymous ActionObserver(ObserverType.DOT_ATTACKED) in act().
    private sealed class RideDotAttackedObserver : ActionObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;

        public RideDotAttackedObserver(Aion.GameServer.Model.GameObjects.Players.Player player)
            : base(ObserverType.DOT_ATTACKED)
        {
            this.player = player;
        }

        public override void Dotattacked(Creature creature, Aion.GameServer.SkillEngine.Model.Effect dotEffect)
        {
            if (Aion.GameServer.Commons.Utils.Rnd.Chance() < 20) // 20% from client action file
                player.UnsetPlayerMode(PlayerMode.RIDE);
        }
    }
}
