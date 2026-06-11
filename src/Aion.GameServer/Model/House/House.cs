using System;
using System.Collections.Generic;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Taskmanager.Tasks.Housing;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Idfactory;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Model.House;

/// <summary>
/// Java parity: model/house/House (Rolandas). extends VisibleObject implements Persistable→IPersistable.
/// getName()→override Name property; java.sql.Timestamp→DateTimeOffset?; EnumMap→Dictionary; synchronized→lock;
/// signed byte→sbyte; @SuppressWarnings(fallthrough) setPersistentState→explicit `goto default`; apache
/// DateUtils.round(DAY_OF_MONTH)→RoundToDay helper (>=12:00 rounds up); TimeUnit.DAYS.toMillis(14)→14L*86400000;
/// currentTimeMillis/getTime→UtcNow/ToUnixTimeMilliseconds. Most housing deps (HouseController/HouseAddress/Building/
/// HousingLand/Sale/HouseType/PlayerScripts/DAOs/GeoService/AuctionEndTask) red-tolerated.
/// </summary>
public class House : VisibleObject, IPersistable
{
    private readonly HouseAddress address;
    private Building building;
    private int ownerId;
    private DateTimeOffset? acquiredTime;
    private HouseDoorState doorState;
    private bool showOwnerName = true;
    private bool inactive;
    private DateTimeOffset? nextPay;
    private HouseBids bids;
    private readonly Dictionary<SpawnType, Npc> spawns = new();
    private HouseRegistry houseRegistry;
    private PlayerScripts playerScripts;
    private IPersistable.PersistentState persistentState;
    private string signNotice;

    public House(HouseAddress address, int instanceId)
        : this(IDFactory.GetInstance().NextId(), address.GetLand().GetDefaultBuilding(), address, instanceId)
    {
    }

    public House(int objectId, Building building, HouseAddress address, int instanceId)
        : base(objectId, new HouseController(), null, null, null, false)
    {
        GetController().SetOwner(this);
        this.address = address;
        this.building = building;
        SetKnownlist(new PlayerAwareKnownList(this));
        ResetDoorState();
        SetPersistentState(IPersistable.PersistentState.UPDATED);
    }

    public new HouseController GetController()
    {
        return (HouseController)base.GetController();
    }

    public override string Name => "HOUSE_" + address.GetId();

    public HouseAddress GetAddress()
    {
        return address;
    }

    public HousingLand GetLand()
    {
        return address.GetLand();
    }

    public override WorldType GetWorldType()
    {
        return World.GetInstance().GetWorldMap(GetAddress().GetMapId()).GetWorldType();
    }

    public Building GetBuilding()
    {
        return building;
    }

