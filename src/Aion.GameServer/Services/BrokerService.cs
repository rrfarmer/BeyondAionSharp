using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Broker;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Services.Player;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Taskmanager;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.World;
using static Aion.GameServer.Model.GameObjects.Persistable;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/BrokerService (kosyachok, ATracer, Sykra). Singleton (SingletonHolder); "EXCHANGE_LOG" logger; 4 race item maps ConcurrentDictionary; scheduleAtFixedRate checkExpiredItems; ConcurrentHashMap.put→indexer/get→GetValueOrDefault/remove→TryRemove/values().removeIf→snapshot+TryRemove; synchronized(this)/(map)→lock; LongSummaryStatistics→LINQ Min/Max; ArrayUtils.removeElement→List.Remove (first occurrence); subList→GetRange; Objects::nonNull→x!=null; streams→LINQ; Collections.emptyList→new List; nested BrokerPeriodicTaskManager + BrokerOpSaveTask (Runnable→Run()) + SingletonHolder. DAO/BrokerItem/packets red-tolerated.</summary>
public class BrokerService
{
    private ConcurrentDictionary<int, BrokerItem> elyosBrokerItems = new ConcurrentDictionary<int, BrokerItem>();
    private ConcurrentDictionary<int, BrokerItem> elyosSettledItems = new ConcurrentDictionary<int, BrokerItem>();
    private ConcurrentDictionary<int, BrokerItem> asmodianBrokerItems = new ConcurrentDictionary<int, BrokerItem>();
    private ConcurrentDictionary<int, BrokerItem> asmodianSettledItems = new ConcurrentDictionary<int, BrokerItem>();
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("EXCHANGE_LOG");
    private const int DELAY_BROKER_SAVE = 6000;
    private const int DELAY_BROKER_CHECK = 60000;
    private BrokerPeriodicTaskManager saveManager;
    private ConcurrentDictionary<int, BrokerPlayerCache> playerBrokerCache = new ConcurrentDictionary<int, BrokerPlayerCache>();

