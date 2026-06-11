using System;
using System.Threading.Tasks;
using Aion.Commons.Utils;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using PersistentState = Aion.GameServer.Model.GameObjects.Persistable.PersistentState;

namespace Aion.GameServer.Services.Items;

/// <summary>Java parity: services/item/ItemActionService (Estrayl). identifyItem (5s cast w/ abort observer, then roll optional sockets/bonus stats/enchant bonus + increment tune count) and applyTuneResult (apply pending tune). Anonymous ItemUseObserver -> nested IdentifyItemObserver capturing outer (uses this in removeObserver); anonymous scheduled Runnable -> Schedule ct-lambda closure; Rnd.get->Get. PendingTuneResult/TuningAction/SM_ packets red-tolerated.</summary>
public class ItemActionService
{
    public static void IdentifyItem(Player player, Item item)
    {
        int itemId = item.GetItemId();
        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), item.GetObjectId(), itemId, 5000, 9, 0), true);
        ItemUseObserver observer = new IdentifyItemObserver(player, item, itemId);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(TaskId.ITEM_USE, ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), item.GetObjectId(), itemId, 0, 10, 0), true);
            item.SetOptionalSockets(Rnd.Get(0, item.GetItemTemplate().GetOptionSlotBonus()));
            item.SetBonusStats(TuningAction.GetRandomStatBonusIdFor(item), true);
            item.SetEnchantBonus(Rnd.Get(0, item.GetItemTemplate().GetMaxEnchantBonus()));
            item.SetTuneCount(item.GetTuneCount() + 1); // not tuned have count = -1
            player.GetInventory().SetPersistentState(PersistentState.UPDATE_REQUIRED);
            PacketSendUtility.SendPacket(player, new SM_INVENTORY_UPDATE_ITEM(player, item));
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ITEM_IDENTIFY_SUCCEED(item.GetL10n()));
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(5000)));
    }

    public static void ApplyTuneResult(Player player, Item item)
    {
        PendingTuneResult tuneResult = item.GetPendingTuneResult();
        if (tuneResult == null)
        {
            AuditLogger.Log(player, "attempted to apply a tune result without tuning the item beforehand.");
            return;
        }
        item.SetOptionalSockets(tuneResult.GetOptionalSockets());
        item.SetEnchantBonus(tuneResult.GetEnchantBonus());
        item.SetBonusStats(tuneResult.GetStatBonusId(), true);
        item.SetPendingTuneResult(null);
        item.SetPersistentState(PersistentState.UPDATE_REQUIRED);
        player.GetInventory().SetPersistentState(PersistentState.UPDATE_REQUIRED);
    }

    private sealed class IdentifyItemObserver : ItemUseObserver
    {
        private readonly Player player;
        private readonly Item item;
        private readonly int itemId;

        public IdentifyItemObserver(Player player, Item item, int itemId)
        {
            this.player = player;
            this.item = item;
            this.itemId = itemId;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(TaskId.ITEM_USE);
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ITEM_IDENTIFY_CANCELED(item.GetL10n()));
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), item.GetObjectId(), itemId, 0, 11, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }
}