    public void SetBuilding(Building building)
    {
        this.building = building;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public override float GetVisibleDistance()
    {
        return HousingConfig.VISIBILITY_DISTANCE;
    }

    public int GetOwnerId()
    {
        return ownerId;
    }

    public void SetOwnerId(int ownerId)
    {
        this.ownerId = ownerId;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public string GetOwnerName()
    {
        if (ownerId == 0)
            return null;
        Npc butler = GetButler();
        return butler == null ? PlayerService.GetPlayerName(ownerId) : butler.GetMasterName();
    }

    public DateTimeOffset? GetAcquiredTime()
    {
        return acquiredTime;
    }

    public void SetAcquiredTime(DateTimeOffset? acquiredTime)
    {
        this.acquiredTime = acquiredTime;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public int GetPermissionsForDB()
    {
        int permissions = showOwnerName ? 1 : 0;
        permissions |= doorState.GetId() << 8;
        return permissions;
    }

    public void SetPermissionsFromDB(int permissions)
    {
        showOwnerName = (permissions & 0xFF) == 1;
        HouseDoorState? d = HouseDoorStates.Get((sbyte)(permissions >> 8));
        if (d == null)
            ResetDoorState();
        else
            doorState = d.Value;
    }

    public HouseDoorState GetDoorState()
    {
        return doorState;
    }

    public bool ResetDoorState()
    {
        return SetDoorState(inactive || ownerId == 0 && bids == null ? HouseDoorState.CLOSED : HouseDoorState.OPEN);
    }

    public bool SetDoorState(HouseDoorState doorState)
    {
        if (this.doorState == doorState)
            return false;
        this.doorState = doorState;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        if (GetPosition() != null && IsSpawned())
            GeoService.GetInstance().SetHouseDoorState(address.GetMapId(), GetInstanceId(), address.GetId(), GetDoorState());
        return true;
    }

    /// <returns>True if the owner name should be displayed in the house sign tooltip</returns>
    public bool IsShowOwnerName()
    {
        return showOwnerName;
    }

    public void SetShowOwnerName(bool showOwnerName)
    {
        this.showOwnerName = showOwnerName;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <returns>True if the owner of this house has another (newly acquired) house</returns>
    public bool IsInactive()
    {
        return inactive;
    }

    public void SetInactive(bool inactive)
    {
        this.inactive = inactive;
    }

    public bool IsFeePaid()
    {
        return nextPay == null || nextPay.Value.ToUnixTimeMilliseconds() >= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public DateTimeOffset? GetNextPay()
    {
        return nextPay;
    }

    public void SetNextPay(DateTimeOffset? nextPay)
    {
        DateTimeOffset? result = null;
        if (nextPay != null) // round to midnight
        {
            result = RoundToDay(nextPay.Value);
        }
        this.nextPay = result;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>Apache DateUtils.round(date, Calendar.DAY_OF_MONTH) parity: rounds to nearest midnight (&gt;=12:00 rounds up).</summary>
    private static DateTimeOffset RoundToDay(DateTimeOffset date)
    {
        DateTimeOffset dayStart = new(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset);
        return date.TimeOfDay.TotalHours >= 12 ? dayStart.AddDays(1) : dayStart;
    }

    public HouseBids GetBids()
    {
        return bids;
    }

    public void SetBids(HouseBids bids, bool resetDoorStateOfUnoccupiedHouse)
    {
        this.bids = bids;
        if (resetDoorStateOfUnoccupiedHouse && ownerId == 0 && ResetDoorState())
            Save();
    }

    public Npc GetButler()
    {
        return GetSpawn(SpawnType.MANAGER);
    }

    public Npc GetRelationshipCrystal()
    {
        return GetSpawn(SpawnType.TELEPORT);
    }

    public Npc GetCurrentSign()
    {
        return GetSpawn(SpawnType.SIGN);
    }

    private Npc GetSpawn(SpawnType type)
    {
        lock (spawns)
        {
            return spawns.GetValueOrDefault(type);
        }
    }

    public void UpdateSpawn(SpawnType type, Npc npc)
    {
        Npc oldSpawn;
        lock (spawns)
        {
            spawns.TryGetValue(type, out oldSpawn);
            spawns[type] = npc;
        }
        if (oldSpawn != null)
            oldSpawn.GetController().Delete();
    }

    /// <summary>
    /// Do not use directly !!! It's for instance destroy of studios only. Studios get reused, Npcs are despawned by instance destroy
    /// </summary>
    public void ClearSpawns()
    {
        lock (spawns)
        {
            spawns.Clear();
        }
    }

    public HouseRegistry GetRegistry()
    {
        if (houseRegistry == null)
            ReloadHouseRegistry();
        return houseRegistry;
    }

    public void ResetRegistry()
    {
        lock (this)
        {
            GetRegistry().Reset();
            houseRegistry = null;
        }
    }

    public void ReloadHouseRegistry()
    {
        lock (this)
        {
            houseRegistry = new HouseRegistry(this);
            if (ownerId != 0 && !IsInactive())
                PlayerRegisteredItemsDAO.LoadRegistry(houseRegistry);
        }
    }

    public PlayerScripts GetPlayerScripts()
    {
        if (playerScripts == null)
            ReloadPlayerScripts();
        return playerScripts;
    }

    public void ReloadPlayerScripts()
    {
        lock (this)
        {
            playerScripts = HouseScriptsDAO.GetPlayerScripts(GetObjectId());
        }
    }

    public HouseType GetHouseType()
    {
        return GetBuilding().GetSize();
    }

    public void Save()
    {
        lock (this)
        {
            HousesDAO.StoreHouse(this);
            if (houseRegistry != null)
                houseRegistry.Save();
        }
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        switch (persistentState)
        {
            case IPersistable.PersistentState.DELETED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    this.persistentState = IPersistable.PersistentState.NOACTION;
                else
                    this.persistentState = IPersistable.PersistentState.DELETED;
                break;
            case IPersistable.PersistentState.UPDATE_REQUIRED:
                if (this.persistentState == IPersistable.PersistentState.NEW)
                    break;
                goto default;
            default:
                this.persistentState = persistentState;
                break;
        }
    }

    public sbyte GetHouseOwnerStates()
    {
        // logic and enum names surely aren't right, but it's what allegedly got sent on retail some time in the past (?):
        // 1|2 or 2 without owner, 1|2 or 1|4 with owner - we can make it right once we know what these values control
        if (IsInactive()) // only houses with owner can be inactive
            return HouseOwnerState.BIDDING_ALLOWED.GetId();
        else if (ownerId == 0 && GetBids() == null)
            return HouseOwnerState.SINGLE_HOUSE.GetId();
        else
            return (sbyte)(HouseOwnerState.HAS_OWNER.GetId() | HouseOwnerState.BIDDING_ALLOWED.GetId());
    }

    public string GetSignNotice()
    {
        return signNotice;
    }

    public void SetSignNotice(string notice)
    {
        signNotice = notice;
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public bool CanEnter(Player player)
    {
        if ((GetOwnerId() != player.GetObjectId() || IsInactive()) && !player.HasAccess(AdminConfig.HOUSE_ENTER_ALL))
        {
            switch (GetDoorState())
            {
                case HouseDoorState.CLOSED:
                    return false;
                case HouseDoorState.CLOSED_EXCEPT_FRIENDS:
                    if (player.GetFriendList().GetFriend(GetOwnerId()) == null && (player.GetLegion() == null || !player.GetLegion().IsMember(GetOwnerId())))
                        return false;
                    break;
            }
        }
        return true;
    }

    public long GetDefaultAuctionPrice()
    {
        Sale saleOptions = GetLand().GetSaleOptions();
        switch (GetHouseType())
        {
            case HouseType.HOUSE:
                if (HousingConfig.HOUSE_MIN_BID > 0)
                    return HousingConfig.HOUSE_MIN_BID;
                break;
            case HouseType.MANSION:
                if (HousingConfig.MANSION_MIN_BID > 0)
                    return HousingConfig.MANSION_MIN_BID;
                break;
            case HouseType.ESTATE:
                if (HousingConfig.ESTATE_MIN_BID > 0)
                    return HousingConfig.ESTATE_MIN_BID;
                break;
            case HouseType.PALACE:
                if (HousingConfig.PALACE_MIN_BID > 0)
                    return HousingConfig.PALACE_MIN_BID;
                break;
        }
        return saleOptions.GetGoldPrice();
    }

    /// <returns>Calculated heading for a player inside looking towards the wall where butler, relationship crystal and the door are located.</returns>
    public sbyte GetTeleportHeading()
    {
        return PositionUtil.GetHeadingTowards(GetX(), GetY(), GetRelationshipCrystal().GetSpawn().GetX(), GetRelationshipCrystal().GetSpawn().GetY());
    }

    public int GetTownLevel()
    {
        if (GetAddress().GetTownId() == 0)
            return 0;
        return TownService.GetInstance().GetTownById(GetAddress().GetTownId()).GetLevel();
    }

    /// <returns>
    /// Seconds until this inactive house will be activated and the old one gets removed from the owner. Returns -1 if this house is already active.
    /// </returns>
    public int SecondsUntilGraceEnd()
    {
        if (IsInactive())
        {
            DateTimeOffset graceEndTime = FindGraceEndTime();
            return Math.Max(0, (int)((graceEndTime.ToUnixTimeMilliseconds() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000));
        }
        return -1;
    }

    /// <summary>
    /// Grace end happens on auction end, so we find the nearest auction end date taking place around two weeks after the house was bought.
    /// </summary>
    private DateTimeOffset FindGraceEndTime()
    {
        long maxGraceEndTimeMillis = GetAcquiredTime().Value.ToUnixTimeMilliseconds() + 14L * 24 * 60 * 60 * 1000;
        DateTimeOffset auctionEndTime = AuctionEndTask.GetInstance().GetNextRunAfter(GetAcquiredTime().Value);
        DateTimeOffset graceEndTime = auctionEndTime;
        while ((auctionEndTime = AuctionEndTask.GetInstance().GetNextRunAfter(auctionEndTime)).ToUnixTimeMilliseconds() <= maxGraceEndTimeMillis)
            graceEndTime = auctionEndTime;
        return graceEndTime;
    }

    public bool MatchesLandRace(Race race)
    {
        bool isEly = DataManager.NPC_DATA.GetNpcTemplate(GetLand().GetManagerNpcId()).GetTribe() == TribeClass.GENERAL;
        return race == Race.ELYOS && isEly || race == Race.ASMODIANS && !isEly;
    }

    public void SendScripts(Player player)
    {
        if (playerScripts == null)
            return;
        playerScripts.SendToPlayer(player, address.GetId());
    }
}
