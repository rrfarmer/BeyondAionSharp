using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/PrivateStoreService (Simple). Player private-shop create/sell/close. Array foreach; Collection.toArray(new T[size])→.ToArray(); Map.values()→.Values, size()→Count, isEmpty()→Count==0; slf4j→ILogger; index-as-itemId quirk preserved. PrivateStore/TradeList/TradePSItem/SM_*/ItemService red-tolerated.</summary>
public class PrivateStoreService
{
    private static readonly ILogger log = AionLog.For("EXCHANGE_LOG");

    public static void CreateStoreWithItems(Player player, TradePSItem[] tradePSItems)
    {
        if (!CanOpenPrivateStore(player))
            return;

        PrivateStore store = new PrivateStore(player);
        foreach (TradePSItem tradePSItem in tradePSItems)
        {
            Item item = player.GetInventory().GetItemByObjId(tradePSItem.GetItemObjId());
            if (!ValidateItem(store, item, tradePSItem))
                return;
            store.AddItemToSell(tradePSItem.GetItemObjId(), tradePSItem);
        }
        player.SetStore(store);
        player.SetState(CreatureState.PRIVATE_SHOP, true);
        RecallService.GetInstance().Cancel(player, RecallService.CancelReason.CANCELLED);
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.OPEN_PRIVATESHOP, 0, 0), true);
    }

    private static bool CanOpenPrivateStore(Player player)
    {
        if (player.IsFlying())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_DISABLED_IN_FLY_MODE());
            return false;
        }
        if (player.GetMoveController().IsInMove())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_DISABLED_IN_MOVING_OBJECT());
            return false;
        }
        if (player.IsInAttackMode())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_DISABLED_IN_COMBAT_MODE());
            return false;
        }
        if (player.IsTrading())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_OPEN_STORE_DURING_CRAFTING()); // name "crafting" is NC fail, msg is correct
            return false;
        }
        if (player.IsInPlayerMode(PlayerMode.RIDE) || player.IsInRobotMode())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_PERSONAL_SHOP_RESTRICTION_RIDE());
            return false;
        }
        if (player.GetEffectController().IsAbnormalSet(AbnormalState.HIDE))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_DISABLED_IN_HIDDEN_MODE());
            return false;
        }
        if (player.IsDead())
            return false;
        if (player.IsInState(CreatureState.CHAIR))
            return false;
        if (player.GetStore() != null)
            return false;
        return true;
    }

    private static bool ValidateItem(PrivateStore store, Item item, TradePSItem psItem)
    {
        if (item == null || psItem.GetItemId() != item.GetItemTemplate().GetTemplateId())
        {
            return false;
        }
        if (psItem.GetCount() > item.GetItemCount() || psItem.GetCount() < 1)
        {
            return false;
        }
        if (psItem.GetPrice() < 0)
        {
            return false;
        }
        if (store.GetSoldItems().Count == 10)
        {
            PacketSendUtility.SendPacket(store.GetOwner(), SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_FULL_BASKET());
            return false;
        }
        if (item.GetPackCount() <= 0 && !item.IsTradeable())
        {
            PacketSendUtility.SendPacket(store.GetOwner(), SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_CANNOT_BE_EXCHANGED());
            return false;
        }
        if (item.IsEquipped())
        {
            PacketSendUtility.SendPacket(store.GetOwner(), SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_CAN_NOT_SELL_EQUIPED_ITEM());
            return false;
        }
        if (store.GetTradeItemByObjId(psItem.GetItemObjId()) != null)
        {
            PacketSendUtility.SendPacket(store.GetOwner(), SM_SYSTEM_MESSAGE.STR_PERSONAL_SHOP_ALREAY_REGIST_ITEM());
            return false;
        }
        return true;
    }

    public static void ClosePrivateStore(Player player)
    {
        if (player.GetStore() == null)
            return;
        player.SetStore(null);
        player.UnsetState(CreatureState.PRIVATE_SHOP);
        player.SetState(CreatureState.ACTIVE);
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.CLOSE_PRIVATESHOP, 0, 0), true);
    }

    /// <summary>This method will move the item to the new player and move kinah to item owner.</summary>
    public static void SellStoreItem(Player seller, Player buyer, TradeList tradeList)
    {
        if (!seller.IsOnline() || !buyer.IsOnline() || seller.GetRace() != buyer.GetRace())
            return;

        PrivateStore store = seller.GetStore();
        if (store == null)
            return;

        long refund = 0;
        lock (store) // buyers must not take the same stack twice: the store count is the only limit
        {
            List<TradePSItem>? boughtItems = GetBoughtItems(seller, store, tradeList);
            if (boughtItems == null || boughtItems.Count == 0)
                return; // Invalid items found or store was empty

            if (buyer.GetInventory().GetFreeSlots() < boughtItems.Count)
            {
                PacketSendUtility.SendPacket(buyer, SM_SYSTEM_MESSAGE.STR_MSG_DICE_INVEN_ERROR());
                return;
            }

            long price = 0;
            try
            {
                foreach (TradePSItem boughtItem in boughtItems)
                    price = checked(price + checked(boughtItem.GetPrice() * boughtItem.GetCount()));
            }
            catch (System.OverflowException)
            {
                AuditLogger.Log(buyer, "tried to buy items for more than " + long.MaxValue + " kinah from private store of " + seller);
                return;
            }

            if (price < 0) // Kinah dupe
            {
                AuditLogger.Log(buyer, "tried to buy item with negative kinah price from private store");
                return;
            }

            foreach (TradePSItem boughtItem in boughtItems) // the stack may be gone since the snapshot
            {
                Item snapshot = seller.GetInventory().GetItemByObjId(boughtItem.GetItemObjId());
                if (snapshot == null || snapshot.GetItemCount() < boughtItem.GetCount())
                    return;
            }

            if (!buyer.GetInventory().TryDecreaseKinah(price))
                return;

            foreach (TradePSItem boughtItem in boughtItems)
            {
                Item item = seller.GetInventory().GetItemByObjId(boughtItem.GetItemObjId());
                long soldCount = item == null ? 0 : DecreaseItemFromPlayer(seller, store, item, boughtItem);
                if (soldCount < boughtItem.GetCount())
                    refund += boughtItem.GetPrice() * (boughtItem.GetCount() - soldCount);
                if (soldCount == 0)
                    continue;
                // unpack
                if (item.GetPackCount() > 0)
                    item.SetPackCount(item.GetPackCount() - 1);

                ItemService.AddItem(buyer, item, soldCount);

                if (soldCount == 1)
                    PacketSendUtility.SendPacket(seller, SM_SYSTEM_MESSAGE.STR_MSG_PERSONAL_SHOP_SELL_ITEM(item.GetL10n()));
                else
                    PacketSendUtility.SendPacket(seller, SM_SYSTEM_MESSAGE.STR_MSG_PERSONAL_SHOP_SELL_ITEM_MULTI(soldCount, item.GetL10n()));
                log.LogInformation("[PRIVATE STORE] > [Seller: {SellerGetName}] sold [Item: {ItemGetItemId}][Amount: {BoughtItemGetCount}] to [Buyer: {BuyerGetName}] for [Price: {BoughtItemGetPriceBoughtItemGetCount}]", seller.GetName(), item.GetItemId(), soldCount, buyer.GetName(), boughtItem.GetPrice() * soldCount);
            }
            if (refund > 0)
                buyer.GetInventory().IncreaseKinah(refund);
            seller.GetInventory().IncreaseKinah(price - refund);
        }

        if (store.GetSoldItems().Count == 0)
            ClosePrivateStore(seller);
    }

    /// <summary>Decrease item count and update inventory.</summary>
    /// <returns>The removed amount, which may be less than requested.</returns>
    private static long DecreaseItemFromPlayer(Player seller, PrivateStore store, Item item, TradePSItem boughtItem)
    {
        long soldCount = boughtItem.GetCount() - seller.GetInventory().DecreaseItemCount(item, boughtItem.GetCount());
        if (soldCount <= 0)
            return 0;
        TradePSItem storeItem = store.GetTradeItemByObjId(item.GetObjectId());
        storeItem.DecreaseCount(soldCount);
        if (storeItem.GetCount() == 0)
            store.RemoveItem(item.GetObjectId());
        return soldCount;
    }

    /// <summary>Entries are unique indexes into the seller's store list.</summary>
    private static List<TradePSItem>? GetBoughtItems(Player seller, PrivateStore store, TradeList tradeList)
    {
        ICollection<TradePSItem> storeList = store.GetSoldItems().Values;
        // we need index based access since tradeList holds index values (this will work since underlying LinkedHashMap preserves insertion order)
        TradePSItem[] storeItems = storeList.ToArray();
        List<TradePSItem> boughtItems = new List<TradePSItem>();

        foreach (TradeItem tradeItem in tradeList.GetTradeItems())
        {
            int index = tradeItem.GetItemId(); // itemId is a store index.
            if (index < 0 || index >= storeItems.Length)
            {
                log.LogWarning("[Private Store] Attempt to buy item with invalid index: {Index}", index);
                return null;
            }
            TradePSItem storeItem = storeItems[index];
            long count = tradeItem.GetCount();
            if (count > storeItem.GetCount())
            {
                log.LogWarning("[Private Store] Attempt to buy more than for sale: {Count} vs. {StoreItemGetCount}", count, storeItem.GetCount());
                return null;
            }
            Item item = seller.GetInventory().GetItemByObjId(storeItem.GetItemObjId());
            if (item == null || item.GetItemCount() < count)
                return null;
            boughtItems.Add(new TradePSItem(storeItem.GetItemObjId(), storeItem.GetItemId(), count, storeItem.GetPrice()));
        }

        return boughtItems;
    }

    public static void OpenPrivateStore(Player activePlayer, string name)
    {
        if (name != null && name.Length > PrivateStore.MAX_STORE_MESSAGE_LENGTH) // retail refuses longer messages instead of truncating them
            return;
        activePlayer.GetStore().SetStoreMessage(name!);
        PacketSendUtility.BroadcastPacket(activePlayer, new SM_PRIVATE_STORE_NAME(activePlayer), true);
    }
}
