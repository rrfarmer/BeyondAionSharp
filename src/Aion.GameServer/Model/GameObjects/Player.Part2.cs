using Aion.GameServer.Dataholders;
using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.State;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity: model/gameobjects/player/Player — partial #2 (Java lines ~515-861): storage/warehouse,
/// settings/titles/group/abyss, effect-controller, legion, fly-state, prison, protection, flight-path, access.
/// </summary>
public partial class Player
{
    public Aion.GameServer.Model.Items.Storage.Storage GetStorage(int storageType)
    {
        if (storageType == Aion.GameServer.Model.Items.Storage.StorageType.CUBE.GetId())
            return inventory;

        if (storageType == Aion.GameServer.Model.Items.Storage.StorageType.REGULAR_WAREHOUSE.GetId())
            return regularWarehouse;

        if (storageType == Aion.GameServer.Model.Items.Storage.StorageType.ACCOUNT_WAREHOUSE.GetId())
            return playerAccount.GetAccountWarehouse();

        if (storageType == Aion.GameServer.Model.Items.Storage.StorageType.LEGION_WAREHOUSE.GetId() && GetLegion() != null)
            return new Aion.GameServer.Model.Items.Storage.LegionStorageProxy(GetLegion().GetLegionWarehouse(), this);

        if (storageType >= Aion.GameServer.Model.Items.Storage.StorageType.PET_BAG_MIN && storageType <= Aion.GameServer.Model.Items.Storage.StorageType.PET_BAG_MAX)
            return petBags[storageType - Aion.GameServer.Model.Items.Storage.StorageType.PET_BAG_MIN];

        if (storageType >= Aion.GameServer.Model.Items.Storage.StorageType.HOUSE_WH_MIN && storageType <= Aion.GameServer.Model.Items.Storage.StorageType.HOUSE_WH_MAX)
            return cabinets[storageType - Aion.GameServer.Model.Items.Storage.StorageType.HOUSE_WH_MIN];

        return null;
    }

    public Aion.GameServer.Model.Items.Storage.Storage[] GetPetBags()
    {
        return petBags;
    }

    public Aion.GameServer.Model.Items.Storage.Storage[] GetCabinets()
    {
        return cabinets;
    }

    /// <summary>Items from UPDATE_REQUIRED storages and equipment.</summary>
    public List<Item> GetDirtyItemsToUpdate()
    {
        List<Item> dirtyItems = new List<Item>();

        foreach (Aion.GameServer.Model.Items.Storage.StorageType st in Aion.GameServer.Model.Items.Storage.StorageType.Values())
        {
            Aion.GameServer.Model.Items.Storage.IStorage storage = GetStorage(st.GetId());
            if (storage != null && storage.GetPersistentState() == Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATE_REQUIRED)
            {
                dirtyItems.AddRange(storage.GetItemsWithKinah());
                dirtyItems.AddRange(storage.GetDeletedItems());
                storage.SetPersistentState(Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATED);
            }
        }

        if (equipment.GetPersistentState() == Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATE_REQUIRED)
        {
            dirtyItems.AddRange(equipment.GetEquippedItems());
            equipment.SetPersistentState(Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.UPDATED);
        }

        return dirtyItems;
    }

    public List<Item> GetAllItems()
    {
        List<Item> items = new List<Item>();
        items.AddRange(inventory.GetItemsWithKinah());
        items.AddRange(regularWarehouse.GetItemsWithKinah());
        items.AddRange(playerAccount.GetAccountWarehouse().GetItemsWithKinah());
        foreach (Aion.GameServer.Model.Items.Storage.Storage petBag in petBags)
            items.AddRange(petBag.GetItemsWithKinah());
        foreach (Aion.GameServer.Model.Items.Storage.Storage cabinet in cabinets)
            items.AddRange(cabinet.GetItemsWithKinah());
        items.AddRange(GetEquipment().GetEquippedItems());
        return items;
    }

    public Aion.GameServer.Model.Items.Storage.Storage GetInventory()
    {
        return inventory;
    }

