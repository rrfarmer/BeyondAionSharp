using System;
using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.World;
using Aion.GameServer.World.Knownlist;
using LocationData = Aion.GameServer.Dataholders.PlayerInitialData.LocationData;
using PlayerCreationData = Aion.GameServer.Dataholders.PlayerInitialData.PlayerCreationData;
using ItemType = Aion.GameServer.Dataholders.PlayerInitialData.PlayerCreationData.ItemType;
using PunishmentType = Aion.GameServer.Services.PunishmentService.PunishmentType;
using PersistentState = Aion.GameServer.Model.GameObjects.IPersistable.PersistentState;

namespace Aion.GameServer.Services.Players;

/// <summary>Java parity: services/player/PlayerService (SoulKeeper, Saelya, Cura). Loads/stores/creates players: isNameUsedOrReserved, storeNewPlayer, storePlayer (all DAO persistence), getPlayer (full load: legion/macros/skills/lists/effects/cooldowns/inventory/warehouse/pet bags/cabinets/equipment stats/punishments/emotions), newPlayer (spawn loc, starting skills + creation items auto-equip, mailbox), getOrLoadPlayerCommonData, cancel/deletePlayer (deletion timer), deletePlayerFromDB, storeCreationTime, add/removeMacro, getPlayerName. Timestamp->DateTimeOffset (getTime->ToUnixTimeMilliseconds, new Timestamp(ms)->FromUnixTimeMilliseconds); currentTimeMillis->UtcNow. Most DAO/model types red-tolerated.</summary>
public class PlayerService
{
    /// <summary>
    /// Returns true if the name is taken (used by another character or recently reserved via rename).
    /// </summary>
    public static bool IsNameUsedOrReserved(string oldName, string newName)
    {
        return IsNameUsedOrReserved(oldName, newName, NameConfig.RESERVE_OLD_NAME_DAYS);
    }

    public static bool IsNameUsedOrReserved(string oldName, string newName, int nameReservationDurationDays)
    {
        return PlayerDAO.IsNameUsed(newName) || OldNamesDAO.IsNameReserved(oldName, newName, nameReservationDurationDays);
    }

    /// <summary>
    /// Stores newly created player. Returns true if character was successfully saved.
    /// </summary>
    public static bool StoreNewPlayer(Player player, string accountName, int accountId)
    {
        return PlayerDAO.SaveNewPlayer(player, accountId, accountName)
            && PlayerAppearanceDAO.Store(player) && PlayerSkillListDAO.StoreSkills(player)
            && InventoryDAO.Store(player);
    }

    /// <summary>
    /// Stores player data into db
    /// </summary>
    public static void StorePlayer(Player player)
    {
        PlayerDAO.StorePlayer(player);
        PlayerSkillListDAO.StoreSkills(player);
        PlayerSettingsDAO.SaveSettings(player);
        PlayerQuestListDAO.Store(player);
        AbyssRankDAO.StoreAbyssRank(player);
        PlayerPunishmentsDAO.StorePlayerPunishment(player, PunishmentType.PRISON);
        PlayerPunishmentsDAO.StorePlayerPunishment(player, PunishmentType.GATHER);
        InventoryDAO.Store(player);
        foreach (House house in player.GetHouses())
            house.Save();
        ItemStoneListDAO.Save(player);
        MailDAO.StoreMailbox(player);
        PortalCooldownsDAO.StorePortalCooldowns(player);
        CraftCooldownsDAO.StoreCraftCooldowns(player);
        HouseObjectCooldownsDAO.StoreHouseObjectCooldowns(player);
        PlayerNpcFactionsDAO.StoreNpcFactions(player);
        AccountPassportsDAO.StorePassport(player.GetAccount());
        if (EventsConfig.ENABLE_HEADHUNTING)
            HeadhuntingDAO.StoreHeadhunter(player.GetObjectId());
    }