    public static BrokerService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private BrokerService()
    {
        InitBrokerService();

        saveManager = new BrokerPeriodicTaskManager(DELAY_BROKER_SAVE);
        ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { CheckExpiredItems(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(DELAY_BROKER_CHECK), TimeSpan.FromMilliseconds(DELAY_BROKER_CHECK));
    }

    private void InitBrokerService()
    {
        log.LogInformation("Loading broker...");
        int loadedBrokerItemsCount = 0;
        int loadedSettledItemsCount = 0;

        List<BrokerItem> brokerItems = BrokerDAO.LoadBroker();

        foreach (BrokerItem item in brokerItems)
        {
            if (item.GetItemBrokerRace() == BrokerRace.ASMODIAN)
            {
                if (item.IsSettled())
                {
                    asmodianSettledItems[item.GetItemUniqueId()] = item;
                    loadedSettledItemsCount++;
                }
                else
                {
                    asmodianBrokerItems[item.GetItemUniqueId()] = item;
                    loadedBrokerItemsCount++;
                }
            }
            else if (item.GetItemBrokerRace() == BrokerRace.ELYOS)
            {
                if (item.IsSettled())
                {
                    elyosSettledItems[item.GetItemUniqueId()] = item;
                    loadedSettledItemsCount++;
                }
                else
                {
                    elyosBrokerItems[item.GetItemUniqueId()] = item;
                    loadedBrokerItemsCount++;
                }
            }
        }

        log.LogInformation("Broker loaded with " + loadedBrokerItemsCount + " broker items, " + loadedSettledItemsCount + " settled items.");
    }

    public void ShowRequestedItems(Player player, int clientMask, byte sortType, int startPage, List<int> itemList)
    {
        BrokerItem[] searchItems = null;
        int playerBrokerMaskCache = GetPlayerMask(player);
        BrokerItemMask brokerMaskById = BrokerItemMask.GetBrokerMaskById(clientMask);
        bool isChidrenMask = brokerMaskById.IsChildrenMask(playerBrokerMaskCache);
        if (itemList != null && clientMask == 0)
        {
            ConcurrentDictionary<int, BrokerItem> brokerItems = GetRaceBrokerItems(player.GetRace());
            if (brokerItems == null)
                return;
            searchItems = brokerItems.Values.ToArray();
        }
        else if ((GetFilteredItems(player).Length == 0 || !isChidrenMask) && clientMask != 0)
        {
            searchItems = GetItemsByMask(player, clientMask, false);
        }
        else if (isChidrenMask)
        {
            searchItems = GetItemsByMask(player, clientMask, true);
        }
        else
            searchItems = GetFilteredItems(player);

        if (searchItems == null)
            return;

        GetPlayerCache(player).SetBrokerSortTypeCache(sortType);
        GetPlayerCache(player).SetBrokerStartPageCache(startPage);

        if (itemList != null)
        {
            List<BrokerItem> itemsFound = new List<BrokerItem>();
            foreach (BrokerItem item in searchItems)
            {
                if (itemList.Contains(item.GetItemId()))
                    itemsFound.Add(item);
            }
            GetPlayerCache(player).SetSearchItemsList(itemList);
            searchItems = itemsFound.ToArray();
            GetPlayerCache(player).SetBrokerListCache(searchItems);
        }
        else
            GetPlayerCache(player).SetSearchItemsList(null);

        SortBrokerItems(searchItems, sortType);
        int totalSearchItemsCount = searchItems.Length;
        searchItems = GetRequestedPage(searchItems, startPage);

        foreach (BrokerItem bi in searchItems)
        {
            if (bi.GetAveragePrice() == 0)
            {
                bi.SetAveragePrice(GetAveragePrice(player.GetRace(), bi.GetItemId()));
            }
        }

        PacketSendUtility.SendPacket(player, new SM_BROKER_SERVICE(searchItems, totalSearchItemsCount, startPage));
    }

    public long GetAveragePrice(Race race, int itemId)
    {
        BrokerItem[] searchItems = null;

        ConcurrentDictionary<int, BrokerItem> brokerItems = GetRaceBrokerItems(race);
        if (brokerItems == null)
            return 0;

        long average = 0, sum = 0;
        int counter = 0;

        searchItems = brokerItems.Values.ToArray();

        foreach (BrokerItem item in searchItems)
        {
            if (itemId == item.GetItemId())
            {
                sum += item.GetPrice();
                counter++;
            }
        }
        average = sum / counter;
        return average;
    }

    private BrokerItem[] GetItemsByMask(Player player, int clientMask, bool cached)
    {
        List<BrokerItem> searchItems = new List<BrokerItem>();

        BrokerItemMask brokerMask = BrokerItemMask.GetBrokerMaskById(clientMask);

        if (cached)
        {
            BrokerItem[] brokerItems = GetFilteredItems(player);
            if (brokerItems == null)
                return null;

            foreach (BrokerItem item in brokerItems)
            {
                if (item == null || item.GetItem() == null)
                    continue;

                if (brokerMask.IsMatches(item.GetItem()))
                {
                    searchItems.Add(item);
                }
            }
        }
        else
        {
            ConcurrentDictionary<int, BrokerItem> brokerItems = GetRaceBrokerItems(player.GetRace());
            if (brokerItems == null)
                return null;
            foreach (BrokerItem item in brokerItems.Values)
            {
                if (item == null || item.GetItem() == null)
                    continue;

                if (brokerMask.IsMatches(item.GetItem()))
                {
                    searchItems.Add(item);
                }
            }
        }

        BrokerItem[] items = searchItems.ToArray();
        GetPlayerCache(player).SetBrokerListCache(items);
        GetPlayerCache(player).SetBrokerMaskCache(clientMask);

        return items;
    }

    private void SortBrokerItems(BrokerItem[] brokerItems, byte sortType)
    {
        Array.Sort(brokerItems, BrokerItem.GetComparatoryByType(sortType));
    }

    private BrokerItem[] GetRequestedPage(BrokerItem[] brokerItems, int startPage)
    {
        List<BrokerItem> page = new List<BrokerItem>();
        int startingElement = startPage * 9;

        for (int i = startingElement, limit = 0; i < brokerItems.Length && limit < 45; i++, limit++)
        {
            page.Add(brokerItems[i]);
        }

        return page.ToArray();
    }

    private ConcurrentDictionary<int, BrokerItem> GetRaceBrokerItems(Race race)
    {
        switch (race)
        {
            case Race.ELYOS:
                return elyosBrokerItems;
            case Race.ASMODIANS:
                return asmodianBrokerItems;
            default:
                return null;
        }
    }

    private ConcurrentDictionary<int, BrokerItem> GetRaceBrokerSettledItems(Race race)
    {
        switch (race)
        {
            case Race.ELYOS:
                return elyosSettledItems;
            case Race.ASMODIANS:
                return asmodianSettledItems;
            default:
                return null;
        }
    }

    public void BuyBrokerItem(Player player, int itemUniqueId, long itemCount)
    {
        bool isEmptyCache = GetFilteredItems(player).Length == 0;
        Race playerRace = player.GetRace();

        if (!PlayerRestrictions.CanTrade(player))
            return;

        lock (this)
        {
            BrokerItem buyingItem = GetRaceBrokerItems(playerRace).GetValueOrDefault(itemUniqueId);
            if (buyingItem == null)
                return; // TODO: Message "this item has already been bought, refresh page please."

            if (buyingItem.GetSellerId() == player.GetObjectId())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_VENDOR_CAN_NOT_BUY_MY_REGISTER_ITEM());
                return;
            }

            if (buyingItem.IsSold() || buyingItem.IsCanceled())
            {
                NullLoggerFactory.Instance.CreateLogger(nameof(BrokerService)).LogWarning(
                    "Player {Name} tried to buy the following item[id={ItemId}, objId={ObjId}, sellerId={SellerId}, sellerName={SellerName}, sold={Sold}, canceled={Canceled}, settled={Settled}, expireTime={ExpireTime}] which is already sold or canceled",
                    player.GetName(), buyingItem.GetItemId(), buyingItem.GetItemUniqueId(), buyingItem.GetSellerId(),
                    PlayerService.GetPlayerName(buyingItem.GetSellerId()), buyingItem.IsSold(), buyingItem.IsCanceled(), buyingItem.IsSettled(),
                    buyingItem.GetExpireTime());
                PacketSendUtility.SendMessage(player, "Sorry, but this item already sold");
                return;
            }

            Item item = buyingItem.GetItem();
            long price = buyingItem.GetPrice() * itemCount;
            if (player.GetInventory().IsFull(item.GetItemTemplate().GetExtraInventoryId()))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_FULL_INVENTORY());
                return;
            }

            if (player.GetInventory().GetKinah() < price)
                return;

            if (buyingItem.GetItemCount() > itemCount && buyingItem.IsSplittingAvailable())
            {
                buyingItem.DecreaseItemCount(itemCount);
                buyingItem.SetPersistentState(PersistentState.UPDATE_REQUIRED);
                // storing old broker item with rest items to sell
                BrokerOpSaveTask bost = new BrokerOpSaveTask(buyingItem, buyingItem.GetItem(), player.GetInventory().GetKinahItem(), player.GetObjectId());
                saveManager.Add(bost);
                // creating new broker item which will be settled
                BrokerItem soldItem = new BrokerItem(ItemFactory.NewItem(buyingItem.GetItemId(), itemCount), buyingItem.GetPrice(), buyingItem.GetSellerId(),
                    buyingItem.IsSplittingAvailable(), buyingItem.GetItemBrokerRace());
                buyingItem = soldItem;
                item = buyingItem.GetItem();
                BrokerOpSaveTask bost2 = new BrokerOpSaveTask(buyingItem, buyingItem.GetItem(), player.GetInventory().GetKinahItem(), player.GetObjectId());
                saveManager.Add(bost2);
            }
            else
            {
                GetRaceBrokerItems(playerRace).TryRemove(itemUniqueId, out _);
            }

            PutToSettled(playerRace, buyingItem, true);

            if (!isEmptyCache)
            {
                // ArrayUtils.removeElement → List.Remove (removes first occurrence)
                List<BrokerItem> tmpCache = new List<BrokerItem>(GetFilteredItems(player));
                tmpCache.Remove(buyingItem);
                BrokerItem[] newCache = tmpCache.ToArray();
                GetPlayerCache(player).SetBrokerListCache(newCache);
            }

            player.GetInventory().DecreaseKinah(price);
            // unpack
            if (item.GetPackCount() > 0)
            {
                item.SetPackCount(item.GetPackCount() * -1);
            }
            Item boughtItem = player.GetInventory().Add(item, ItemPacketService.ItemAddType.BROKER_BUY);

            if (LoggingConfig.LOG_BROKER_EXCHANGE)
                log.LogInformation("Player: " + player.GetName() + " bought item " + boughtItem.GetItemId() + " [" + boughtItem.GetItemName() + "] (count: " + itemCount
                    + ") from player: " + PlayerService.GetPlayerName(buyingItem.GetSellerId()) + " (total price: " + price + ")");

            // create save task
            BrokerOpSaveTask bost3 = new BrokerOpSaveTask(buyingItem, boughtItem, player.GetInventory().GetKinahItem(), player.GetObjectId());
            saveManager.Add(bost3);
        }
        ShowRequestedItems(player, GetPlayerCache(player).GetBrokerMaskCache(), GetPlayerCache(player).GetBrokerSortTypeCache(),
            GetPlayerCache(player).GetBrokerStartPageCache(), GetPlayerCache(player).GetSearchItemList());
    }

    private void PutToSettled(Race race, BrokerItem brokerItem, bool isSold)
    {
        if (isSold)
            brokerItem.RemoveItem();
        else
            brokerItem.SetSettled();

        brokerItem.SetPersistentState(PersistentState.UPDATE_REQUIRED);

        switch (race)
        {
            case Race.ASMODIANS:
                asmodianSettledItems[brokerItem.GetItemUniqueId()] = brokerItem;
                break;

            case Race.ELYOS:
                elyosSettledItems[brokerItem.GetItemUniqueId()] = brokerItem;
                break;
        }
        saveManager.Add(new BrokerOpSaveTask(brokerItem));
        Player seller = World.World.GetInstance().GetPlayer(brokerItem.GetSellerId());
        if (seller != null)
        {
            PacketSendUtility.SendPacket(seller, new SM_BROKER_SERVICE(true, GetEarnedKinahFromSoldItems(seller.GetRace(), seller.GetObjectId())));
            // TODO: Retail system message
        }
    }

    private int GetRegisteredItemsCount(Player player)
    {
        int playerId = player.GetObjectId();
        int c = 0;
        foreach (BrokerItem item in GetRaceBrokerItems(player.GetRace()).Values)
        {
            if (item != null && playerId == item.GetSellerId())
                c++;
        }
        return c;
    }

    public void RegisterItem(Player player, int itemUniqueId, long count, long price, bool splittingAvailable)
    {
        Item itemToRegister = player.GetInventory().GetItemByObjId(itemUniqueId);
        Race playerRace = player.GetRace();

        if (itemToRegister == null || count > itemToRegister.GetItemCount())
            return;

        if (!PlayerRestrictions.CanTrade(player))
        {
            return;
        }

        if (price <= 0 || count <= 0)
            return;

        if (count > 1 && price / count > 999_999_999 || price > 99_999_999_999L) // retail price limits
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LIMITED_VENDOR_CANT_OVER_GOLD());
            return;
        }

        // Check Trade Hack
        if (itemToRegister.GetPackCount() <= 0 && !itemToRegister.IsTradeable())
            return;

        if (!AdminService.GetInstance().CanOperate(player, null, itemToRegister, "broker"))
            return;

        BrokerRace brRace;

        if (playerRace == Race.ASMODIANS)
            brRace = BrokerRace.ASMODIAN;
        else if (playerRace == Race.ELYOS)
            brRace = BrokerRace.ELYOS;
        else
            return;

        int registeredItemsCount = GetRegisteredItemsCount(player);
        long registrationCommition = 0;
        if (registeredItemsCount > 14)
        {
            PacketSendUtility.SendPacket(player, new SM_BROKER_SERVICE(BrokerMessages.NO_SPACE_AVAIABLE.GetId()));
            return;
        }
        else if (registeredItemsCount > 9) // round down in order to match client prices
            registrationCommition = (long)(price * count * 0.04f);
        else
            registrationCommition = (long)(price * count * 0.02f);

        if (registrationCommition < 10)
            registrationCommition = 10;
        else
            registrationCommition = PricesService.GetPriceForService(registrationCommition, player.GetRace());

        if (player.GetInventory().GetKinah() < registrationCommition)
        {
            PacketSendUtility.SendPacket(player, new SM_BROKER_SERVICE(BrokerMessages.NO_ENOUGHT_KINAH.GetId()));
            return;
        }
        if (!itemToRegister.GetItemTemplate().IsStackable())
            splittingAvailable = false;

        player.GetInventory().DecreaseKinah(registrationCommition);
        if (itemToRegister.GetItemTemplate().IsStackable() && count < itemToRegister.GetItemCount())
        {
            int itemId = itemToRegister.GetItemId();
            player.GetInventory().DecreaseItemCount(itemToRegister, count);
            itemToRegister = ItemFactory.NewItem(itemId, count);
        }
        else
        {
            player.GetInventory().Remove(itemToRegister);
            PacketSendUtility.SendPacket(player, new SM_DELETE_ITEM(itemToRegister.GetObjectId()));
        }

        itemToRegister.SetItemLocation(StorageType.BROKER.GetId());

        BrokerItem newBrokerItem = new BrokerItem(itemToRegister, price, player.GetObjectId(), splittingAvailable, brRace);

        switch (brRace)
        {
            case BrokerRace.ASMODIAN:
                asmodianBrokerItems[newBrokerItem.GetItemUniqueId()] = newBrokerItem;
                break;

            case BrokerRace.ELYOS:
                elyosBrokerItems[newBrokerItem.GetItemUniqueId()] = newBrokerItem;
                break;
        }

        BrokerOpSaveTask bost = new BrokerOpSaveTask(newBrokerItem, itemToRegister, player.GetInventory().GetKinahItem(), player.GetObjectId());
        saveManager.Add(bost);

        PacketSendUtility.SendPacket(player, new SM_BROKER_SERVICE(newBrokerItem, 0, registeredItemsCount));
    }

    public void ShowSellWindow(Player player, int itemUniqueId)
    {
        Item itemToRegister = player.GetInventory().GetItemByObjId(itemUniqueId);
        if (itemToRegister == null)
            return;
        List<long> prices = GetRaceBrokerItems(player.GetRace()).Values
            .Where(item => itemToRegister.GetItemId() == item.GetItemId())
            .Select(item => item.GetPrice())
            .ToList();
        long lowestPrice = prices.Count == 0 ? 0 : prices.Min();
        long highestPrice = prices.Count == 0 ? 0 : prices.Max();
        PacketSendUtility.SendPacket(player, new SM_BROKER_SERVICE((byte)0, itemUniqueId, lowestPrice, highestPrice));
    }

    public void ShowRegisteredItems(Player player)
    {
        ConcurrentDictionary<int, BrokerItem> brokerItems = GetRaceBrokerItems(player.GetRace());

        List<BrokerItem> registeredItems = new List<BrokerItem>();
        int playerId = player.GetObjectId();

        foreach (BrokerItem item in brokerItems.Values)
        {
            if (item != null && item.GetItem() != null && playerId == item.GetSellerId())
                registeredItems.Add(item);
        }

        PacketSendUtility.SendPacket(player, new SM_BROKER_SERVICE(registeredItems.ToArray()));
    }

    public bool HasRegisteredItems(Player player)
    {
        ConcurrentDictionary<int, BrokerItem> brokerItems = GetRaceBrokerItems(player.GetRace());
        foreach (BrokerItem item in brokerItems.Values)
        {
            if (item != null && item.GetItem() != null && player.GetObjectId() == item.GetSellerId())
                return true;
        }

        return false;
    }

    public void CancelRegisteredItem(Player player, int brokerItemId)
    {
        ConcurrentDictionary<int, BrokerItem> brokerItems = GetRaceBrokerItems(player.GetRace());
        BrokerItem brokerItem = brokerItems.GetValueOrDefault(brokerItemId);

        if (!PlayerRestrictions.CanTrade(player))
        {
            return;
        }
        if (brokerItem != null)
        {
            if (brokerItem.GetSellerId() != player.GetObjectId())
            {
                log.LogInformation("[AUDIT] Player: {Name} tried to get item from broker that he doesn't own", player.GetName());
                return;
            }
            if (player.GetInventory().IsFull(brokerItem.GetItem().GetItemTemplate().GetExtraInventoryId()))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_EXCHANGE_FULL_INVENTORY());
                return;
            }
            lock (this)
            {
                player.GetInventory().Add(brokerItem.GetItem(), ItemPacketService.ItemAddType.BROKER_RETURN);
                brokerItem.SetPersistentState(PersistentState.DELETED);
                saveManager.Add(new BrokerOpSaveTask(brokerItem));
                brokerItem.SetIsCanceled(true);
                brokerItems.TryRemove(brokerItemId, out _);
                PacketSendUtility.SendPacket(player, new SM_BROKER_SERVICE((byte)0, brokerItemId));
            }
        }
        ShowRegisteredItems(player);
    }

    public void ShowSettledItems(Player player, int startPageIndex)
    {
        int itemsPerPage = 9;
        List<BrokerItem> settledItems = GetSettledItemsForPlayer(player.GetRace(), player.GetObjectId());
        List<BrokerItem> itemsToSend = settledItems.GetRange(itemsPerPage * startPageIndex, settledItems.Count - itemsPerPage * startPageIndex);
        SplitList<BrokerItem> itemSplitList = new DynamicServerPacketBodySplitList<BrokerItem>(itemsToSend, true, SM_BROKER_SERVICE.SETTLED_ITEMS_STATIC_BODY_SIZE,
            SM_BROKER_SERVICE.SETTLED_ITEMS_DYNAMIC_BODY_PART_SIZE_CALCULATOR);
        ListPart<BrokerItem> pagesToSend = itemSplitList.Iterator().Next(); // client only supports one packet worth of pages
        int lastFullPageIndex = pagesToSend.IsLast() || pagesToSend.Size() <= itemsPerPage ? pagesToSend.Size() : pagesToSend.Size() - pagesToSend.Size() % itemsPerPage;
        List<BrokerItem> firstFullPages = pagesToSend.SubList(0, lastFullPageIndex); // incomplete pages create gaps, so we trim sent items to full pages
        PacketSendUtility.SendPacket(player, new SM_BROKER_SERVICE(firstFullPages, settledItems.Count, startPageIndex, ExtractEarnedKinahForSoldItems(settledItems)));
    }

    private List<BrokerItem> GetSettledItemsForPlayer(Race playerRace, int playerId)
    {
        ConcurrentDictionary<int, BrokerItem> settledItemsForRace = GetRaceBrokerSettledItems(playerRace);
        if (settledItemsForRace == null)
            return new List<BrokerItem>();
        return settledItemsForRace.Values.Where(item => item != null).Where(item => item.GetSellerId() == playerId).ToList();
    }

    private long ExtractEarnedKinahForSoldItems(ICollection<BrokerItem> items)
    {
        if (items == null || items.Count == 0)
            return 0;
        return items.Where(item => item != null).Where(item => item.IsSold()).Sum(item => item.GetPrice() * item.GetItemCount());
    }

    public long GetEarnedKinahFromSoldItems(PlayerCommonData playerCommonData)
    {
        return GetEarnedKinahFromSoldItems(playerCommonData.GetRace(), playerCommonData.GetPlayerObjId());
    }

    private long GetEarnedKinahFromSoldItems(Race playerRace, int playerId)
    {
        return ExtractEarnedKinahForSoldItems(GetSettledItemsForPlayer(playerRace, playerId));
    }

    public void SettleAccount(Player player)
    {
        Race playerRace = player.GetRace();
        ConcurrentDictionary<int, BrokerItem> brokerSettledItems = GetRaceBrokerSettledItems(playerRace);
        List<BrokerItem> collectedItems = new List<BrokerItem>();
        int playerId = player.GetObjectId();
        long kinahCollect = 0;
        bool itemsLeft = false;

        foreach (BrokerItem item in brokerSettledItems.Values)
        {
            if (item.GetSellerId() == playerId)
                collectedItems.Add(item);
        }

        foreach (BrokerItem item in collectedItems)
        {
            if (item.IsSold())
            {
                bool result = false;
                switch (playerRace)
                {
                    case Race.ASMODIANS:
                        result = asmodianSettledItems.TryRemove(item.GetItemUniqueId(), out _);
                        break;
                    case Race.ELYOS:
                        result = elyosSettledItems.TryRemove(item.GetItemUniqueId(), out _);
                        break;
                }

                if (result)
                {
                    item.SetPersistentState(PersistentState.DELETED);
                    saveManager.Add(new BrokerOpSaveTask(item));
                    kinahCollect += item.GetPrice() * item.GetItemCount();
                }
            }
            else
            {
                if (item.GetItem() != null)
                {
                    Item resultItem = player.GetInventory().Add(item.GetItem());
                    if (resultItem != null)
                    {
                        bool result = false;
                        switch (playerRace)
                        {
                            case Race.ASMODIANS:
                                result = asmodianSettledItems.TryRemove(item.GetItemUniqueId(), out _);
                                break;
                            case Race.ELYOS:
                                result = elyosSettledItems.TryRemove(item.GetItemUniqueId(), out _);
                                break;
                        }
                        if (result)
                        {
                            item.SetPersistentState(PersistentState.DELETED);
                            saveManager.Add(new BrokerOpSaveTask(item));
                        }
                    }
                    else
                        itemsLeft = true;
                }
                else
                    log.LogWarning("Broker settled item missed. ObjID: " + item.GetItemUniqueId());
            }
        }

        player.GetInventory().IncreaseKinah(kinahCollect);

        ShowSettledItems(player, 0);

        if (!itemsLeft)
            PacketSendUtility.SendPacket(player, new SM_BROKER_SERVICE(false, 0));
    }

    private void CheckExpiredItems()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (Race race in new[] { Race.ASMODIANS, Race.ELYOS })
        {
            ConcurrentDictionary<int, BrokerItem> brokerItems = GetRaceBrokerItems(race);
            foreach (BrokerItem item in brokerItems.Values)
            {
                if (item != null && item.GetExpireTime().ToUnixTimeMilliseconds() <= now)
                {
                    lock (this)
                    {
                        PutToSettled(race, item, false);
                        brokerItems.TryRemove(item.GetItemUniqueId(), out _);
                    }
                }
            }
        }
    }

    public void OnPlayerLogin(Player player)
    {
        List<BrokerItem> settledItemsForPlayer = GetSettledItemsForPlayer(player.GetRace(), player.GetObjectId());
        if (settledItemsForPlayer.Count != 0)
            PacketSendUtility.SendPacket(player, new SM_BROKER_SERVICE(true, ExtractEarnedKinahForSoldItems(settledItemsForPlayer)));
    }

    private BrokerPlayerCache GetPlayerCache(Player player)
    {
        BrokerPlayerCache cacheEntry = playerBrokerCache.GetValueOrDefault(player.GetObjectId());
        if (cacheEntry == null)
        {
            cacheEntry = new BrokerPlayerCache();
            playerBrokerCache[player.GetObjectId()] = cacheEntry;
        }
        return cacheEntry;
    }

    public void RemovePlayerCache(Player player)
    {
        playerBrokerCache.TryRemove(player.GetObjectId(), out _);
    }

    public void OnPlayerDeleted(int playerId)
    {
        foreach (Race playerRace in new[] { Race.ELYOS, Race.ASMODIANS })
        {
            ConcurrentDictionary<int, BrokerItem> brokerItems = GetRaceBrokerItems(playerRace);
            if (brokerItems != null)
            {
                lock (brokerItems)
                {
                    foreach (KeyValuePair<int, BrokerItem> kv in brokerItems.ToArray())
                        if (kv.Value.GetSellerId() == playerId)
                            brokerItems.TryRemove(kv.Key, out _);
                }
            }
            brokerItems = GetRaceBrokerSettledItems(playerRace);
            if (brokerItems != null)
            {
                lock (brokerItems)
                {
                    foreach (KeyValuePair<int, BrokerItem> kv in brokerItems.ToArray())
                        if (kv.Value.GetSellerId() == playerId)
                            brokerItems.TryRemove(kv.Key, out _);
                }
            }
        }
    }

    private int GetPlayerMask(Player player)
    {
        return GetPlayerCache(player).GetBrokerMaskCache();
    }

    private BrokerItem[] GetFilteredItems(Player player)
    {
        return GetPlayerCache(player).GetBrokerListCache();
    }

    /// <summary>Frequent running save task</summary>
    public sealed class BrokerPeriodicTaskManager : AbstractFIFOPeriodicTaskManager<BrokerOpSaveTask>
    {
        private const string CALLED_METHOD_NAME = "brokerOperation()";

        public BrokerPeriodicTaskManager(int period)
            : base(period)
        {
        }

        protected override void CallTask(BrokerOpSaveTask task)
        {
            task.Run();
        }

        protected override string GetCalledMethodName()
        {
            return CALLED_METHOD_NAME;
        }
    }

    /// <summary>This class is used for storing all items in one shot after any broker operation (Java implements Runnable→Run()).</summary>
    public sealed class BrokerOpSaveTask
    {
        private BrokerItem brokerItem;
        private Item item;
        private Item kinahItem;
        private int playerId;

        internal BrokerOpSaveTask(BrokerItem brokerItem, Item item, Item kinahItem, int playerId)
        {
            this.brokerItem = brokerItem;
            this.item = item;
            this.kinahItem = kinahItem;
            this.playerId = playerId;
        }

        public BrokerOpSaveTask(BrokerItem brokerItem)
        {
            this.brokerItem = brokerItem;
        }

        public void Run()
        {
            // first save item for FK consistency
            if (item != null)
            {
                InventoryDAO.Store(item, playerId);
                ItemStoneListDAO.Save(new List<Item> { item });
            }
            if (brokerItem != null)
                BrokerDAO.Store(brokerItem);
            if (kinahItem != null)
                InventoryDAO.Store(kinahItem, playerId);
        }
    }

    private static class SingletonHolder
    {
        internal static readonly BrokerService instance = new BrokerService();
    }
}
