using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/HousingService (Rolandas, Neon). House/studio ownership + spawning. ConcurrentHashMap(copy-ctor from DAO map)→ConcurrentDictionary; IntStream.of(int[]).boxed().toSet→new HashSet<int>(arr); Stream.concat→Concat; mapToInt.distinct→Select.Distinct; sorted(comparing)→OrderBy; Map.get→GetValueOrDefault, values().remove(v)→find-key+TryRemove; synchronized(house)→lock(house); new Timestamp(currentTimeMillis())→DateTimeOffset.FromUnixTimeMilliseconds(UtcNow…); getTime→ToUnixTimeMilliseconds; PersistentState→IPersistable.PersistentState; Collections.singletonList→List initializer. House/templates/DAO/SM_* red-tolerated.</summary>
public class HousingService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(HousingService));
    // Contains all houses by their addresses
    private readonly ConcurrentDictionary<int, House> customHouses;
    private readonly ConcurrentDictionary<int, House> studios;

    private static class SingletonHolder
    {
        internal static readonly HousingService instance = new HousingService();
    }

    public static HousingService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private HousingService()
    {
        customHouses = new ConcurrentDictionary<int, House>(HousesDAO.LoadHouses(DataManager.HOUSE_DATA.GetLands(), false));
        studios = new ConcurrentDictionary<int, House>(HousesDAO.LoadHouses(DataManager.HOUSE_DATA.GetLands(), true));
        UpdateInactiveStateForAllHouses();
        RevokeOwnershipOfDeletedPlayers();
        log.LogInformation("Loaded " + customHouses.Count + " houses and " + studios.Count + " studios");
    }

    private void RevokeOwnershipOfDeletedPlayers()
    {
        HashSet<int> playerIds = new HashSet<int>(PlayerDAO.GetUsedIDs());
        foreach (House house in customHouses.Values.Concat(studios.Values))
        {
            // houses table has no player_id foreign key because houses need to stay in DB even on player deletion (to keep bidding possible for example)
            if (house.GetOwnerId() > 0 && !playerIds.Contains(house.GetOwnerId()))
            {
                log.LogWarning("Player with ID " + house.GetOwnerId() + " got deleted from DB, revoking house ownership for house " + house.GetAddress().GetId());
                ChangeOwner(house, 0);
            }
        }
    }

    private void UpdateInactiveStateForAllHouses()
    {
        foreach (int id in customHouses.Values.Select(h => h.GetOwnerId()).Distinct())
            UpdateInactiveStateForPlayerHouses(id);
    }

    /// <summary>Only for server start. Doesn't send packets or reloads house registries because it should only be run once on init.</summary>
    private void UpdateInactiveStateForPlayerHouses(int playerObjId)
    {
        IEnumerable<House> houses = customHouses.Values.Where(house => house.GetOwnerId() == playerObjId);
        if (playerObjId == 0) // not occupied houses
        {
            foreach (House house in houses)
                house.SetInactive(false);
        }
        else
        {
            List<House> housesSortedByAcquireDate = houses.OrderBy(house => house.GetAcquiredTime()).ToList();
            for (int i = 0; i < housesSortedByAcquireDate.Count; i++)
                housesSortedByAcquireDate[i].SetInactive(i != 0); // first house (oldest) should be active, rest inactive
        }
    }

    public void ChangeOwner(House house, int newOwnerId)
    {
        int oldOwnerId = house.GetOwnerId();
        if (oldOwnerId == newOwnerId)
            return;

        lock (house)
        {
            bool newOwnerHasAnotherHouse = newOwnerId != 0 && customHouses.Values.Any(h => h.GetOwnerId() == newOwnerId);
            house.ResetRegistry();
            house.GetPlayerScripts().RemoveAll();
            house.SetOwnerId(newOwnerId);
            if (newOwnerId == 0 && RemoveStudio(house))
            {
                HousesDAO.DeleteHouse(oldOwnerId);
                NotifyAboutOwnerChange(oldOwnerId, house.GetAddress().GetId(), false);
                return;
            }
            house.SetInactive(newOwnerHasAnotherHouse);
            house.ResetDoorState();
            house.SetShowOwnerName(true);
            house.SetSignNotice(null);
            house.SetAcquiredTime(newOwnerId == 0 ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            house.SetNextPay(null);

            Building defaultBuilding = house.GetLand().GetDefaultBuilding();
            if (defaultBuilding != house.GetBuilding())
                SwitchHouseBuilding(house, defaultBuilding.GetId());
            else // in else clause because building switch also saves the house
                house.Save();
        }
        House newHouseOfOldOwner = FindInactiveHouse(oldOwnerId); // other house of seller that should get activated
        if (newHouseOfOldOwner != null && newHouseOfOldOwner.GetPosition() != null && newHouseOfOldOwner.IsSpawned())
        {
            newHouseOfOldOwner.SetInactive(false);
            newHouseOfOldOwner.ReloadHouseRegistry();
            newHouseOfOldOwner.GetController().UpdateSign();
            newHouseOfOldOwner.GetController().UpdateAppearance();
        }
        NotifyAboutOwnerChange(oldOwnerId, house.GetAddress().GetId(), false);
        NotifyAboutOwnerChange(newOwnerId, house.GetAddress().GetId(), true);
        if (house.GetPosition() != null && house.IsSpawned())
        {
            house.GetController().UpdateHouseSpawns();
            house.GetController().KickVisitors(null, true, true);
        }
    }

    private void NotifyAboutOwnerChange(int ownerId, int addressId, bool isNewOwner)
    {
        if (ownerId == 0)
            return;
        Player player = World.GetInstance().GetPlayer(ownerId);
        if (player != null)
        {
            player.ResetHouses();
            if (!isNewOwner)
                PacketSendUtility.SendPacket(player, new SM_HOUSE_ACQUIRE(player.GetObjectId(), addressId, false));
            PacketSendUtility.SendPacket(player, new SM_HOUSE_OWNER_INFO(player));
            if (isNewOwner)
                PacketSendUtility.SendPacket(player, new SM_HOUSE_ACQUIRE(player.GetObjectId(), addressId, true));
        }
    }

    public void SpawnHouses(WorldMapInstance instance, int registeredId)
    {
        if (registeredId > 0)
        {
            SpawnStudio(instance.GetMapId(), instance.GetInstanceId(), registeredId);
            return;
        }
        int spawnedCounter = 0;
        foreach (HouseAddress address in DataManager.HOUSE_DATA.GetAddresses(instance.GetMapId()))
        {
            if (address.GetLand().GetDefaultBuilding().GetType_() == BuildingType.PERSONAL_INS)
                continue; // ignore studios

            House customHouse = customHouses.GetValueOrDefault(address.GetId());
            if (customHouse == null)
            {
                customHouse = new House(address, instance.GetInstanceId());
                // house without owner when acquired will be inserted to DB
                customHouse.SetPersistentState(IPersistable.PersistentState.NEW);
                customHouses[address.GetId()] = customHouse;
            }
            WorldPosition position = World.GetInstance().CreatePosition(address.GetMapId(), address.GetX(), address.GetY(), address.GetZ(), (byte)0,
                instance.GetInstanceId());
            customHouse.SetPosition(position);
            SpawnEngine.BringIntoWorld(customHouse);
            spawnedCounter++;
        }
        if (spawnedCounter > 0)
        {
            log.LogInformation("Spawned " + spawnedCounter + " houses in " + instance);
        }
    }

    private void SpawnStudio(int worldId, int instanceId, int registeredId)
    {
        House studio = GetPlayerStudio(registeredId);
        if (studio == null || studio.GetAddress().GetMapId() != worldId)
            return;
        if (studio.GetPosition() == null || studio.GetInstanceId() != instanceId)
        {
            HouseAddress addr = studio.GetAddress();
            studio.SetPosition(World.GetInstance().CreatePosition(addr.GetMapId(), addr.GetX(), addr.GetY(), addr.GetZ(), (byte)0, instanceId));
            SpawnEngine.BringIntoWorld(studio);
        }
    }

    public List<House> FindPlayerHouses(int playerObjId)
    {
        if (studios.ContainsKey(playerObjId))
        {
            return new List<House> { studios.GetValueOrDefault(playerObjId) };
        }
        List<House> houses = new List<House>();
        foreach (House house in customHouses.Values)
        {
            if (house.GetOwnerId() == playerObjId)
                houses.Add(house);
        }
        return houses;
    }

    /// <summary>The players studio or current active house.</summary>
    public House FindActiveHouse(int playerObjId)
    {
        if (studios.ContainsKey(playerObjId))
            return studios.GetValueOrDefault(playerObjId);
        foreach (House house in customHouses.Values)
        {
            if (house.GetOwnerId() == playerObjId && !house.IsInactive())
                return house;
        }
        return null;
    }

    /// <summary>The players current inactive house.</summary>
    public House FindInactiveHouse(int playerObjId)
    {
        foreach (House house in customHouses.Values)
        {
            if (house.GetOwnerId() == playerObjId && house.IsInactive())
                return house;
        }
        return null;
    }

    public House GetHouseByAddress(int address)
    {
        return customHouses.GetValueOrDefault(address);
    }

    public House GetPlayerStudio(int playerId)
    {
        return studios.GetValueOrDefault(playerId);
    }

    public bool RemoveStudio(House studio)
    {
        // Java parity: studios.values().remove(studio) — remove first entry whose value equals studio.
        foreach (KeyValuePair<int, House> kv in studios)
        {
            if (kv.Value == studio)
                return studios.TryRemove(kv.Key, out _);
        }
        return false;
    }

    public void RegisterPlayerStudio(Player player)
    {
        CreateStudio(player, false);
    }

    public void RecreatePlayerStudio(Player player)
    {
        CreateStudio(player, true);
    }

    private void CreateStudio(Player player, bool chargeFee)
    {
        if (player.GetHouses().Count != 0)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_INS_CANT_OWN_MORE_HOUSE());
            return;
        }
        HouseAddress address = DataManager.HOUSE_DATA.GetStudioAddress(player.GetRace());
        if (chargeFee && !player.GetInventory().TryDecreaseKinah(address.GetLand().GetSaleOptions().GetGoldPrice()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_NOT_ENOUGH_MONEY());
            return;
        }

        House studio = new House(address, 0);
        studios[player.GetObjectId()] = studio;
        studio.SetPersistentState(IPersistable.PersistentState.NEW);
        ChangeOwner(studio, player.GetObjectId());

        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_INS_OWN_SUCCESS());
    }

    public bool CanOwnHouse(Player player, bool notify)
    {
        int questId = player.GetRace() == Race.ELYOS ? 18802 : 28802;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.COMPLETE)
        {
            if (notify)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANT_OWN_NOT_COMPLETE_QUEST(questId));
            return false;
        }
        return true;
    }

    public void SwitchHouseBuilding(House currentHouse, int newBuildingId)
    {
        currentHouse.SetBuilding(DataManager.HOUSE_BUILDING_DATA.GetBuilding(newBuildingId));
        currentHouse.Save();
        currentHouse.ReloadHouseRegistry(); // load new defaults
        currentHouse.GetController().SpawnObjects();
    }

    public List<House> GetCustomHouses()
    {
        return new List<House>(customHouses.Values);
    }

    public House FindHouse(int objId)
    {
        foreach (House house in customHouses.Values)
        {
            if (house.GetObjectId() == objId)
                return house;
        }
        return null;
    }

    public House FindStudio(int objId)
    {
        foreach (House studio in studios.Values)
        {
            if (studio.GetObjectId() == objId)
                return studio;
        }
        return null;
    }

    public House FindHouseOrStudio(int objId)
    {
        House studio = FindStudio(objId);
        return studio == null ? FindHouse(objId) : studio;
    }

    public void OnPlayerDeleted(int playerObjId)
    {
        HousingBidService.GetInstance().DisableBids(playerObjId);
        foreach (House house in FindPlayerHouses(playerObjId))
            ChangeOwner(house, 0);
    }

    public void OnPlayerLogin(Player player)
    {
        House activeHouse = player.GetActiveHouse();
        if (activeHouse != null)
        {
            if (HousingConfig.ENABLE_HOUSE_PAY && activeHouse.GetNextPay() != null && activeHouse.GetNextPay().Value.ToUnixTimeMilliseconds() <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OVERDUE());
        }
        else
        {
            foreach (Letter letter in player.GetMailbox().GetNewSystemLetters("$$HS_OVERDUE_"))
            {
                if (letter.GetSenderName().EndsWith("FINAL") || letter.GetSenderName().EndsWith("3RD"))
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_SEQUESTRATE());
                    break;
                }
            }
        }
        PacketSendUtility.SendPacket(player, new SM_HOUSE_OWNER_INFO(player));
    }
}
