using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Services;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Auction (Rolandas, Luzien, Neon).</summary>
public class Auction : AdminCommand
{
    public Auction()
        : base("auction", "Adds or removes houses to/from auction.")
    {
        SetSyntaxInfo(
            "<address> [starting price] - Auctions the given house.",
            "<zone> <house type> <count> [starting price] - Auctions free houses of given type that are in the specified zone.",
            "asmo|ely <house type> <count> [starting price] - Auctions free asmodian or elysean houses of given type.",
            "end <address|zone> - Ends the auction for given house(s), transferring ownership to the highest bidder.",
            "cancel <address|zone> - Cancels the auction for given house(s).",
            "Zone: Zone name from zones xml files",
            "House type: house, mansion, estate, palace",
            "If no starting price is given, default will be taken from templates."
        );
    }

    protected override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        bool isCancel = "cancel".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase);
        if ("end".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase) || isCancel)
        {
            if (paramsArr.Length < 2)
            {
                SendInfo(admin);
                return;
            }

            bool isHouseAddress = Regex.IsMatch(paramsArr[1], "^\\d+$");
            List<House> houses;
            if (isHouseAddress)
            {
                House house = HousingService.GetInstance().GetHouseByAddress(int.Parse(paramsArr[1]));
                if (house == null)
                {
                    SendInfo(admin, "Invalid house address.");
                    return;
                }
                houses = new List<House> { house };
            }
            else
            {
                houses = FindHousesInZone(admin, paramsArr[1], Predicates.AlwaysTrue<House>());
                if (houses == null)
                    return;
            }

            int removedHouses = 0;
            foreach (House house in houses)
            {
                if (!isCancel && HousingBidService.GetInstance().EndAuction(house.GetObjectId())
                    || isCancel && HousingBidService.GetInstance().CancelAuction(house))
                {
                    SendInfo(admin,
                        (isCancel ? "Canceled" : "Ended") + " auction for " + house.GetHouseType().ToString().ToLower() + " " + house.GetAddress().GetId());
                    removedHouses++;
                }
            }
            if (removedHouses == 0)
                SendInfo(admin, isHouseAddress ? "The house is not for sale." : "No houses for sale in this zone.");
        }
        else if (Regex.IsMatch(paramsArr[0], "^\\d+$"))
        {
            int address = int.Parse(paramsArr[0]);
            House house = HousingService.GetInstance().GetHouseByAddress(address);
            if (house == null)
            {
                SendInfo(admin, "Invalid address.");
                return;
            }
            if (house.GetBids() != null)
            {
                SendInfo(admin, "Address " + address + " is already in auction.");
                return;
            }
            long price = paramsArr.Length < 2 ? house.GetDefaultAuctionPrice() : long.Parse(paramsArr[1]);
            if (price <= 0)
            {
                SendInfo(admin, "Starting price must be positive.");
                return;
            }
            HousingBidService.GetInstance().Auction(house, price);
            SendInfo(admin, "Address " + address + " was auctioned successfully.");
        }
        else if ("add".Equals(paramsArr[0]))
        {
            if (paramsArr.Length < 4 || paramsArr.Length > 5)
            {
                SendInfo(admin);
                return;
            }

            HouseType houseType = Enum.Parse<HouseType>(paramsArr[2].ToUpper());
            int maxCount = int.Parse(paramsArr[3]);
            if (maxCount <= 0)
            {
                SendInfo(admin, "Count must be positive.");
                return;
            }

            Predicate<House> filter = house => house.GetHouseType() == houseType && house.GetBids() == null && house.GetOwnerId() == 0;
            List<House> houses;
            if ("asmodians".StartsWith(paramsArr[1].ToLower()))
            {
                Predicate<House> baseFilter = filter;
                filter = house => baseFilter(house) && house.MatchesLandRace(Race.ASMODIANS);
                houses = HousingService.GetInstance().GetCustomHouses().Where(h => filter(h)).ToList();
            }
            else if ("elyos".StartsWith(paramsArr[1].ToLower()))
            {
                Predicate<House> baseFilter = filter;
                filter = house => baseFilter(house) && house.MatchesLandRace(Race.ELYOS);
                houses = HousingService.GetInstance().GetCustomHouses().Where(h => filter(h)).ToList();
            }
            else
            {
                houses = FindHousesInZone(admin, paramsArr[1], filter);
            }
            if (houses == null)
                return;
            if (houses.Count == 0)
            {
                SendInfo(admin, "No auctionable " + houseType.ToString().ToLower() + "s found.");
                return;
            }
            long price = paramsArr.Length < 5 ? houses[0].GetDefaultAuctionPrice() : long.Parse(paramsArr[4]);
            if (price <= 0)
            {
                SendInfo(admin, "Starting price must be positive.");
                return;
            }

            int counter = 0;
            Shuffle(houses);
            foreach (House house in houses)
            {
                if (HousingBidService.GetInstance().Auction(house, price) && ++counter > maxCount)
                    break;
            }

            SendInfo(admin, "Auctioned " + counter + " " + houseType.ToString().ToLower() + "s for a starting price of " + price + " Kinah.");
        }
        else
        {
            SendInfo(admin);
        }
    }

    private List<House> FindHousesInZone(Player admin, string zoneName, Predicate<House> filter)
    {
        ZoneName zone = ZoneName.Get(zoneName);
        if (zone == ZoneName.NONE)
        {
            SendInfo(admin, "Invalid zone name");
            return null;
        }
        List<House> housesToRemove = new List<House>();
        foreach (House house in HousingService.GetInstance().GetCustomHouses())
        {
            if (!filter(house))
                continue;
            if (house.GetPosition().GetMapRegion().IsInsideZone(zone, house.GetX(), house.GetY(), house.GetZ()))
                housesToRemove.Add(house);
        }
        return housesToRemove;
    }

    private static readonly Random Rng = new Random();

    // Java parity: Collections.shuffle(List)
    private static void Shuffle(List<House> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
