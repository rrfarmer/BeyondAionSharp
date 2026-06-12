using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Taskmanager.Tasks.Housing;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/HousingBidService (Rolandas, Neon). House auction bids. **DayOfWeek.getValue() (ISO Mon=1..Sun=7) → C# conversion (Sunday?7:(int)DayOfWeek)**; HouseBids.bid()→DoBid() (prior method/Bid-class collision rename); ConcurrentHashMap→ConcurrentDictionary (putIfAbsent→!TryAdd, get→GetValueOrDefault, remove(k,v)→TryRemove(KVP), remove(k)→TryRemove(out)); stream filter/findAny→Where/FirstOrDefault, anyMatch→Any, reduce(maxBy(comparing))→MaxBy; Persistable.PersistentState→IPersistable.PersistentState; ZonedDateTime→DateTimeOffset; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; Integer.parseInt→int.Parse; switch(HouseType). HouseBids/Bid converged; House/AuctionResult/MailFormatter/AuctionEndTask/DAO red-tolerated.</summary>
public class HousingBidService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("HOUSE_AUCTION_LOG");
    private static readonly HousingBidService instance = new HousingBidService();
    private readonly ConcurrentDictionary<int, HouseBids> bids = new ConcurrentDictionary<int, HouseBids>();

    private HousingBidService()
    {
        ISet<int> deletedPlayerIds = HouseBidsDAO.LoadBids(bids);
        foreach (int id in deletedPlayerIds)
            DisableBids(id);
        SetBidInfoToHouses();
        log.LogInformation("Loaded bids for " + bids.Count + " houses");
    }

    private void SetBidInfoToHouses()
    {
        foreach (House house in HousingService.GetInstance().GetCustomHouses())
        {
            house.SetBids(GetBidInfo(house), true);
            if (house.GetBids() != null && house.IsInactive())
                log.LogWarning(house + " is for auction but inactive.");
        }
    }

    public static HousingBidService GetInstance()
    {
        return instance;
    }

    public bool IsRegisteringAllowed()
    {
        if (!HousingConfig.ENABLE_HOUSE_AUCTIONS)
            return false;
        DateTimeOffset now = ServerTime.Now();
        // Java DayOfWeek.getValue(): ISO Mon=1..Sun=7
        int today = now.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)now.DayOfWeek;
        int from = HousingConfig.HOUSE_AUCTION_REGISTER_DAYS[0];
        int to = HousingConfig.HOUSE_AUCTION_REGISTER_DAYS[1];
        if (from > to) // e.g. saturday (6) to wednesday (3)
            return from <= today || to >= today;
        else // e.g. monday (1) to friday (5)
            return from <= today && to >= today;
    }

    public bool Auction(House house, long initialPrice)
    {
        HouseBids houseBids = new HouseBids(house.GetObjectId(), initialPrice);
        HouseBids.Bid bid = houseBids.GetHighestBid();
        if (!bids.TryAdd(house.GetObjectId(), houseBids))
            return false;
        if (house.GetPersistentState() == IPersistable.PersistentState.NEW) // house must exist in DB before saving a bid due to foreign key
            house.Save();
        if (!HouseBidsDAO.AddBid(bid))
        {
            bids.TryRemove(new KeyValuePair<int, HouseBids>(house.GetObjectId(), houseBids));
            return false;
        }
        house.SetBids(houseBids, true);
        house.GetController().UpdateSign();
        house.GetController().UpdateAppearance();
        return true;
    }

    private bool IsAuctionOpen(int houseObjectId)
    {
        return bids.ContainsKey(houseObjectId) && IsBiddingTime(houseObjectId);
    }

    private bool IsBiddingTime(int houseObjectId)
    {
        DateTimeOffset now = ServerTime.Now();
        return now.DayOfWeek != DayOfWeek.Sunday || now.Hour < 12 || AuctionEndTask.GetInstance().IsAuctionProlonged(houseObjectId);
    }

    public HouseBids GetBidInfo(House house)
    {
        return bids.GetValueOrDefault(house.GetObjectId());
    }

    public List<HouseBids> GetBidInfo(Race race)
    {
        List<HouseBids> houseBids = new List<HouseBids>();
        foreach (HouseBids bidInfo in bids.Values)
        {
            if (HousingService.GetInstance().FindHouse(bidInfo.GetHouseObjectId()).MatchesLandRace(race))
                houseBids.Add(bidInfo);
        }
        return houseBids;
    }

    public HouseBids.Bid Bid(Player player, int listIndex, long bidOffer)
    {
        HouseBids houseBids = bids.Values.Where(b => b.GetListIndex() == listIndex).FirstOrDefault();
        if (!IsAllowedToBid(player, houseBids, bidOffer))
            return null; // bid too low or bidding not allowed
        HouseBids.Bid previousBid = houseBids.GetHighestBid();
        HouseBids.Bid bid = houseBids.DoBid(player, bidOffer);
        if (bid == null) // another bidder just bid more
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_LOWER());
            PacketSendUtility.SendPacket(player, new SM_RECEIVE_BIDS(0));
            return null;
        }
        if (AuctionEndTask.GetInstance().TryProlongAuction(bid.GetHouseObjectId()))
            HouseBidsDAO.AddBid(bid); // no need to save the bid if prolongation failed (the auction just ended)
        player.GetInventory().DecreaseKinah(bid.GetKinah());
        House bidHouse = HousingService.GetInstance().FindHouse(bid.GetHouseObjectId());
        if (previousBid != houseBids.GetInitialOffer() && previousBid.GetPlayerObjectId() != 0)
        {
            PlayerCommonData prevPcd = PlayerService.GetOrLoadPlayerCommonData(previousBid.GetPlayerObjectId());
            if (prevPcd.IsOnline())
            {
                PacketSendUtility.SendPacket(prevPcd.GetPlayer(), SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_CANCEL());
                PacketSendUtility.SendPacket(prevPcd.GetPlayer(), new SM_RECEIVE_BIDS(0));
            }
            MailFormatter.SendHouseAuctionMail(bidHouse, prevPcd, AuctionResult.FAILED_BID, bid.GetTime(), previousBid.GetKinah());
        }
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_SUCCESS(bidHouse.GetAddress().GetId()));
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_PRICE_CHANGE(bidOffer));
        PacketSendUtility.SendPacket(player, new SM_RECEIVE_BIDS(0));
        return bid;
    }

    private bool IsAllowedToBid(Player player, HouseBids houseBids, long bidOffer)
    {
        if (!HousingService.GetInstance().CanOwnHouse(player, true))
            return false;
        if (houseBids == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_FAIL());
            return false;
        }
        if (!IsAuctionOpen(houseBids.GetHouseObjectId()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_TIMEOUT());
            return false;
        }
        House bidHouse = HousingService.GetInstance().FindHouse(houseBids.GetHouseObjectId());
        if (player.GetObjectId() == bidHouse.GetOwnerId()) // client usually already checks this, but we want to make sure
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_MY_HOUSE());
            return false;
        }

        if (HousingService.GetInstance().FindInactiveHouse(player.GetObjectId()) != null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_GRACE_HOUSE());
            return false;
        }
        House playerHouse = player.GetActiveHouse();
        if (playerHouse != null && !playerHouse.IsFeePaid() && HousingConfig.ENABLE_HOUSE_PAY)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_OVERDUE());
            return false;
        }
        int minBidLevel = GetMinBidLevel(bidHouse);
        if (player.GetLevel() < minBidLevel)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_LOW_LEVEL(minBidLevel));
            return false;
        }
        HouseBids.Bid highestBid = houseBids.GetHighestBid();
        if (highestBid.GetPlayerObjectId() == player.GetObjectId())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_SUCC_BID_HOUSE());
            return false;
        }
        if (bids.Values.Any(b => b.IsHighestBidder(player)))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_OTHER_HOUSE());
            return false;
        }
        if (player.GetInventory().GetKinah() < bidOffer)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_NOT_ENOUGH_MONEY(bidOffer));
            return false;
        }
        long currentBid = highestBid.GetKinah();
        if (bidOffer - currentBid >= currentBid * HousingConfig.AUCTION_BID_STEP_LIMIT / 100f)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_BID_EXCESS_ACCOUNT());
            return false;
        }
        return true;
    }

    public void EndAuctions()
    {
        foreach (int houseObjectId in bids.Keys)
        {
            if (!AuctionEndTask.GetInstance().IsAuctionProlonged(houseObjectId))
                EndAuction(houseObjectId);
        }
        ImpoundAndAuctionOldPlayerHouses();
    }

    public bool EndAuction(int houseObjectId)
    {
        AuctionEndTask.GetInstance().OnAuctionEnd(houseObjectId);
        HouseBids bids;
        if (!HouseBidsDAO.DeleteHouseBids(houseObjectId) || !this.bids.TryRemove(houseObjectId, out bids))
            return false;
        House house = HousingService.GetInstance().FindHouse(houseObjectId);
        house.SetBids(null, false);
        int sellerId = house.GetOwnerId();
        PlayerCommonData sellerPcd = sellerId == 0 ? null : PlayerService.GetOrLoadPlayerCommonData(sellerId);
        HouseBids.Bid highestBid = bids.GetHighestBid();
        if (highestBid == bids.GetInitialOffer())
        {
            AuctionResult result = AuctionResult.FAILED_SALE;
            long time = bids.GetInitialOffer().GetTime(); // registration time
            long compensation = 0;

            House inactiveHouse = sellerId == 0 ? null : HousingService.GetInstance().FindInactiveHouse(sellerId);
            if (inactiveHouse != null && inactiveHouse.SecondsUntilGraceEnd() == 0)
            {
                HousingService.GetInstance().ChangeOwner(house, 0); // inactive house will also be activated automatically by this
                result = AuctionResult.GRACE_FAIL;
                time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                compensation = (long)(bids.GetInitialOffer().GetKinah() * HousingConfig.AUCTION_GRACE_END_REFUND_PERCENT);
            }
            else
            {
                house.GetController().UpdateSign();
            }
            if (sellerPcd != null)
            {
                if (sellerPcd.IsOnline())
                    PacketSendUtility.SendPacket(sellerPcd.GetPlayer(), SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_FAIL(house.GetAddress().GetId()));
                MailFormatter.SendHouseAuctionMail(house, sellerPcd, result, time, compensation);
            }
            if (LoggingConfig.LOG_HOUSE_AUCTION)
            {
                log.LogInformation("Address " + house.GetAddress().GetId() + " not sold for " + bids.GetInitialOffer().GetKinah() + " kinah (result: " + result
                    + "; return: " + compensation + " kinah)");
            }
        }
        else
        {
            PlayerCommonData buyerPcd = PlayerService.GetOrLoadPlayerCommonData(highestBid.GetPlayerObjectId());

            if (buyerPcd == null)
            {
                if (highestBid.GetPlayerObjectId() == 0)
                    log.LogInformation(house + " wasn't sold because the winning bidder deleted his character.");
                else
                    log.LogWarning(house + " could not be sold to player " + highestBid.GetPlayerObjectId() + " because the player couldn't be found");
                house.GetController().UpdateSign();
                return true;
            }
            if (buyerPcd.GetPlayerObjId() == sellerId)
            {
                log.LogWarning("Sold " + house + " to its own owner (" + sellerId + "), cancelling!");
                house.GetController().UpdateSign();
                return true;
            }

            House studio = HousingService.GetInstance().GetPlayerStudio(buyerPcd.GetPlayerObjId());
            if (studio != null)
                HousingService.GetInstance().ChangeOwner(studio, 0);
            HousingService.GetInstance().ChangeOwner(house, buyerPcd.GetPlayerObjId());

            AuctionResult result = AuctionResult.WIN_BID;
            long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (buyerPcd.IsOnline())
                PacketSendUtility.SendPacket(buyerPcd.GetPlayer(), SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_WIN(house.GetAddress().GetId()));
            if (house.IsInactive()) // buyer has another house
            {
                if (buyerPcd.IsOnline())
                    PacketSendUtility.SendPacket(buyerPcd.GetPlayer(), SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_GRACE_START(house.GetAddress().GetId()));
                result = AuctionResult.GRACE_START;
                MailFormatter.SendHouseAuctionMail(house, buyerPcd, result, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + house.SecondsUntilGraceEnd() * 1000, 0);
            }
            else
            {
                MailFormatter.SendHouseAuctionMail(house, buyerPcd, result, time, 0);
            }

            if (sellerPcd != null)
            {
                if (sellerPcd.IsOnline())
                    PacketSendUtility.SendPacket(sellerPcd.GetPlayer(), SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_SUCCESS(house.GetAddress().GetId()));
                House newHouse = HousingService.GetInstance().FindActiveHouse(sellerPcd.GetPlayerObjId());
                if (newHouse != null) // seller got his new house activated because the old one is sold
                {
                    if (sellerPcd.IsOnline())
                        PacketSendUtility.SendPacket(sellerPcd.GetPlayer(), SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_GRACE_SUCCESS(house.GetAddress().GetId()));
                    MailFormatter.SendHouseAuctionMail(newHouse, sellerPcd, AuctionResult.GRACE_SUCCESS, time, highestBid.CalculateSaleRewardKinah());
                }
                else
                    MailFormatter.SendHouseAuctionMail(house, sellerPcd, AuctionResult.SUCCESS_SALE, time, highestBid.CalculateSaleRewardKinah());
            }

            if (LoggingConfig.LOG_HOUSE_AUCTION)
            {
                string sellerInfo = sellerPcd == null ? "" : " by player " + sellerPcd.GetPlayerObjId();
                log.LogInformation("Address " + house.GetAddress().GetId() + " sold" + sellerInfo + " for " + highestBid.GetKinah() + " kinah (" + bids.GetBidCount()
                    + " bids; result: " + result + ") to player " + buyerPcd.GetPlayerObjId());
            }
        }
        return true;
    }

    private void ImpoundAndAuctionOldPlayerHouses()
    {
        foreach (House house in HousingService.GetInstance().GetCustomHouses())
        {
            if (house.IsInactive() && house.SecondsUntilGraceEnd() == 0)
            {
                House oldHouse = HousingService.GetInstance().FindActiveHouse(house.GetOwnerId());
                HousingService.GetInstance().ChangeOwner(oldHouse, 0);
                PlayerCommonData pcd = PlayerService.GetOrLoadPlayerCommonData(house.GetOwnerId());
                if (pcd.IsOnline())
                    PacketSendUtility.SendPacket(pcd.GetPlayer(),
                        SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_GRACE_FAIL(house.GetAddress().GetId(), oldHouse.GetAddress().GetId()));
                if (Auction(oldHouse, oldHouse.GetDefaultAuctionPrice()))
                    MailFormatter.SendHouseAuctionMail(house, pcd, AuctionResult.GRACE_FAIL, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        (long)(oldHouse.GetDefaultAuctionPrice() * HousingConfig.AUCTION_GRACE_END_REFUND_PERCENT));
            }
        }
    }

    private int GetMinBidLevel(House house)
    {
        switch (house.GetHouseType())
        {
            case HouseType.HOUSE:
                if (HousingConfig.HOUSE_MIN_BID_LEVEL > 0)
                    return HousingConfig.HOUSE_MIN_BID_LEVEL;
                break;
            case HouseType.MANSION:
                if (HousingConfig.MANSION_MIN_BID_LEVEL > 0)
                    return HousingConfig.MANSION_MIN_BID_LEVEL;
                break;
            case HouseType.ESTATE:
                if (HousingConfig.ESTATE_MIN_BID_LEVEL > 0)
                    return HousingConfig.ESTATE_MIN_BID_LEVEL;
                break;
            case HouseType.PALACE:
                if (HousingConfig.PALACE_MIN_BID_LEVEL > 0)
                    return HousingConfig.PALACE_MIN_BID_LEVEL;
                break;
        }
        return house.GetLand().GetSaleOptions().GetMinLevel();
    }

    public void DisableBids(int playerObjId)
    {
        List<HouseBids.Bid> deletedBids = new List<HouseBids.Bid>();
        foreach (HouseBids b in bids.Values)
            deletedBids.AddRange(b.DeleteOrDisableBids(playerObjId));
        HouseBidsDAO.DeleteOrDisableBids(playerObjId, deletedBids);
    }

    public HouseBids.Bid FindLastBid(Player player)
    {
        return bids.Values.Select(b => b.GetLatestBid(player)).Where(x => x != null).MaxBy(bid => bid.GetTime());
    }

    public HouseBids FindBidsForRegisteredHouse(Player player)
    {
        foreach (House house in player.GetHouses())
        {
            if (house.GetBids() != null)
                return house.GetBids();
        }
        return null;
    }

    public bool CancelAuction(House house)
    {
        if (!this.bids.TryRemove(house.GetObjectId(), out HouseBids bids))
            return false;

        HouseBidsDAO.DeleteHouseBids(house.GetObjectId());
        house.SetBids(null, true);
        house.GetController().UpdateSign();
        house.GetController().UpdateAppearance();

        if (house.GetOwnerId() != 0)
        {
            PlayerCommonData pcd = PlayerService.GetOrLoadPlayerCommonData(house.GetOwnerId());
            if (pcd.IsOnline())
            {
                PacketSendUtility.SendPacket(pcd.GetPlayer(), SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_FAIL(house.GetAddress().GetId()));
                PacketSendUtility.SendPacket(pcd.GetPlayer(), new SM_RECEIVE_BIDS(1));
            }
            MailFormatter.SendHouseAuctionMail(house, pcd, AuctionResult.CANCELED_BID, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0);
        }
        HouseBids.Bid highestBid = bids.GetHighestBid();
        if (highestBid != bids.GetInitialOffer() && highestBid.GetPlayerObjectId() != 0)
        {
            // return bid price only to the last bidder (previous bidders already get their money back when another player bids more)
            PlayerCommonData pcd = PlayerService.GetOrLoadPlayerCommonData(highestBid.GetPlayerObjectId());
            MailFormatter.SendHouseAuctionMail(house, pcd, AuctionResult.CANCELED_BID, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), highestBid.GetKinah());
        }

        return true;
    }

    /// <summary>Notify once about new auction results, based on system mail checks and login time.</summary>
    public void OnPlayerLogin(Player player)
    {
        List<Letter> letters = player.GetMailbox().GetNewSystemLetters("$$HS_AUCTION_MAIL");
        bool needsRefresh = false;

        foreach (Letter letter in letters)
        {
            string[] titleParts = letter.GetTitle().Split(',');
            string[] bodyParts = letter.GetMessage().Split(',');
            AuctionResult result = AuctionResultExtensions.GetResultFromId(int.Parse(titleParts[0])).Value;
            if (result == AuctionResult.FAILED_BID)
            {
                needsRefresh = true;
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_CANCEL());
            }
            else if (result == AuctionResult.WIN_BID || result == AuctionResult.GRACE_START)
            {
                needsRefresh = true;
                int address = int.Parse(bodyParts[1]);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_BID_WIN(address));
            }
            else if (result == AuctionResult.FAILED_SALE)
            {
                needsRefresh = true;
                int address = int.Parse(bodyParts[1]);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_FAIL(address));
            }
            else if (result == AuctionResult.SUCCESS_SALE || result == AuctionResult.GRACE_SUCCESS)
            {
                needsRefresh = true;
                int address = int.Parse(bodyParts[1]);
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_AUCTION_SUCCESS(address));
            }
        }

        if (needsRefresh)
            PacketSendUtility.SendPacket(player, new SM_RECEIVE_BIDS(0));
    }
}
