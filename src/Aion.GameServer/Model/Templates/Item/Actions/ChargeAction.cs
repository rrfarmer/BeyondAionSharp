using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/ChargeAction.</summary>
[XmlType("ChargeItemAction")]
public class ChargeAction : AbstractItemAction
{
    [XmlAttribute("capacity")] private int maxChargeLevel;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        return Aion.GameServer.Services.Item.ItemChargeService.FilterItemsToCondition(player, null, parentItem.GetImprovement().GetChargeWay()).Count != 0;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int chargeWay = parentItem.GetImprovement().GetChargeWay();
        ICollection<Item> conditioningItems = Aion.GameServer.Services.Item.ItemChargeService.FilterItemsToCondition(player, null, chargeWay);

        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 3000, 0, 0), true);
        ItemUseObserver observer = new ChargeUseObserver(player, parentItem, chargeWay);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 0), true);
            if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
                return ValueTask.CompletedTask;
            Aion.GameServer.Services.Item.ItemChargeService.ChargeItems(player, conditioningItems, maxChargeLevel, false, false);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(3000)));
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class ChargeUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;
        private readonly int chargeWay;

        public ChargeUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, int chargeWay)
        {
            this.player = player;
            this.parentItem = parentItem;
            this.chargeWay = chargeWay;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            if (chargeWay == 1)
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_CHARGE_CANCELED());
            else
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_ITEM_CHARGE2_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