    public static Player GetPlayer(int playerObjId, Account account)
    {
        // Player common data and appearance should be already loaded in account
        PlayerAccountData playerAccountData = account.GetPlayerAccountData(playerObjId);
        PlayerCommonData pcd = playerAccountData.GetPlayerCommonData();
        Player player = new Player(playerAccountData, account);
        int oldOwnerId = pcd.GetWorldOwnerId();
        player.SetPosition(Aion.GameServer.World.World.GetInstance().CreatePosition(pcd.GetMapId(), pcd.GetX(), pcd.GetY(), pcd.GetZ(), pcd.GetHeading(), 0));
        pcd.SetWorldOwnerId(oldOwnerId);
        LegionMember legionMember = LegionService.GetInstance().GetLegionMember(pcd);
        if (legionMember != null)
        {
            player.SetLegionMember(legionMember);
        }

        player.SetMacros(PlayerMacrosDAO.LoadMacros(playerObjId));
        player.SetSkillList(PlayerSkillListDAO.LoadSkillList(playerObjId));
        player.SetKnownlist(new KnownList(player));
        player.SetFriendList(FriendListDAO.Load(player));
        player.SetBlockList(BlockListDAO.Load(playerObjId));
        player.SetTitleList(PlayerTitleListDAO.LoadTitleList(playerObjId));
        player.SetPlayerSettings(PlayerSettingsDAO.LoadSettings(playerObjId));
        AbyssRankDAO.LoadAbyssRank(player);
        PlayerNpcFactionsDAO.LoadNpcFactions(player);
        MotionDAO.LoadMotionList(player);
        AccountPassportsDAO.LoadPassport(player.GetAccount());
        player.SetEffectController(new PlayerEffectController(player));
        player.SetFlyController(new FlyController(player));
        PlayerStatFunctions.AddPredefinedStatFunctions(player);

        player.SetQuestStateList(PlayerQuestListDAO.Load(playerObjId));
        player.SetRecipeList(PlayerRecipesDAO.Load(player.GetObjectId()));

        account.GetAccountWarehouse().SetOwner(player);
        InventoryDAO.LoadStorage(playerObjId, player.GetInventory());
        ItemStoneListDAO.Load(player.GetInventory().GetItems());
        ItemStoneListDAO.Load(player.GetEquipment().GetEquippedItemsWithoutStigma());

        InventoryDAO.LoadStorage(playerObjId, player.GetWarehouse());
        ItemStoneListDAO.Load(player.GetWarehouse().GetItems());

        foreach (Storage petBag in player.GetPetBags())
        {
            InventoryDAO.LoadStorage(playerObjId, petBag);
            ItemStoneListDAO.Load(petBag.GetItems());
        }
        foreach (Storage cabinet in player.GetCabinets())
        {
            InventoryDAO.LoadStorage(playerObjId, cabinet);
            ItemStoneListDAO.Load(cabinet.GetItems());
        }

        // Apply equipment stats (items and manastones were loaded in account)
        player.GetEquipment().OnLoadApplyEquipmentStats();

        PlayerPunishmentsDAO.LoadPlayerPunishments(player);

        // load saved effects
        PlayerEffectsDAO.LoadPlayerEffects(player);
        // load saved player cooldowns
        PlayerCooldownsDAO.LoadPlayerCooldowns(player);
        // load item cooldowns
        ItemCooldownsDAO.LoadItemCooldowns(player);
        // load portal cooldowns
        PortalCooldownsDAO.LoadPortalCooldowns(player);
        // load house object use cooldowns
        HouseObjectCooldownsDAO.LoadHouseObjectCooldowns(player);
        // load bind point
        PlayerBindPointDAO.LoadBindPoint(player);
        // load craft cooldowns
        CraftCooldownsDAO.LoadCraftCooldowns(player);

        PlayerLifeStatsDAO.LoadPlayerLifeStat(player);
        PlayerEmotionListDAO.LoadEmotions(player);
        if (player.HasPermission(MembershipConfig.EMOTIONS_ALL))
        {
            foreach (int emotionId in EmotionLearnAction.GetLearnableEmotionIds())
                player.GetEmotions().Add(emotionId, 0, false);
        }

        return player;
    }

    /// <summary>
    /// This method is used for creating new players
    /// </summary>
    public static Player NewPlayer(PlayerAccountData playerAccountData, Account account)
    {
        PlayerCommonData playerCommonData = playerAccountData.GetPlayerCommonData();
        PlayerInitialData playerInitialData = DataManager.PLAYER_INITIAL_DATA;
        LocationData ld = playerInitialData.GetSpawnLocation(playerCommonData.GetRace());

        playerCommonData.SetMapId(ld.GetMapId());
        playerCommonData.SetX(ld.GetX());
        playerCommonData.SetY(ld.GetY());
        playerCommonData.SetZ(ld.GetZ());
        playerCommonData.SetHeading(ld.GetHeading());

        Player newPlayer = new Player(playerAccountData, account);

        // Starting skills
        newPlayer.SetSkillList(new PlayerSkillList());
        SkillLearnService.LearnNewSkills(newPlayer, 1, newPlayer.GetLevel());

        // Starting items
        PlayerCreationData playerCreationData = playerInitialData.GetPlayerCreationData(playerCommonData.GetPlayerClass());
        if (playerCreationData != null)
        { // player transfer
            List<ItemType> items = playerCreationData.GetItems();
            foreach (ItemType itemType in items)
            {
                int itemId = itemType.GetTemplate().GetTemplateId();
                Item item = ItemFactory.NewItem(itemId, itemType.GetCount());
                if (item == null)
                {
                    continue;
                }

                // When creating new player - all equipment that has slot values will be equipped
                // Make sure you will not put into xml file more items than possible to equip.
                ItemTemplate itemTemplate = item.GetItemTemplate();

                if ((itemTemplate.IsArmor() || itemTemplate.IsWeapon()) && !newPlayer.GetEquipment().IsSlotEquipped(itemTemplate.GetItemSlot()))
                {
                    item.SetEquipped(true);
                    ItemSlot itemSlot = ItemSlotExtensions.GetSlotFor(itemTemplate.GetItemSlot());
                    item.SetEquipmentSlot(itemSlot.GetSlotIdMask());
                }
                newPlayer.GetInventory().OnLoadHandler(item);
            }
        }
        newPlayer.SetMailbox(new Mailbox(newPlayer));

        // Mark inventory and equipment as UPDATE_REQUIRED to be saved during character creation
        newPlayer.GetInventory().SetPersistentState(PersistentState.UPDATE_REQUIRED);
        newPlayer.GetEquipment().SetPersistentState(PersistentState.UPDATE_REQUIRED);
        return newPlayer;
    }

