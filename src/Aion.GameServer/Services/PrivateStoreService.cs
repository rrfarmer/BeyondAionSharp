using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Skillengine.Effect;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/PrivateStoreService (Simple). Player private-shop create/sell/close. Array foreach; Collection.toArray(new T[size])→.ToArray(); Map.values()→.Values, size()→Count, isEmpty()→Count==0; slf4j→ILogger; index-as-itemId quirk preserved. PrivateStore/TradeList/TradePSItem/SM_*/ItemService red-tolerated.</summary>
public class PrivateStoreService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("EXCHANGE_LOG");

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

        List<TradePSItem> boughtItems = GetBoughtItems(seller, tradeList);
        if (boughtItems == null || boughtItems.Count == 0)
            return; // Invalid items found or store was empty

        if (buyer.GetInventory().GetFreeSlots() < boughtItems.Count)
        {
            PacketSendUtility.SendPacket(buyer, SM_SYSTEM_MESSAGE.STR_MSG_DICE_INVEN_ERROR());
            return;
        }

        long price = 0;
        foreach (TradePSItem boughtItem in boughtItems)
            price += boughtItem.GetPrice() * boughtItem.GetCount();

        if (price < 0) // Kinah dupe
        {
            AuditLogger.Log(buyer, "tried to buy item with negative kinah price from private store");
            return;
        }

        if (price > buyer.GetInventory().GetKinah())
            return;

        foreach (TradePSItem boughtItem in boughtItems)
        {
            Item item = seller.GetInventory().GetItemByObjId(boughtItem.GetItemObjId());
            if (item != null)
            {
                // Fix "Private store stackable items dupe" by Asanka
                if (item.GetItemCount() < boughtItem.GetCount())
                {
                    AuditLogger.Log(buyer, "tried to buy more than players private store item stack count");
                    return;
                }

                DecreaseItemFromPlayer(seller, item, boughtItem);
                // unpack
                if (item.GetPackCount() > 0)
                    item.SetPackCount(item.GetPackCount() - 1);

                ItemService.AddItem(buyer, item, boughtItem.GetCount());

                if (boughtItem.GetCount() == 1)
                    PacketSendUtility.SendPacket(seller, SM_SYSTEM_MESSAGE.STR_MSG_PERSONAL_SHOP_SELL_ITEM(item.GetL10n()));
                else
                    PacketSendUtility.SendPacket(seller, SM_SYSTEM_MESSAGE.STR_MSG_PERSONAL_SHOP_SELL_ITEM_MULTI(boughtItem.GetCount(), item.GetL10n()));
                log.LogInformation("[PRIVATE STORE] > [Seller: " + seller.GetName() + "] sold [Item: " + item.GetItemId() + "][Amount: " + boughtItem.GetCount()
                    + "] to [Buyer: " + buyer.GetName() + "] for [Price: " + boughtItem.GetPrice() * boughtItem.GetCount() + "]");
            }
        }
        buyer.GetInventory().DecreaseKinah(price);
        seller.GetInventory().IncreaseKinah(price);

        if (seller.GetStore().GetSoldItems().Count == 0)
            ClosePrivateStore(seller);
    }

    /// <summary>Decrease item count and update inventory.</summary>
    private static void DecreaseItemFromPlayer(Player seller, Item item, TradePSItem boughtItem)
    {
        seller.GetInventory().DecreaseItemCount(item, boughtItem.GetCount());
        TradePSItem storeItem = seller.GetStore().GetTradeItemByObjId(item.GetObjectId());
        storeItem.DecreaseCount(boughtItem.GetCount());
        if (storeItem.GetCount() == 0)
            seller.GetStore().RemoveItem(item.GetObjectId());
    }

    private static List<TradePSItem> GetBoughtItems(Player seller, TradeList tradeList)
    {
        ICollection<TradePSItem> storeList = seller.GetStore().GetSoldItems().Values;
        // we need index based access since tradeList holds index values (this will work since underlying LinkedHashMap preserves insertion order)
        TradePSItem[] storeItems = storeList.ToArray();
        List<TradePSItem> boughtItems = new List<TradePSItem>();

        foreach (TradeItem tradeItem in tradeList.GetTradeItems())
        {
            if (tradeItem.GetItemId() >= 0 && tradeItem.GetItemId() < storeItems.Length) // itemId is index! blame the one who implemented this
            {
                TradePSItem storeItem = storeItems[tradeItem.GetItemId()];
                if (tradeItem.GetCount() > storeItem.GetCount())
                {
                    log.LogWarning("[Private Store] Attempt to buy more than for sale: " + tradeItem.GetCount() + " vs. " + storeItem.GetCount());
                    return null;
                }
                boughtItems.Add(new TradePSItem(storeItem.GetItemObjId(), storeItem.GetItemId(), tradeItem.GetCount(), storeItem.GetPrice()));
            }
            else
            {
                log.LogWarning("[Private Store] Attempt to buy from invalid store index: " + tradeItem.GetItemId());
                return null;
            }
        }

        return boughtItems;
    }

    public static void OpenPrivateStore(Player activePlayer, string name)
    {
        activePlayer.GetStore().SetStoreMessage(name);
        PacketSendUtility.BroadcastPacket(activePlayer, new SM_PRIVATE_STORE_NAME(activePlayer), true);
    }
}