    public Aion.GameServer.Model.GameObjects.Players.PlayerSettings GetPlayerSettings()
    {
        return playerSettings;
    }

    public void SetPlayerSettings(Aion.GameServer.Model.GameObjects.Players.PlayerSettings playerSettings)
    {
        this.playerSettings = playerSettings;
    }

    public Aion.GameServer.Model.GameObjects.Players.Title.TitleList GetTitleList()
    {
        return titleList;
    }

    public void SetTitleList(Aion.GameServer.Model.GameObjects.Players.Title.TitleList titleList)
    {
        this.titleList = titleList;
        titleList.SetOwner(this);
    }

    public Aion.GameServer.Model.Team.Group.PlayerGroup GetPlayerGroup()
    {
        return playerGroup;
    }

    public void SetPlayerGroup(Aion.GameServer.Model.Team.Group.PlayerGroup playerGroup)
    {
        this.playerGroup = playerGroup;
    }

    public Aion.GameServer.Model.GameObjects.Players.AbyssRank GetAbyssRank()
    {
        return abyssRank;
    }

    public void SetAbyssRank(Aion.GameServer.Model.GameObjects.Players.AbyssRank abyssRank)
    {
        this.abyssRank = abyssRank;
    }

    public override Aion.GameServer.Controllers.Effects.PlayerEffectController GetEffectController()
    {
        return (Aion.GameServer.Controllers.Effects.PlayerEffectController)base.GetEffectController();
    }

    /// <summary>Returns true if has valid LegionMember.</summary>
    public bool IsLegionMember()
    {
        return legionMember != null;
    }

    public void SetLegionMember(Aion.GameServer.Model.Team.Legion.LegionMember legionMember)
    {
        this.legionMember = legionMember;
    }

    public Aion.GameServer.Model.Team.Legion.LegionMember GetLegionMember()
    {
        return legionMember;
    }

    public Aion.GameServer.Model.Team.Legion.Legion GetLegion()
    {
        return legionMember != null ? legionMember.GetLegion() : null;
    }

    /// <summary>True if a player has a store opened.</summary>
    public bool HasStore()
    {
        return GetStore() != null;
    }

    /// <summary>Removes legion from player.</summary>
    public void ResetLegionMember()
    {
        SetLegionMember(null);
    }

    public bool IsInGroup()
    {
        return playerGroup != null;
    }

    /// <summary>The account name of this player.</summary>
    public string GetAccountName()
    {
        return playerAccount.GetName();
    }

    public int GetWarehouseExpansions()
    {
        return GetCommonData().GetWhNpcExpands() + GetCommonData().GetWhBonusExpands();
    }

    public int GetWhNpcExpands()
    {
        return GetCommonData().GetWhNpcExpands();
    }

    public int GetWhBonusExpands()
    {
        return GetCommonData().GetWhBonusExpands();
    }

    public void SetWarehouseLimit()
    {
        GetWarehouse().SetLimit(Aion.GameServer.Model.Items.Storage.StorageType.REGULAR_WAREHOUSE.GetLimit() + (GetWarehouseExpansions() * GetWarehouse().GetRowLength()));
    }

    public Aion.GameServer.Model.Items.Storage.Storage GetWarehouse()
    {
        return regularWarehouse;
    }

    /// <summary>0: regular, 1: fly, 2: glide — bitset.</summary>
    public int GetFlyState()
    {
        return flyState;
    }

    public void SetFlyState(Aion.GameServer.Model.GameObjects.State.FlyState flyState)
    {
        this.flyState |= flyState.GetId();
    }

    public void UnsetFlyState(Aion.GameServer.Model.GameObjects.State.FlyState flyState)
    {
        this.flyState &= ~flyState.GetId();
    }

    public bool IsInFlyState(Aion.GameServer.Model.GameObjects.State.FlyState flyState)
    {
        return (this.flyState & flyState.GetId()) == flyState.GetId();
    }