    public static PlayerCommonData GetOrLoadPlayerCommonData(int playerObjId)
    {
        Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(playerObjId);
        if (player == null)
            return PlayerDAO.LoadPlayerCommonData(playerObjId);
        return player.GetCommonData();
    }

    public static PlayerCommonData GetOrLoadPlayerCommonData(string name)
    {
        Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(name);
        if (player == null)
            return PlayerDAO.LoadPlayerCommonDataByName(name);
        return player.GetCommonData();
    }

    /// <summary>
    /// Cancel Player deletion process if its possible. Returns true if deletion was successfully canceled.
    /// </summary>
    public static bool CancelPlayerDeletion(PlayerAccountData accData)
    {
        if (accData.GetDeletionDate() == null)
        {
            return true;
        }

        if (accData.GetDeletionDate().Value.ToUnixTimeMilliseconds() > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            accData.SetDeletionDate(null);
            StoreDeletionTime(accData);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Starts player deletion process if its possible. If possible, character should be deleted after 5 minutes.
    /// </summary>
    public static void DeletePlayer(PlayerAccountData accData)
    {
        if (accData.GetDeletionDate() != null)
        {
            return;
        }

        accData.SetDeletionDate(DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + CustomConfig.CHARACTER_DELETION_TIME_MINUTES * 60 * 1000));
        StoreDeletionTime(accData);
    }

    /// <summary>
    /// Completely removes player from database
    /// </summary>
    public static void DeletePlayerFromDB(int playerId)
    {
        DeletePlayerFromDB(playerId, true);
    }

    public static void DeletePlayerFromDB(int playerId, bool notifyServices)
    {
        InventoryDAO.DeletePlayerOrLegionItems(playerId);
        PlayerDAO.DeletePlayer(playerId);
        if (notifyServices)
        {
            HousingService.GetInstance().OnPlayerDeleted(playerId);
            BrokerService.GetInstance().OnPlayerDeleted(playerId);
        }
    }

    /// <summary>
    /// Updates deletion time in database
    /// </summary>
    private static void StoreDeletionTime(PlayerAccountData accData)
    {
        PlayerDAO.UpdateDeletionTime(accData.GetPlayerCommonData().GetPlayerObjId(), accData.GetDeletionDate());
    }

    public static void StoreCreationTime(int objectId, DateTimeOffset creationDate)
    {
        PlayerDAO.StoreCreationTime(objectId, creationDate);
    }

    /// <summary>
    /// Add macro for player
    /// </summary>
    public static void AddMacro(Player player, int macroOrder, string macroXML)
    {
        if (player.GetMacros().Add(macroOrder, macroXML))
        {
            PlayerMacrosDAO.AddMacro(player.GetObjectId(), macroOrder, macroXML);
        }
        else
        {
            PlayerMacrosDAO.UpdateMacro(player.GetObjectId(), macroOrder, macroXML);
        }
    }

    /// <summary>
    /// Remove macro with specified index from specified player
    /// </summary>
    public static void RemoveMacro(Player player, int macroOrder)
    {
        if (player.GetMacros().Remove(macroOrder))
        {
            PlayerMacrosDAO.DeleteMacro(player.GetObjectId(), macroOrder);
        }
    }

    public static string GetPlayerName(int objectId)
    {
        Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(objectId);
        if (player != null)
            return player.GetName();
        return PlayerDAO.GetPlayerNameByObjId(objectId);
    }
}
