using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.Idfactory;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/ExchangeService (ATracer). Player-to-player trade. ConcurrentHashMap→ConcurrentDictionary (get→GetValueOrDefault, put→indexer, remove→TryRemove out-param); Map.get→GetValueOrDefault; Arrays.asList→List initializer; Player...→params Player[]; nested ItemPacketService.* types preserved; slf4j→ILogger. Exchange/ExchangeItem ported; Item/Storage/SM_*/DAO red-tolerated.</summary>
public class ExchangeService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("EXCHANGE_LOG");

    private readonly ConcurrentDictionary<int, Exchange> exchanges = new ConcurrentDictionary<int, Exchange>();

    public static ExchangeService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private ExchangeService()
    {
    }

    public void RegisterExchange(Player player1, Player player2)
    {
        if (!ValidateParticipants(player1, player2))
            return;

        exchanges[player1.GetObjectId()] = new Exchange(player1, player2);
        exchanges[player2.GetObjectId()] = new Exchange(player2, player1);

        PacketSendUtility.SendPacket(player2, new SM_EXCHANGE_REQUEST(player1.GetName()));
        PacketSendUtility.SendPacket(player1, new SM_EXCHANGE_REQUEST(player2.GetName()));
    }

    private bool ValidateParticipants(Player player1, Player player2)
    {
        return PlayerRestrictions.CanTrade(player1) && PlayerRestrictions.CanTrade(player2);
    }

    private Player GetCurrentParter(Player player)
    {
        Exchange exchange = exchanges.GetValueOrDefault(player.GetObjectId());
        return exchange != null ? exchange.GetTargetPlayer() : null;
    }

    private Exchange GetCurrentExchange(Player player)
    {
        return exchanges.GetValueOrDefault(player.GetObjectId());
    }

    public Exchange GetCurrentParnterExchange(Player player)
    {
        Player partner = GetCurrentParter(player);
        return partner != null ? GetCurrentExchange(partner) : null;
    }

    public bool IsPlayerInExchange(Player player)
    {
        return GetCurrentExchange(player) != null;
    }

    public void AddKinah(Player activePlayer, long itemCount)
    {
        Exchange currentExchange = GetCurrentExchange(activePlayer);
        if (currentExchange == null || currentExchange.IsLocked())
            return;

        if (itemCount < 1)
            return;

        // count total amount in inventory
        long availableCount = activePlayer.GetInventory().GetKinah();

        // count amount that was already added to exchange
        availableCount -= currentExchange.GetKinahCount();

        long countToAdd = availableCount > itemCount ? itemCount : availableCount;

        if (countToAdd > 0)
        {
            Player partner = GetCurrentParter(activePlayer);
            PacketSendUtility.SendPacket(activePlayer, new SM_EXCHANGE_ADD_KINAH(countToAdd, 0));
            PacketSendUtility.SendPacket(partner, new SM_EXCHANGE_ADD_KINAH(countToAdd, 1));
            currentExchange.AddKinah(countToAdd);
        }
    }

    public void AddItem(Player activePlayer, int itemObjId, long itemCount)
    {
        Item item = activePlayer.GetInventory().GetItemByObjId(itemObjId);
        if (item == null)
            return;

        Player partner = GetCurrentParter(activePlayer);
        if (partner == null)
            return;
        if (item.GetPackCount() <= 0 && !item.IsTradeable() && !TemporaryTradeTimeTask.GetInstance().CanTrade(item, partner.GetObjectId()))
        {
            if (!item.IsLegionTradeable() || activePlayer.GetLegion() == null || !activePlayer.GetLegion().Equals(partner.GetLegion()))
                return;
        }

        if (itemCount < 1)
            return;

        if (itemCount > item.GetItemCount())
            return;

        Exchange currentExchange = GetCurrentExchange(activePlayer);

        if (currentExchange == null)
            return;

        if (currentExchange.IsLocked())
            return;

        if (currentExchange.IsExchangeListFull())
            return;

        if (!AdminService.GetInstance().CanOperate(activePlayer, partner, item, "trade"))
            return;

        ExchangeItem exchangeItem = currentExchange.GetItems().GetValueOrDefault(item.GetObjectId());

        long actuallAddCount = 0;
        // item was not added previosly
        if (exchangeItem == null)
        {
            Item newItem = null;
            if (itemCount < item.GetItemCount())
            {
                newItem = ItemFactory.NewItem(item.GetItemId(), itemCount);
            }
            else
            {
                newItem = item;
            }
            exchangeItem = new ExchangeItem(itemObjId, itemCount, newItem);
            currentExchange.AddItem(itemObjId, exchangeItem);
            actuallAddCount = itemCount;
        }
        // item was already added
        else
        {
            // if player add item count that is more than possible
            // happens with exploits
            if (item.GetItemCount() == exchangeItem.GetItemCount())
                return;

            long possibleToAdd = item.GetItemCount() - exchangeItem.GetItemCount();
            actuallAddCount = itemCount > possibleToAdd ? possibleToAdd : itemCount;
            exchangeItem.AddCount(actuallAddCount);
        }

        if (!item.GetItemTemplate().IsStackable() || item.GetItemCount() == exchangeItem.GetItemCount())
        {
            PacketSendUtility.SendPacket(activePlayer, new SM_DELETE_ITEM(itemObjId, ItemPacketService.ItemDeleteType.PUT_TO_EXCHANGE));
        }
        else
        {
            Item fakeItem = new Item(itemObjId, item.GetItemTemplate());
            fakeItem.SetItemCount(item.GetItemCount() - exchangeItem.GetItemCount());
            PacketSendUtility.SendPacket(activePlayer, new SM_INVENTORY_UPDATE_ITEM(activePlayer, fakeItem,
                ItemPacketService.ItemUpdateType.PUT_TO_EXCHANGE));
        }

        PacketSendUtility.SendPacket(activePlayer, new SM_EXCHANGE_ADD_ITEM(0, exchangeItem.GetItem(), activePlayer));
        PacketSendUtility.SendPacket(partner, new SM_EXCHANGE_ADD_ITEM(1, exchangeItem.GetItem(), partner));
    }

    public void LockExchange(Player activePlayer)
    {
        Exchange exchange = GetCurrentExchange(activePlayer);
        if (exchange != null)
        {
            exchange.Lock();
            Player currentParter = GetCurrentParter(activePlayer);
            PacketSendUtility.SendPacket(currentParter, new SM_EXCHANGE_CONFIRMATION(3));
        }
    }

    public void CancelExchange(Player activePlayer)
    {
        Player currentPartner = GetCurrentParter(activePlayer);
        ReturnItems(activePlayer);

        if (currentPartner != null)
        {
            ReturnItems(currentPartner);
            PacketSendUtility.SendPacket(currentPartner, new SM_EXCHANGE_CONFIRMATION(1));
        }

        CleanUpExchanges(true, activePlayer, currentPartner);
    }

    private void ReturnItems(Player player)
    {
        Exchange exchange = GetCurrentExchange(player);
        if (exchange == null)
        {
            return;
        }
        if (exchange.GetItems().Count != 0)
        {
            foreach (ExchangeItem exItem in exchange.GetItems().Values)
            {
                Item realItem = player.GetInventory().GetItemByObjId(exItem.GetItemObjId());
                if (realItem == null)
                {
                    log.LogWarning("Player " + player.GetName() + " is trying to return fake item on exchange cancel!");
                    return;
                }
                if (realItem.GetItemCount() == exItem.GetItemCount())
                {
                    PacketSendUtility.SendPacket(player, new SM_INVENTORY_ADD_ITEM(new List<Item> { realItem }, player, ItemPacketService.ItemAddType.PLAYER_EXCHANGE_GET_BACK));
                }
                else
                {
                    PacketSendUtility.SendPacket(player, new SM_INVENTORY_UPDATE_ITEM(player, realItem, ItemPacketService.ItemUpdateType.INC_PLAYER_EXCHANGE_GET_BACK));
                }
            }
            PacketSendUtility.SendPacket(player, SM_CUBE_UPDATE.CubeSize(StorageType.CUBE, player));
        }
    }

    public void ConfirmExchange(Player activePlayer)
    {
        if (activePlayer == null || !activePlayer.IsOnline())
            return;

        Exchange currentExchange = GetCurrentExchange(activePlayer);

        // TODO: Why is exchange null =/
        if (currentExchange == null)
            return;
        currentExchange.Confirm();

        Player currentPartner = GetCurrentParter(activePlayer);
        PacketSendUtility.SendPacket(currentPartner, new SM_EXCHANGE_CONFIRMATION(2));

        if (GetCurrentExchange(currentPartner).IsConfirmed())
        {
            PerformTrade(activePlayer, currentPartner);
        }
    }

    private void PerformTrade(Player activePlayer, Player currentPartner)
    {
        Exchange exchange1 = GetCurrentExchange(activePlayer);
        Exchange exchange2 = GetCurrentExchange(currentPartner);

        if (!ValidateExchange(activePlayer, currentPartner))
        {
            if (!ValidateInventorySize(currentPartner, exchange1))
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_EXCHANGE_CANT_EXCHANGE_HEAVY_TO_ADD_EXCHANGE_ITEM());
            else
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_PARTNER_TOO_HEAVY_TO_EXCHANGE());
            CleanUpExchanges(true, activePlayer, currentPartner);
            return;
        }

        if (!RemoveItemsFromInventory(activePlayer, exchange1) || !RemoveItemsFromInventory(currentPartner, exchange2))
        {
            CleanUpExchanges(true, activePlayer, currentPartner);
            AuditLogger.Log(activePlayer, "tried to exploit kinah exchange with partner: " + currentPartner);
            return;
        }

        PacketSendUtility.SendPacket(activePlayer, new SM_EXCHANGE_CONFIRMATION(0));
        PacketSendUtility.SendPacket(currentPartner, new SM_EXCHANGE_CONFIRMATION(0));

        PutItemToInventory(activePlayer, currentPartner, exchange1, exchange2);
        PutItemToInventory(currentPartner, activePlayer, exchange2, exchange1);
        InventoryDAO.Store(exchange1.GetActiveplayer());
        InventoryDAO.Store(exchange2.GetActiveplayer());

        CleanUpExchanges(false, activePlayer, currentPartner);
    }

    private void CleanUpExchanges(bool releaseIds, params Player[] players)
    {
        foreach (Player player in players)
        {
            if (player == null)
                continue;

            exchanges.TryRemove(player.GetObjectId(), out Exchange exchange);
            if (exchange != null && releaseIds)
            {
                foreach (ExchangeItem item in exchange.GetItems().Values)
                {
                    if (item.GetItemObjId() != item.GetItem().GetObjectId() && player.GetInventory().GetItemByObjId(item.GetItem().GetObjectId()) == null)
                        IDFactory.GetInstance().ReleaseId(item.GetItem().GetObjectId()); // release ID if it was a newly allocated one
                }
            }
        }
    }

    private bool RemoveItemsFromInventory(Player player, Exchange exchange)
    {
        Storage inventory = player.GetInventory();

        foreach (ExchangeItem exchangeItem in exchange.GetItems().Values)
        {
            Item item = exchangeItem.GetItem();
            Item itemInInventory = inventory.GetItemByObjId(exchangeItem.GetItemObjId());
            if (itemInInventory == null)
            {
                AuditLogger.Log(player, "tried to trade not existing item");
                return false;
            }

            long itemCount = exchangeItem.GetItemCount();

            if (itemCount < itemInInventory.GetItemCount())
            {
                inventory.DecreaseItemCount(itemInInventory, itemCount);
            }
            else
            {
                // remove from source inventory only
                inventory.Remove(itemInInventory);
                exchangeItem.SetItem(itemInInventory);
                // release when only part stack was added in the beginning -> full stack in the end
                if (item.GetObjectId() != exchangeItem.GetItemObjId())
                {
                    IDFactory.GetInstance().ReleaseId(item.GetObjectId());
                }
                PacketSendUtility.SendPacket(player, new SM_DELETE_ITEM(itemInInventory.GetObjectId()));
            }
        }
        return player.GetInventory().TryDecreaseKinah(exchange.GetKinahCount());
    }

    private bool ValidateExchange(Player activePlayer, Player currentPartner)
    {
        Exchange exchange1 = GetCurrentExchange(activePlayer);
        Exchange exchange2 = GetCurrentExchange(currentPartner);
        bool activePlayerCheck = ValidateInventorySize(activePlayer, exchange2);
        bool currentPartnerCheck = ValidateInventorySize(currentPartner, exchange1);
        if (!activePlayerCheck)
        {
            PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_EXCHANGE_CANT_EXCHANGE_HEAVY_TO_ADD_EXCHANGE_ITEM());
            PacketSendUtility.SendPacket(currentPartner, SM_SYSTEM_MESSAGE.STR_PARTNER_TOO_HEAVY_TO_EXCHANGE());
        }
        else if (!currentPartnerCheck)
        {
            PacketSendUtility.SendPacket(currentPartner, SM_SYSTEM_MESSAGE.STR_EXCHANGE_CANT_EXCHANGE_HEAVY_TO_ADD_EXCHANGE_ITEM());
            PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_PARTNER_TOO_HEAVY_TO_EXCHANGE());
        }
        return activePlayerCheck && currentPartnerCheck;
    }

    private bool ValidateInventorySize(Player activePlayer, Exchange exchange)
    {
        int numberOfFreeSlots = activePlayer.GetInventory().GetFreeSlots();
        return numberOfFreeSlots >= exchange.GetItems().Count;
    }

    private void PutItemToInventory(Player giver, Player partner, Exchange exchange1, Exchange exchange2)
    {
        foreach (ExchangeItem exchangeItem in exchange1.GetItems().Values)
        {
            Item itemToPut = exchangeItem.GetItem();
            itemToPut.SetEquipmentSlot(0);
            if (itemToPut.GetPackCount() > 0) // unpack
                itemToPut.SetPackCount(itemToPut.GetPackCount() * -1);
            partner.GetInventory().Add(itemToPut, ItemPacketService.ItemAddType.PLAYER_EXCHANGE_GET);
            if (LoggingConfig.LOG_PLAYER_EXCHANGE)
                log.LogInformation("Player " + giver.GetName() + " exchanged item " + itemToPut.GetItemId() + " [" + itemToPut.GetItemName() + "] (count: "
                    + itemToPut.GetItemCount() + ") with player " + partner.GetName());
        }
        long kinahToExchange = exchange1.GetKinahCount();
        if (kinahToExchange > 0)
        {
            partner.GetInventory().IncreaseKinah(kinahToExchange);
            if (LoggingConfig.LOG_PLAYER_EXCHANGE)
                log.LogInformation("Player " + giver.GetName() + " exchanged " + kinahToExchange + " Kinah with player " + partner.GetName());
        }
    }

    private static class SingletonHolder
    {
        internal static readonly ExchangeService instance = new ExchangeService();
    }
}
