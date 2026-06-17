using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/PolishAction.</summary>
[XmlType("PolishAction")]
public class PolishAction : AbstractItemAction
{
    [XmlAttribute("set_id")] public int polishSetId;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (parentItem.GetItemTemplate().GetLevel() > targetItem.GetItemTemplate().GetLevel())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_POLISH_WRONG_LEVEL());
            return false;
        }
        if (!targetItem.IsIdentified())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_POLISH_NEED_IDENTIFY());
            return false;
        }
        return !player.IsInAttackMode() && targetItem.GetItemTemplate().IsWeapon() && targetItem.GetItemTemplate().IsCanPolish();
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 5000, 0, 0), true);
        ItemUseObserver observer = new PolishUseObserver(player, parentItem);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);

            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 1, 1), true);
            if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
            {
                return ValueTask.CompletedTask;
            }
            int bonusNumber = DataManager.ITEM_RANDOM_BONUSES.SelectRandomBonusNumber(Aion.GameServer.Model.Templates.Items.Bonuses.StatBonusType.POLISH, polishSetId);
            if (bonusNumber == 0)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_FAILED(parentItem.GetL10n()));
                return ValueTask.CompletedTask;
            }
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_POLISH_SUCCEED(targetItem.GetL10n()));
            Aion.GameServer.Model.Items.IdianStone idianStone = targetItem.GetIdianStone();
            if (idianStone != null)
            {
                idianStone.OnUnEquip(player);
                targetItem.SetIdianStone(null);
                idianStone.SetPersistentState(IPersistable.PersistentState.DELETED);
                Aion.GameServer.Dao.ItemStoneListDAO.StoreIdianStones(idianStone);
            }
            idianStone = new Aion.GameServer.Model.Items.IdianStone(parentItem.GetItemId(), IPersistable.PersistentState.NEW, targetItem, bonusNumber, 1000000);
            targetItem.SetIdianStone(idianStone);
            if (targetItem.IsEquipped())
            {
                idianStone.OnEquip(player, targetItem.GetEquipmentSlot());
            }
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_INVENTORY_UPDATE_ITEM(player, targetItem));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000)));
    }

    // Java parity: anonymous ItemUseObserver in act().
    private sealed class PolishUseObserver : ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;

        public PolishUseObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            player.RemoveItemCoolDown(parentItem.GetItemTemplate().GetUseLimits().GetDelayId());
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_ITEM_CANCELED());
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 2, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }

    public int GetPolishSetId()
    {
        return polishSetId;
    }
}
