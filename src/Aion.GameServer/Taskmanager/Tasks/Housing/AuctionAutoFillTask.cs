using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Services;

namespace Aion.GameServer.Taskmanager.Tasks.Housing;

/// <summary>
/// Handles registering unoccupied houses automatically for auction (Java parity: taskmanager/tasks/housing/AuctionAutoFillTask,
/// Neon) : AbstractCronTask singleton. Set→HashSet; Collections.shuffle→in-place Fisher-Yates; groupingBy(getHouseType,
/// counting())→GroupBy.ToDictionary(g=>g.Key,g=>(long)g.Count()); int[]{0} mutable-closure→plain captured int;
/// Map.forEach→foreach; removeIf→RemoveAll; iterator.remove→indexed RemoveAt. HousingService/HousingBidService red-tolerated.
/// </summary>
public class AuctionAutoFillTask : AbstractCronTask
{
    private static readonly AuctionAutoFillTask instance = new();
    private static readonly Random rnd = new();

    public static AuctionAutoFillTask GetInstance()
    {
        return instance;
    }

    private AuctionAutoFillTask() : base(HousingConfig.AUCTION_AUTO_FILL_TIME)
    {
    }

    protected override void ExecuteTask()
    {
        if (HousingConfig.ENABLE_HOUSE_AUCTIONS)
        {
            AutoFillAuction(Race.ELYOS);
            AutoFillAuction(Race.ASMODIANS);
        }
    }

    private void AutoFillAuction(Race race)
    {
        HashSet<House> auctionedHouses = FindAuctionedHouses(race);
        List<House> auctionableHouses = FindAuctionableHouses(race, auctionedHouses);
        Shuffle(auctionableHouses);
        Dictionary<HouseType, long> auctionedHouseCounts = auctionedHouses.GroupBy(h => h.GetHouseType()).ToDictionary(g => g.Key, g => (long)g.Count());
        int added = 0;
        foreach (KeyValuePair<HouseType, int> kv in HousingConfig.AUCTION_AUTO_FILL_LIMITS)
        {
            HouseType houseType = kv.Key;
            int limit = kv.Value;
            for (long auctioned = auctionedHouseCounts.GetValueOrDefault(houseType, 0L); auctioned < limit; auctioned++)
            {
                House house = FindAndRemoveHouse(auctionableHouses, houseType);
                if (house == null || !HousingBidService.GetInstance().Auction(house, house.GetDefaultAuctionPrice()))
                    break;
                added++;
            }
        }
        log.LogInformation("[" + race + "] Added " + added + " new houses automatically to auction.");
    }

    /// <summary>Java parity: Collections.shuffle (in-place Fisher-Yates).</summary>
    private static void Shuffle(List<House> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private HashSet<House> FindAuctionedHouses(Race race)
    {
        return HousingBidService.GetInstance().GetBidInfo(race).Select(ToHouse).ToHashSet();
    }

    private House ToHouse(HouseBids houseBids)
    {
        return HousingService.GetInstance().FindHouse(houseBids.GetHouseObjectId());
    }

    private List<House> FindAuctionableHouses(Race race, HashSet<House> auctionedHouses)
    {
        List<House> houses = HousingService.GetInstance().GetCustomHouses();
        houses.RemoveAll(house => house.GetOwnerId() != 0 || auctionedHouses.Contains(house) || !house.MatchesLandRace(race));
        return houses;
    }

    private House FindAndRemoveHouse(List<House> houses, HouseType houseType)
    {
        for (int i = 0; i < houses.Count; i++)
        {
            House house = houses[i];
            if (house.GetHouseType() == houseType)
            {
                houses.RemoveAt(i);
                return house;
            }
        }
        return null;
    }
}