    /// <summary>CreatureState is unreliable for players; returns true if player is flying or gliding.</summary>
    public override bool IsFlying()
    {
        return flyState >= 1;
    }

    /// <summary>CreatureState is unreliable for players; returns true if player is flying.</summary>
    public override bool IsInFlyingState()
    {
        return IsInFlyState(Aion.GameServer.Model.GameObjects.State.FlyState.FLYING);
    }

    public bool IsInGlidingState()
    {
        return IsInFlyState(Aion.GameServer.Model.GameObjects.State.FlyState.GLIDING);
    }

    public bool IsTrading()
    {
        return Aion.GameServer.Services.ExchangeService.GetInstance().IsPlayerInExchange(this);
    }

    public bool IsInPrison()
    {
        return GetPrisonDurationSeconds() > 0;
    }

    public void SetPrisonEndTimeMillis(long prisonEndTimeMillis)
    {
        this.prisonEndTimeMillis = prisonEndTimeMillis;
    }

    public int GetPrisonDurationSeconds()
    {
        if (prisonEndTimeMillis == 0)
            return 0;
        int durationSeconds = (int)((prisonEndTimeMillis - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000);
        if (durationSeconds < 0)
            prisonEndTimeMillis = durationSeconds = 0;
        return durationSeconds;
    }

    public bool IsProtectionActive()
    {
        return IsInVisualState(CreatureVisualState.BLINKING);
    }

    public override bool IsInvulnerable()
    {
        return IsInCustomState(Aion.GameServer.Model.GameObjects.Players.CustomPlayerState.INVULNERABLE);
    }

    public void SetMailbox(Aion.GameServer.Model.GameObjects.Players.Mailbox mailbox)
    {
        this.mailbox = mailbox;
    }

    public Aion.GameServer.Model.GameObjects.Players.Mailbox GetMailbox()
    {
        return mailbox;
    }

    public Aion.GameServer.Controllers.FlyController GetFlyController()
    {
        return flyController;
    }

    public void SetFlyController(Aion.GameServer.Controllers.FlyController flyController)
    {
        this.flyController = flyController;
    }

    public void SetCraftingTask(Aion.GameServer.SkillEngine.Task.CraftingTask craftingTask)
    {
        this.craftingTask = craftingTask;
    }

    public Aion.GameServer.SkillEngine.Task.CraftingTask GetCraftingTask()
    {
        return craftingTask;
    }

    public void SetFlightTeleportId(int flightTeleportId)
    {
        SetFlightPath(new Aion.GameServer.Model.Templates.Flypath.FlightPath(Aion.GameServer.Model.Templates.Flypath.FlightPath.Type.FLIGHT_TRANSPORTER, flightTeleportId, 0));
    }

    public void SetFlightPath(Aion.GameServer.Model.Templates.Flypath.FlightPath flightPath)
    {
        this.flightPath = flightPath;
    }

    public bool IsUsingFlightPath(Aion.GameServer.Model.Templates.Flypath.FlightPath.Type type)
    {
        return flightPath != null && flightPath.GetType_() == type && IsInState(CreatureState.FLYING);
    }

    public bool IsUsingFlightTransporterOrWindstream()
    {
        return flightPath != null && IsInState(CreatureState.FLYING);
    }

    public Aion.GameServer.Model.Templates.Flypath.FlightPath GetFlightPath()
    {
        return flightPath;
    }

    public void SetCurrentFlypath(Aion.GameServer.Model.Templates.Flypath.FlyPathEntry path)
    {
        this.flyLocationId = path;
        if (path != null)
            this.flyStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        else
            this.flyStartTime = 0;
    }

    /// <summary>True if the player has the specified access level or higher.</summary>
    public bool HasAccess(sbyte accessLevel)
    {
        return playerAccount.GetAccessLevel() >= accessLevel;
    }

    /// <summary>True if the player is a member of the server staff.</summary>
    public bool IsStaff()
    {
        return playerAccount.GetAccessLevel() > 0;
    }
}
