using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using Aion.GameServer.Configs.Ingameshop;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Mail;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Network.LoginServer.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.Ingameshop;

/// <summary>Java parity: model/ingameshop/InGameShopEn (KID, xTz). Singleton. Map<Byte,List<IGItem>>→Dictionary<sbyte,List<IGItem>>; AtomicInteger→Interlocked; ConcurrentHashMap→ConcurrentDictionary; TreeMap+DescFilter→SortedDictionary w/ desc IComparer; Collections.emptyList→new List; equalsIgnoreCase→OrdinalIgnoreCase; Timestamp(currentTimeMillis)→DateTimeOffset.UtcNow. DAOs/LoginServer/IGRequest/packets/services red-tolerated.</summary>
public class InGameShopEn
{
    private static InGameShopEn instance = new();
    private readonly ILogger log = NullLogger.Instance;

    public static InGameShopEn GetInstance()
    {
        return instance;
    }

    private Dictionary<sbyte, List<IGItem>> items;
    private InGameShopProperty iGProperty;
    private int lastRequestId = 0;
    private ConcurrentDictionary<int, IGRequest> activeRequests;

    public InGameShopEn()
    {
        if (!InGameShopConfig.ENABLE_IN_GAME_SHOP)
        {
            log.LogInformation("InGameShop is disabled.");
            return;
        }
        iGProperty = InGameShopProperty.Load();
        activeRequests = new ConcurrentDictionary<int, IGRequest>();
        items = InGameShopDAO.LoadInGameShopItems();
        log.LogInformation("Loaded with " + items.Count + " items.");
    }

    public InGameShopProperty GetIGSProperty()
    {
        return iGProperty;
    }

    public void Reload()
    {
        if (!InGameShopConfig.ENABLE_IN_GAME_SHOP)
        {
            log.LogInformation("InGameShop is disabled.");
            return;
        }
        iGProperty.Clear();
        iGProperty = InGameShopProperty.Load();
        items = InGameShopDAO.LoadInGameShopItems();
        log.LogInformation("Loaded with " + items.Count + " items.");
    }

    public IGItem GetIGItem(int id)
    {
        foreach (sbyte key in items.Keys)
        {
            foreach (IGItem item in items[key])
            {
                if (item.GetObjectId() == id)
                {
                    return item;
                }
            }
        }
        return null;
    }

    public ICollection<IGItem> GetItems(sbyte category)
    {
        if (!items.ContainsKey(category))
        {
            return new List<IGItem>();
        }
        return this.items[category];
    }

    public List<int> GetTopSales(int subCategory, sbyte category)
    {
        sbyte max = 6;
        SortedDictionary<int, int> map = new(new DescFilter());
        if (!items.ContainsKey(category))
        {
            return new List<int>();
        }
        foreach (IGItem item in this.items[category])
        {
            if (item.GetSalesRanking() == 0)
                continue;

            if (subCategory != 2 && item.GetSubCategory() != subCategory)
                continue;

            map[item.GetSalesRanking()] = item.GetObjectId();
        }
        List<int> top = new();
        sbyte cnt = 0;
        foreach (int objId in map.Values)
        {
            if (cnt <= max)
            {
                top.Add(objId);
                cnt++;
            }
            else
                break;
        }
        map.Clear();
        return top;
    }

    private class DescFilter : IComparer<int>
    {
        public int Compare(int o1, int o2)
        {
            int i1 = o1;
            int i2 = o2;
            return -i1.CompareTo(i2);
        }
    }

    public int GetMaxList(sbyte subCategoryId, sbyte category)
    {
        int id = 0;
        if (!items.ContainsKey(category))
        {
            return id;
        }
        foreach (IGItem item in items[category])
        {
            if (item.GetSubCategory() == subCategoryId)
                if (item.GetList() > id)
                    id = item.GetList();
        }

        return id;
    }

