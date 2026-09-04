using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/ChargeAction.</summary>
[XmlType("ChargeItemAction")]
public class ChargeAction : AbstractItemAction
{
    [XmlAttribute("capacity")] public int maxChargeLevel;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        return GetConditioningItems(player, parentItem, targetItem).Count != 0;
    }

    /// <summary>
    /// The items to condition (just <c>targetItem</c> if one was selected), sending the appropriate "not chargeable" message if there are none.
    /// </summary>
    private ICollection<Item> GetConditioningItems(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem)
    {
        int chargeWay = parentItem.GetImprovement().GetChargeWay();
        if (targetItem != null)
        {
            if (targetItem.GetImprovement() == null || targetItem.GetImprovement().GetChargeWay() != chargeWay
                || targetItem.CalculateAvailableChargeLevel(player) == 0)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_FAIL_NOT_CHARGEABLE(targetItem.GetL10n()));
                return new List<Item>();
            }
            int achievableLevel = Aion.GameServer.Services.Items.ItemChargeService.CalculateMaxChargeLevelBasedOnRank(player, targetItem, maxChargeLevel);
            int achievableChargePoints = achievableLevel == 1 ? Aion.GameServer.Model.Items.ChargeInfo.LEVEL1 : Aion.GameServer.Model.Items.ChargeInfo.LEVEL2;
            if (targetItem.GetChargePoints() >= achievableChargePoints)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                    Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_FAIL_ALREADY_CHARGED(targetItem.GetL10n(), achievableLevel.ToString()));
                return new List<Item>();
            }
            return new List<Item> { targetItem };
        }
        ICollection<Item> conditioningItems = Aion.GameServer.Services.Items.ItemChargeService.FilterItemsToCondition(player, null, chargeWay);
        if (conditioningItems.Count == 0)
        {
            if (chargeWay == 1)
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_ALL_FAIL_NO_CHARGEABLE_EQUIPMENT());
            else
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_ALL_FAIL_NO_CHARGEABLE_EQUIPMENT());
        }
        return conditioningItems;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        int chargeWay = parentItem.GetImprovement().GetChargeWay();
        int castingDelay = parentItem.GetItemTemplate().GetCastingDelay();
        if (castingDelay <= 0)
        {
            FinishUse(player, parentItem, targetItem);
            return;
        }
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), castingDelay, 0, 0), true);
        ItemUseObserver observer = new ChargeUseObserver(player, parentItem, chargeWay);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            FinishUse(player, parentItem, targetItem);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(castingDelay)));
    }

    private void FinishUse(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem)
    {
        if (targetItem != null && player.GetInventory().GetItemByObjId(targetItem.GetObjectId()) == null && !targetItem.IsEquipped())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_NO_TARGET_ITEM());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 2, 0), true);
            return;
        }
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 0), true);
        ICollection<Item> conditioningItems = GetConditioningItems(player, parentItem, targetItem);
        if (conditioningItems.Count == 0)
            return;
        if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
            return;
        player.StartCooldown(parentItem);
        if (targetItem != null) // avoid the "Successfully conditioned equipped item(s)" bulk summary for a single targeted item
            Aion.GameServer.Services.Items.ItemChargeService.ChargeItem(player, targetItem, maxChargeLevel, false, false);
        else
            Aion.GameServer.Services.Items.ItemChargeService.ChargeItems(player, conditioningItems, maxChargeLevel, false, false);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_USE_ITEM(parentItem.GetL10n()));
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
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE_CANCELED());
            else
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_ITEM_CHARGE2_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
