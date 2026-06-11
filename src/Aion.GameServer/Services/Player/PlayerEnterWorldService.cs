using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer;
using Aion.GameServer.Cache;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Custom.Pvpmap;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Network.Aion.Skillinfo;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.Craft;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Panesterra;
using Aion.GameServer.Services.Reward;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Services.Toypet;
using Aion.GameServer.Skillengine;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World;
using static Aion.GameServer.Configs.Main.SecurityConfig.MultiClientingRestrictionMode;
using Msg = Aion.GameServer.Network.Aion.Serverpackets.SM_ENTER_WORLD_CHECK.Msg;
using Status = Aion.GameServer.Model.GameObjects.Players.FriendList.Status;
using ConnectType = Aion.GameServer.Model.Account.CharacterPasskey.ConnectType;
using PunishmentType = Aion.GameServer.Services.PunishmentService.PunishmentType;
using PersistentState = Aion.GameServer.Model.GameObjects.Persistable.PersistentState;

namespace Aion.GameServer.Services.Players;

/// <summary>Java parity: services/player/PlayerEnterWorldService (ATracer, Neon). Full enter-world handshake: validation (account/pcd, online/reentry, ban, passkey, dupe), multi-client gate, then the retail login packet sequence (skills/quests/titles/UI/items/warehouse/abyss/legion/group/mail/housing/etc.), energy-of-repose, passive skill activation, fortress/vortex zone relocation, expirable registration, periodic save tasks. ConcurrentLinkedQueue<Integer> dedupe gate -> ConcurrentDictionary<int,byte> set (ContainsKey/TryAdd/TryRemove); stream findAny.map.orElse->LINQ; Timestamp->DateTimeOffset (getTime->ToUnixTimeMilliseconds); Throwable->Exception; IllegalState->InvalidOperation; Math.round->(long)Floor(x+0.5); lossy long*=float->explicit cast; static-import enum->using static; nested aliases. Named Runnable tasks GeneralUpdateTask/ItemUpdateTask -> classes w/ Run(). Many service/DAO/packet/SplitList types red-tolerated.</summary>
public sealed class PlayerEnterWorldService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("GAMECONNECTION_LOG");
    private static readonly string VERSION_INFO = "Server " + GameServer.versionInfo.GetBuildInfo(GSConfig.TIME_ZONE_ID);
    private static readonly ConcurrentDictionary<int, byte> enteringWorld = new ConcurrentDictionary<int, byte>(); // Java ConcurrentLinkedQueue used as a concurrent set

    public static void EnterWorld(AionConnection client, int objectId)
    {
        Account account = client.GetAccount();
        PlayerAccountData playerAccData = account.GetPlayerAccountData(objectId);
        if (playerAccData == null)
        {
            log.LogWarning("Player enterWorld fail: character obj ID {ObjectId} was not found on account ID {AccountId}.", objectId, account.GetId());
            client.SendPacket(new SM_ENTER_WORLD_CHECK(Msg.CONNECTION_ERROR));
            return;
        }

        PlayerCommonData pcd = playerAccData.GetPlayerCommonData();
        if (pcd == null)
        {
            log.LogWarning("Player enterWorld fail: CommonData for character obj ID {ObjectId} is null.", objectId);
            client.SendPacket(new SM_ENTER_WORLD_CHECK(Msg.CONNECTION_ERROR));
            return;
        }

        if (PlayerDAO.IsOnline(objectId))
        { // char is still leaving the world and not saved yet (fast reentry from plastic surgery screen or packet hack)
            client.SendPacket(new SM_ENTER_WORLD_CHECK(Msg.REENTRY_TIME));
            return;
        }
        int? onlinePlayerId = account.GetPlayerAccDataList()
            .Where(p => p.GetPlayerCommonData().IsOnline())
            .Select(p => (int?)p.GetPlayerCommonData().GetPlayerObjId())
            .FirstOrDefault();
        if (onlinePlayerId != null)
        { // a char was online during acc login (double login or client crash), so reload pcd, appearance and acc warehouse
            if (PlayerDAO.IsOnline(onlinePlayerId.Value))
            { // the found char is still leaving the world, so the acc wh might still be outdated
                client.SendPacket(new SM_ENTER_WORLD_CHECK(Msg.REENTRY_TIME));
                return;
            }
            playerAccData = AccountService.LoadPlayerAccountData(onlinePlayerId.Value);
            if (onlinePlayerId == objectId)
                pcd = playerAccData.GetPlayerCommonData(); // refresh lastOnline for reentry time validation
            account.AddPlayerAccountData(playerAccData);
            account.SetAccountWarehouse(AccountService.LoadAccountWarehouse(account));
        }

        if (World.GetInstance().IsInWorld(objectId))
        {
            log.LogWarning("Player enterWorld fail: Duplicate character obj ID {ObjectId} found in world.", objectId);
            client.SendPacket(new SM_ENTER_WORLD_CHECK(Msg.CONNECTION_ERROR));
            return;
        }

        // check if char is banned
        CharacterBanInfo cbi = playerAccData.GetCharBanInfo();
        if (cbi != null)
        {
            if (cbi.GetEnd() >= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000)
            {
                client.SendPacket(new SM_ENTER_WORLD_CHECK(Msg.CONNECTION_ERROR));
                return;
            }
            else
            {
                PlayerPunishmentsDAO.UnpunishPlayer(objectId, PunishmentType.CHARBAN);
            }
        }

        // passkey check
        if (SecurityConfig.PASSKEY_ENABLE && !account.GetCharacterPasskey().IsPass())
        {
            account.GetCharacterPasskey().SetConnectType(ConnectType.ENTER);
            account.GetCharacterPasskey().SetObjectId(objectId);
            bool isExistPasskey = PlayerPasskeyDAO.ExistCheckPlayerPasskey(account.GetId());
            client.SendPacket(new SM_CHARACTER_SELECT(!isExistPasskey ? 0 : 1));
            return;
        }

        DateTimeOffset? lastOnline = pcd.GetLastOnline();
        if (!pcd.IsInEditMode() && lastOnline != null && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastOnline.Value.ToUnixTimeMilliseconds() < (GSConfig.CHARACTER_REENTRY_TIME * 1000))
        {
            client.SendPacket(new SM_ENTER_WORLD_CHECK(Msg.REENTRY_TIME));
            return;
        }

        Player player = PlayerService.GetPlayer(objectId, account);
        if (!enteringWorld.ContainsKey(objectId) && enteringWorld.TryAdd(objectId, 0))
        {
            try
            {
                if (player.IsStaff() || MultiClientingService.TryEnterWorld(player, client))
                {
                    EnterWorld(client, player);
                }
                else
                {
                    Msg msg = SecurityConfig.MULTI_CLIENTING_RESTRICTION_MODE == SAME_FACTION ? Msg.BOTH_FACTIONS : Msg.CONNECTION_ERROR;
                    client.SendPacket(new SM_ENTER_WORLD_CHECK(msg));
                }
            }
            catch (Exception ex)
            {
                player.GetController().Delete();
                pcd.SetOnline(false);
                PlayerDAO.OnlinePlayer(player, false);
                player.SetClientConnection(null);
                client.SetActivePlayer(null);
                client.SendPacket(new SM_ENTER_WORLD_CHECK(Msg.CONNECTION_ERROR));
                log.LogError(ex, "Error during enter world of " + player);
            }
            finally
            {
                enteringWorld.TryRemove(objectId, out _);
            }
        }
    }

    private static void EnterWorld(AionConnection client, Player player)
    {
        Account account = player.GetAccount();
        PlayerCommonData pcd = player.GetCommonData();

        client.ResetPingFailCount();
        ActivatePassiveSkillEffects(player); // before setClientConnection to avoid packet spam
        player.SetClientConnection(client);
        if (!client.SetActivePlayer(player))
            throw new InvalidOperationException("Couldn't set active player");
        pcd.SetOnline(true);
        player.GetFriendList().SetStatus(Status.ONLINE, pcd);
        PlayerDAO.OnlinePlayer(player, true);
        PlayerDAO.StoreLastOnlineTime(player.GetObjectId(), DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        log.LogInformation("Player " + player.GetName() + " (" + account + ") logged on");
        pcd.SetInEditMode(false);

        World.GetInstance().StoreObject(player);

        // change player position if he isn't allowed to spawn in the current zone
        if (ValidateFortressZone(player)) // only check vortex zone if fortress check was ok (otherwise, the player is already set to bind point)
            ValidateVortexZone(player);

        // if player skipped some levels offline, learn missing skills and stuff
        player.GetController().OnLevelChange(PlayerDAO.GetOldCharacterLevel(player.GetObjectId()), player.GetLevel());

        // Energy of Repose must be calculated before sending SM_STATS_INFO
        if (pcd.GetLastOnline() != null)
        {
            long secondsOffline = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - pcd.GetLastOnline().Value.ToUnixTimeMilliseconds()) / 1000;
            if (secondsOffline > 10 * 60) // 10 mins offline = 0 salvation points
                pcd.ResetSalvationPoints();

            UpdateEnergyOfRepose(player, secondsOffline);

            if (secondsOffline > 5 * 60)
                pcd.SetDp(0);
        }

        client.SendPacket(new SM_HOUSE_SCRIPTS(0, PlayerScript.LUA_SANDBOX_FIX)); // client executes this immediately (scary, right?!)
        client.SendPacket(new SM_UNK_3_5_1());
        StigmaService.OnPlayerLogin(player);
        client.SendPacket(new SM_ENTER_WORLD_CHECK());

        InstanceService.OnPlayerLogin(player);
        // Update player skills first!!!
        if (player.HasAccess(AdminConfig.GM_SKILLS))
            GMService.GetInstance().AddGmSkills(player);
        AbyssSkillService.UpdateSkills(player);
        SplitList<PlayerSkillEntry> skillEntrySplitList = new DynamicServerPacketBodySplitList<PlayerSkillEntry>(player.GetSkillList().GetAllSkills(), false,
            SM_SKILL_LIST.STATIC_BODY_SIZE, SkillEntryWriter.DYNAMIC_BODY_PART_SIZE_CALCULATOR);
        skillEntrySplitList.ForEach(part => PacketSendUtility.SendPacket(player, new SM_SKILL_LIST(part)));
        if (player.GetSkillCoolDowns() != null)
            client.SendPacket(new SM_SKILL_COOLDOWN(player, player.GetSkillCoolDowns(), false));

        if (player.GetItemCoolDowns().Count != 0)
            client.SendPacket(new SM_ITEM_COOLDOWN(player.GetItemCoolDowns()));

        QuestEngine.GetInstance().SendCompletedQuests(player);
        client.SendPacket(new SM_QUEST_LIST(player.GetQuestStateList().GetUncompletedQuests()));
        client.SendPacket(new SM_TITLE_INFO(pcd.GetTitleId()));
        if (pcd.GetBonusTitleId() != 0)
        {
            player.GetTitleList().SetBonusTitle(pcd.GetBonusTitleId());
        }
        client.SendPacket(new SM_MOTION(player.GetMotions().GetMotions().Values));
        client.SendPacket(new SM_AFTER_TIME_CHECK_4_7_5()); // it is also sent after enter world check

        byte[] uiSettings = player.GetPlayerSettings().GetUiSettings();
        byte[] shortcuts = player.GetPlayerSettings().GetShortcuts();
        byte[] houseBuddies = player.GetPlayerSettings().GetHouseBuddies();

        if (uiSettings != null)
            client.SendPacket(new SM_UI_SETTINGS(uiSettings, 0));

        if (shortcuts != null)
            client.SendPacket(new SM_UI_SETTINGS(shortcuts, 1));

        if (houseBuddies != null)
            client.SendPacket(new SM_UI_SETTINGS(houseBuddies, 2));

        SendItemInfos(client, player);

        client.SendPacket(new SM_CHANNEL_INFO(player.GetPosition()));

        KiskService.GetInstance().OnLogin(player);
        TeleportService.SendObeliskBindPoint(player);
        TeleportService.SendKiskBindPoint(player);

        PanesterraService.GetInstance().OnEnterPanesterra(player);

        // ----------------------------- Retail sequence -----------------------------
        client.SendPacket(new SM_PLAYER_SPAWN(player));
        // SM_WEATHER miss on login (but he 'live' in CM_LEVEL_READY.. need investigate)
        client.SendPacket(new SM_GAME_TIME());
        if (player.IsLegionMember())
            LegionService.GetInstance().OnLogin(player);
        SendWarehouseItemInfos(client, player);
        client.SendPacket(new SM_TITLE_INFO(player));
        client.SendPacket(new SM_EMOTION_LIST((byte)0, player.GetEmotions().GetEmotions()));
        // SM_BD_UNK h 0
        SiegeService.GetInstance().OnPlayerLogin(player);
        client.SendPacket(new SM_PRICES());
        if (player.GetCraftCooldowns().Count != 0)
            client.SendPacket(new SM_RECIPE_COOLDOWN(player, 1));
        BindPointTeleportService.OnLogin(player);
        client.SendPacket(new SM_FRIEND_LIST());
        client.SendPacket(new SM_BLOCK_LIST());
        if (AutoGroupConfig.AUTO_GROUP_ENABLE)
        {
            AutoGroupService.GetInstance().OnPlayerLogin(player);
        }
        client.SendPacket(new SM_INSTANCE_INFO((byte)2, player));
        client.SendPacket(new SM_ABYSS_RANK(player));
        client.SendPacket(new SM_STATS_INFO(player));
        // ----------------------------- Retail sequence -----------------------------

        if (player.HasAccess(AdminConfig.REVISION_INFO_ON_LOGIN))
            PacketSendUtility.SendMessage(player, VERSION_INFO, ChatType.WHITE);

        if (account.GetMembership() > 0 && account.GetMembership() <= MembershipConfig.MEMBERSHIP_TYPES.Length)
        {
            string accountType = MembershipConfig.MEMBERSHIP_TYPES[account.GetMembership() - 1];
            client.SendPacket(new SM_MESSAGE(0, null, "Your account is " + accountType, ChatType.GOLDEN_YELLOW));
        }

        // Alliance Packet after SetBindPoint
        PlayerAllianceService.OnPlayerLogin(player);

        PunishmentService.UpdatePrisonStatus(player);

        PlayerGroupService.OnPlayerLogin(player);
        PetService.GetInstance().OnPlayerLogin(player);

        // ----------------------------- Retail sequence -----------------------------
        client.SendPacket(new SM_LEGION_DOMINION_LOC_INFO());
        MailService.OnPlayerLogin(player);
        HousingBidService.GetInstance().OnPlayerLogin(player); // must ensure player mailbox is initialized first
        AtreianPassportService.GetInstance().OnLogin(player);
        SendMacroList(client, player);
        client.SendPacket(new SM_RECIPE_LIST(player.GetRecipeList().GetRecipeList()));
        BrokerService.GetInstance().OnPlayerLogin(player);
        HousingService.GetInstance().OnPlayerLogin(player); // must ensure player mailbox is initialized first
        // ----------------------------- Retail sequence -----------------------------
        if (CustomConfig.ENABLE_SIMPLE_2NDCLASS)
            ClassChangeService.ShowClassChangeDialog(player);

        GMService.GetInstance().OnPlayerLogin(player);

        if (player.GetAbyssRank().GetRank().GetId() >= AbyssRankEnum.STAR1_OFFICER.GetId())
        {
            client.SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_GLORY_POINT_LOSE_COMMON());
            client.SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_GLORY_POINT_LOSE_PERSONAL(player.GetName(), player.GetAbyssRank().GetRank().GetGpLossPerDay()));
        }

        // Trigger restore services on login.
        player.GetLifeStats().UpdateCurrentStats();
        player.GetObserveController().NotifyHPChangeObservers(player.GetLifeStats().GetCurrentHp());

        if (HTMLConfig.ENABLE_HTML_WELCOME)
            HTMLService.ShowHTML(player, HTMLCache.GetInstance().GetHTML("welcome.xhtml"));

        AdventService.GetInstance().OnLogin(player);

        player.GetNpcFactions().SendDailyQuest();

        if (HTMLConfig.ENABLE_GUIDES)
            HTMLService.OnPlayerLogin(player);

        player.GetEquipment().CheckRankLimitItems(); // Remove items after offline changed rank

        List<Expirable> expirables = new List<Expirable>();
        foreach (StorageType st in StorageType.Values())
        {
            if (st == StorageType.LEGION_WAREHOUSE)
                continue;
            IStorage storage = player.GetStorage(st.GetId());
            if (storage != null)
                expirables.AddRange(storage.GetItems());
        }
        expirables.AddRange(player.GetEquipment().GetEquippedItems());
        expirables.AddRange(player.GetMotions().GetMotions().Values);
        expirables.AddRange(player.GetEmotions().GetEmotions());
        expirables.AddRange(player.GetTitleList().GetTitles());
        ExpireTimerTask.GetInstance().RegisterExpirables(expirables, player);

        if (player.GetActiveHouse() != null)
        {
            foreach (var obj in player.GetActiveHouse().GetRegistry().GetObjects())
            {
                if (obj.GetPersistentState() != PersistentState.DELETED)
                    ExpireTimerTask.GetInstance().RegisterExpirable(obj, player);
            }
        }
        // scheduler periodic update
        player.GetController().AddTask(TaskId.PLAYER_UPDATE, ThreadPoolManager.GetInstance().ScheduleAtFixedRate(
            new GeneralUpdateTask(player.GetObjectId()), PeriodicSaveConfig.PLAYER_GENERAL * 1000, PeriodicSaveConfig.PLAYER_GENERAL * 1000));
        player.GetController().AddTask(TaskId.INVENTORY_UPDATE, ThreadPoolManager.GetInstance()
            .ScheduleAtFixedRate(new ItemUpdateTask(player.GetObjectId()), PeriodicSaveConfig.PLAYER_ITEMS * 1000, PeriodicSaveConfig.PLAYER_ITEMS * 1000));

        SurveyService.GetInstance().ShowAvailable(player);
        EventService.GetInstance().OnPlayerLogin(player);

        if (CraftConfig.DELETE_EXCESS_CRAFT_ENABLE)
            RelinquishCraftStatus.RemoveExcessCraftStatus(player, false);

        // try to send bonus pack (if mailbox was full on lvlup)
        BonusPackService.GetInstance().AddPlayerCustomReward(player);
        FactionPackService.GetInstance().AddPlayerCustomReward(player);
        VeteranRewardService.GetInstance().TryReward(player);

        PvpMapService.GetInstance().OnLogin(player);
    }

    private static void UpdateEnergyOfRepose(Player player, long secondsOffline)
    {
        player.GetCommonData().UpdateMaxRepose();
        if (player.GetCommonData().IsReadyForReposeEnergy() && secondsOffline > 4 * 3600)
        { // more than 4 hours offline: start counting Repose Energy addition
            double hours = secondsOffline / 3600d;
            // 48 hours offline = 100% Repose Energy (~1% each 30mins source: http://forums.na.aiononline.com/na/showthread.php?t=105940)
            long addReposeEnergy = (long)Math.Floor((hours / 48) * player.GetCommonData().GetMaxReposeEnergy() + 0.5);
            // Additional Energy of Repose bonus if inside house
            House house = player.GetActiveHouse();
            if (house != null)
            {
                HouseAddress hPos = house.GetAddress();
                if (player.GetWorldId() == hPos.GetMapId()
                    && PositionUtil.IsInRange(player.GetX(), player.GetY(), player.GetZ(), hPos.GetX(), hPos.GetY(), hPos.GetZ(), 7))
                    addReposeEnergy = (long)(addReposeEnergy * (house.GetHouseType() == HouseType.STUDIO ? 1.05f : 1.10f)); // apartment = 5% bonus, other houses 10%
            }
            player.GetCommonData().AddReposeEnergy(addReposeEnergy);
        }
    }

    private static void ActivatePassiveSkillEffects(Player player)
    {
        foreach (PlayerSkillEntry skillEntry in player.GetSkillList().GetAllSkills())
        {
            SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(skillEntry.GetSkillId());
            if (skillTemplate.IsPassive())
                SkillEngine.GetInstance().ApplyEffectDirectly(skillTemplate, skillEntry.GetSkillLevel(), player, player);
        }
    }

    private static bool ValidateFortressZone(Player player)
    {
        FortressLocation fortress = SiegeService.GetInstance().FindFortress(player.GetWorldId(), player.GetX(), player.GetY(), player.GetZ());
        if (fortress != null && fortress.IsVulnerable() && fortress.IsEnemy(player))
        {
            long lastOnlineMillis = player.GetCommonData().GetLastOnline() == null ? 0 : player.GetCommonData().GetLastOnline().Value.ToUnixTimeMilliseconds();
            // only relocate if the player logged out before siege start (online enemies automatically get teleported outside the fortress)
            if (lastOnlineMillis < SiegeService.GetInstance().GetSiege(fortress).GetStartTime())
            {
                BindPointPosition bind = player.GetBindPoint();
                if (bind != null)
                {
                    World.GetInstance().SetPosition(player, bind.GetMapId(), bind.GetX(), bind.GetY(), bind.GetZ(), bind.GetHeading());
                }
                else
                {
                    PlayerInitialData.LocationData start = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(player.GetRace());
                    World.GetInstance().SetPosition(player, start.GetMapId(), start.GetX(), start.GetY(), start.GetZ(), start.GetHeading());
                }
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Checks if the player is allowed to be in the current vortex zone. He will be sent to the location's home point if not.
    /// </summary>
    private static void ValidateVortexZone(Player player)
    {
        VortexLocation loc = VortexService.GetInstance().GetLocationByWorld(player.GetWorldId());
        if (loc != null && player.GetRace().Equals(loc.GetInvadersRace()))
        {
            if (loc.IsInsideLocation(player) && loc.IsActive() && loc.GetVortexController().GetPassedPlayers().ContainsKey(player.GetObjectId()))
                return;

            int mapId = loc.GetHomeWorldId();
            float x = loc.GetHomePoint().GetX();
            float y = loc.GetHomePoint().GetY();
            float z = loc.GetHomePoint().GetZ();
            byte h = loc.GetHomePoint().GetHeading();
            World.GetInstance().SetPosition(player, mapId, x, y, z, h);
        }
    }

    private static void SendItemInfos(AionConnection client, Player player)
    {
        player.SetCubeLimit();
        player.SetWarehouseLimit();
        // items
        Storage inventory = player.GetInventory();
        List<Item> allItems = new List<Item>();
        if (inventory.GetKinah() == 0)
        {
            inventory.IncreaseKinah(0); // create an empty object with value 0
        }
        allItems.Add(inventory.GetKinahItem()); // always included even with 0 count, and first in the packet !
        allItems.AddRange(player.GetEquipment().GetEquippedItems());
        allItems.AddRange(inventory.GetItems());

        SplitList<Item> inventoryItemSplitList = new FixedElementCountSplitList<Item>(allItems, true, 10);
        inventoryItemSplitList.ForEach(part => client.SendPacket(new SM_INVENTORY_INFO(part.IsFirst(), part, player)));
        client.SendPacket(new SM_INVENTORY_INFO(false, new List<Item>(), player));
    }

    private static void SendWarehouseItemInfos(AionConnection client, Player player)
    {
        WarehouseService.SendWarehouseInfo(player, true);
        // from 30 to 49, from 60 to 79
        for (int i = StorageType.PET_BAG_MIN - 2; i <= StorageType.HOUSE_WH_MAX; i++)
        {
            if (i >= 50 && i < StorageType.HOUSE_WH_MIN)
                continue;
            IStorage storage = player.GetStorage(i);
            if (storage == null || storage.GetItemsWithKinah().Count == 0)
            {
                client.SendPacket(new SM_WAREHOUSE_INFO(null, i, 0, true, player));
                continue;
            }
            SplitList<Item> warehouseItemSplitList = new FixedElementCountSplitList<Item>(storage.GetItemsWithKinah(), true, 10);
            int storageType = i;
            warehouseItemSplitList.ForEach(part => client.SendPacket(new SM_WAREHOUSE_INFO(part, storageType, 0, part.IsFirst(), player)));
            client.SendPacket(new SM_WAREHOUSE_INFO(null, storageType, 0, false, player));
            client.SendPacket(new SM_WAREHOUSE_INFO(null, i, 0, false, player));
        }
    }

    private static void SendMacroList(AionConnection client, Player player)
    {
        SplitList<Macros.Macro> macroSplitList = new DynamicServerPacketBodySplitList<Macros.Macro>(player.GetMacros().GetAll(), true, SM_MACRO_LIST.STATIC_BODY_SIZE,
            SM_MACRO_LIST.DYNAMIC_BODY_PART_SIZE_CALCULATOR);
        macroSplitList.ForEach(part => PacketSendUtility.SendPacket(player, new SM_MACRO_LIST(player.GetObjectId(), part, part.IsFirst())));
    }
}

/// <summary>Java parity: services/player/GeneralUpdateTask (package-private, implements Runnable). Periodic save of abyss rank, skills, quests, player and houses.</summary>
internal class GeneralUpdateTask : Runnable
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(GeneralUpdateTask));
    private readonly int playerId;

    internal GeneralUpdateTask(int playerId)
    {
        this.playerId = playerId;
    }

    public void Run()
    {
        Player player = World.GetInstance().GetPlayer(playerId);
        if (player != null)
        {
            try
            {
                AbyssRankDAO.StoreAbyssRank(player);
                PlayerSkillListDAO.StoreSkills(player);
                PlayerQuestListDAO.Store(player);
                PlayerDAO.StorePlayer(player);
                foreach (House house in player.GetHouses())
                    house.Save();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Exception during periodic saving of player " + player.GetName());
            }
        }
    }
}

/// <summary>Java parity: services/player/ItemUpdateTask (package-private, implements Runnable). Periodic save of inventory items and their stones.</summary>
internal class ItemUpdateTask : Runnable
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ItemUpdateTask));
    private readonly int playerId;

    internal ItemUpdateTask(int playerId)
    {
        this.playerId = playerId;
    }

    public void Run()
    {
        Player player = World.GetInstance().GetPlayer(playerId);
        if (player != null)
        {
            try
            {
                InventoryDAO.Store(player);
                ItemStoneListDAO.Save(player);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Exception during periodic saving of player items " + player.GetName());
            }
        }
    }
}