    public void BuyItemRequest(Player player, int itemObjId)
    {
        if (player.GetInventory().IsFull())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DICE_INVEN_ERROR());
            return;
        }

        IGItem item = InGameShopEn.GetInstance().GetIGItem(itemObjId);
        IGRequest request = new(Interlocked.Increment(ref lastRequestId), player.GetObjectId(), itemObjId);
        request.accountId = player.GetClientConnection().GetAccount().GetId();
        if (LoginServer.GetInstance().SendPacket(new SM_PREMIUM_CONTROL(request, item.GetItemPrice())))
            activeRequests[request.requestId] = request;
        if (LoggingConfig.LOG_INGAMESHOP)
            log.LogInformation("[INGAMESHOP] > " + player + " (" + player.GetAccount() + ") is watching item:" + item.GetItemId() + " cost " + item.GetItemPrice() + " toll.");
    }

    public void GiftItemRequest(Player player, string receiver, string message, int itemObjId)
    {
        if (receiver.Equals(player.GetName(), StringComparison.OrdinalIgnoreCase))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INGAMESHOP_CANNOT_GIVE_TO_ME());
            return;
        }

        if (!InGameShopConfig.ALLOW_GIFTS)
        {
            PacketSendUtility.SendMessage(player, "Gifts are disabled.");
            return;
        }

        if (!PlayerDAO.IsNameUsed(receiver))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INGAMESHOP_NO_USER_TO_GIFT());
            return;
        }

        PlayerCommonData recipientCommonData = PlayerService.GetOrLoadPlayerCommonData(receiver);
        if (recipientCommonData.GetMailboxLetters() >= 100)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MAIL_MSG_RECIPIENT_MAILBOX_FULL(recipientCommonData.GetName()));
            return;
        }

        if (!InGameShopConfig.ENABLE_GIFT_OTHER_RACE && !player.IsStaff())
            if (player.GetRace() != recipientCommonData.GetRace())
            {
                PacketSendUtility.SendPacket(player, new SM_MAIL_SERVICE(MailMessage.MAIL_IS_ONE_RACE_ONLY));
                return;
            }

        IGItem item = GetIGItem(itemObjId);
        IGRequest request = new(Interlocked.Increment(ref lastRequestId), player.GetObjectId(), receiver, message, itemObjId);
        request.accountId = player.GetClientConnection().GetAccount().GetId();
        if (LoginServer.GetInstance().SendPacket(new SM_PREMIUM_CONTROL(request, item.GetItemPrice())))
            activeRequests[request.requestId] = request;
    }

    public void AddToll(Player player, long cnt)
    {
        if (InGameShopConfig.ENABLE_IN_GAME_SHOP)
        {
            IGRequest request = new(Interlocked.Increment(ref lastRequestId), player.GetObjectId(), 0);
            request.accountId = player.GetClientConnection().GetAccount().GetId();
            PacketSendUtility.SendMessage(player, "You received " + cnt + " Toll");
            if (LoginServer.GetInstance().SendPacket(new SM_PREMIUM_CONTROL(request, cnt * -1)))
                activeRequests[request.requestId] = request;
        }
        else
        {
            PacketSendUtility.SendMessage(player, "You can't add toll if ingameshop is disabled!");
        }
    }

    public void AddToll(int playerId, long cnt)
    {
        if (InGameShopConfig.ENABLE_IN_GAME_SHOP)
        {
            IGRequest request = new(Interlocked.Increment(ref lastRequestId), playerId, 0);
            request.accountId = playerId;
            if (LoginServer.GetInstance().SendPacket(new SM_PREMIUM_CONTROL(request, cnt * -1)))
                activeRequests[request.requestId] = request;
        }
    }

    public bool DecreaseToll(Player player, long price)
    {
        if (LoginServer.GetInstance().SendPacket(
            new SM_ACCOUNT_TOLL_INFO(player.GetClientConnection().GetAccount().GetToll() - price, player.GetAccount().GetId())))
        {
            player.GetClientConnection().GetAccount().SetToll(player.GetClientConnection().GetAccount().GetToll() - price);
            PacketSendUtility.SendPacket(player, new SM_TOLL_INFO(player.GetClientConnection().GetAccount().GetToll()));
            return true;
        }
        else
        {
            PacketSendUtility.SendMessage(player, "ls communication error.");
            return false;
        }
    }

    public void FinishRequest(int requestId, int result, long toll)
    {
        IGRequest request = this.activeRequests[requestId];
        if (request.requestId == requestId)
        {
            Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(request.playerId);
            if (player != null)
            {
                if (result == 1)
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INGAMESHOP_ERROR());
                }
                else if (result == 2)
                {
                    IGItem item = GetIGItem(request.itemObjId);
                    if (item == null)
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INGAMESHOP_ERROR());
                        log.LogError("player " + player.GetName() + " requested " + request.itemObjId + " that was not exists in list.");
                        return;
                    }
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INGAMESHOP_NOT_ENOUGH_CASH("Toll"));
                    PacketSendUtility.SendPacket(player, new SM_TOLL_INFO(toll));
                    if (LoggingConfig.LOG_INGAMESHOP)
                        log.LogInformation("[INGAMESHOP] > " + player + " (" + player.GetAccount() + ") has not bought item: " + item.GetItemId() + " count: " + item.GetItemCount() + " Cause: NOT ENOUGH TOLLS");
                }
                else if (result == 3)
                {
                    // uses for lottery
                    if (request.itemObjId == 0)
                    {
                        PacketSendUtility.SendPacket(player, new SM_TOLL_INFO(toll));
                        player.GetClientConnection().GetAccount().SetToll(toll);
                        return;
                    }

                    IGItem item = GetIGItem(request.itemObjId);
                    if (item == null)
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INGAMESHOP_ERROR());
                        log.LogError("player " + player.GetName() + " requested " + request.itemObjId + " that was not exists in list.");
                        return;
                    }

                    if (request.gift)
                    {
                        SystemMailService.SendMail(player.GetName(), request.receiver, "In Game Shop", request.message, item.GetItemId(), item.GetItemCount(), 0,
                            LetterType.BLACKCLOUD);
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INGAMESHOP_GIFT_SUCCESS());
                        if (LoggingConfig.LOG_INGAMESHOP)
                            log.LogInformation("[INGAMESHOP] > " + player + " (" + player.GetAccount() + ") BUY ITEM: " + item.GetItemId() + " COUNT: " + item.GetItemCount() + " FOR PlayerName: " + request.receiver);
                        if (LoggingConfig.LOG_INGAMESHOP_SQL)
                            InGameShopLogDAO.Log("GIFT", DateTimeOffset.UtcNow, player.GetName(),
                                player.GetAccountName(), request.receiver, item.GetItemId(), item.GetItemCount(), item.GetItemPrice());
                    }
                    else
                    {
                        ItemService.AddItem(player, item.GetItemId(), item.GetItemCount());
                        if (LoggingConfig.LOG_INGAMESHOP)
                            log.LogInformation("[INGAMESHOP] > " + player + " (" + player.GetAccount() + ") BUY ITEM: " + item.GetItemId() + " COUNT: " + item.GetItemCount());
                        if (LoggingConfig.LOG_INGAMESHOP_SQL)
                            InGameShopLogDAO.Log("BUY", DateTimeOffset.UtcNow, player.GetName(),
                                player.GetAccountName(), player.GetName(), item.GetItemId(), item.GetItemCount(), item.GetItemPrice());
                        InventoryDAO.Store(player);
                    }
                    item.IncreaseSales();
                    InGameShopDAO.IncreaseSales(item.GetObjectId(), item.GetSalesRanking());
                    PacketSendUtility.SendPacket(player, new SM_TOLL_INFO(toll));
                    player.GetClientConnection().GetAccount().SetToll(toll);
                }
                else if (result == 4)
                {
                    player.GetClientConnection().GetAccount().SetToll(toll);
                    PacketSendUtility.SendPacket(player, new SM_TOLL_INFO(toll));
                }
            }

            activeRequests.TryRemove(request.requestId, out _);
        }
    }
}
