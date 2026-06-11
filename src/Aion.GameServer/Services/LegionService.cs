using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.ConquerorAndProtectorSystem;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.Collections;
using Aion.GameServer.Utils.Idfactory;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/LegionService (Simple, cura, Source, Neon) — load/store legions and members; create/disband/recreate/invite/kick/leave, rank/permission/emblem/announcement/level/warehouse/history flows. Singleton (SingletonHolder); legionsById/legionMemberById ConcurrentDictionary; 5 anonymous RequestResponseHandler subclasses→nested classes (access outer privates); inner LegionRestrictions→nested class; computeIfAbsent no-null-store→TryGetValue+GetOrAdd guard; ByteBuffer emblem chunking→manual index over byte[]; LegionRank.values()[id]→Enum.GetValues cast; switch-expr msgId; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; new Timestamp→DateTimeOffset.FromUnixTimeMilliseconds; removeIf→RemoveAll; streams→LINQ; Pattern.matcher().matches()→IsMatch. DAO/Legion/packets/RequestResponseHandler red-tolerated.</summary>
public class LegionService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(LegionService));
    private readonly ConcurrentDictionary<int, Legion> legionsById = new ConcurrentDictionary<int, Legion>();
    private readonly ConcurrentDictionary<int, LegionMember> legionMemberById = new ConcurrentDictionary<int, LegionMember>();
    private const int MAX_LEGION_LEVEL = 8;

    private LegionRestrictions legionRestrictions = new LegionRestrictions();

    public static LegionService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private LegionService()
    {
    }

    private void StoreLegion(Legion legion, bool newLegion)
    {
        if (newLegion)
        {
            AddCachedLegion(legion);
            LegionDAO.SaveNewLegion(legion);
        }
        else
        {
            LegionDAO.StoreLegion(legion);
            LegionDAO.StoreLegionEmblem(legion.GetLegionId(), legion.GetLegionEmblem());
        }
    }

    private void StoreLegion(Legion legion)
    {
        StoreLegion(legion, false);
    }

    public void StoreLegionMember(LegionMember legionMember)
    {
        LegionMemberDAO.StoreLegionMember(legionMember);
    }

    public ICollection<Legion> GetCachedLegions()
    {
        return legionsById.Values;
    }

    private void AddCachedLegion(Legion legion)
    {
        legionsById[legion.GetLegionId()] = legion;
    }

    public static void DeleteLegionFromDB(int legionId)
    {
        LegionDAO.DeleteLegion(legionId);
        InventoryDAO.DeletePlayerOrLegionItems(legionId);
    }

    /// <summary>This method will remove the legion member from cache and the database</summary>
    private void DeleteLegionMemberFromDB(LegionMember legionMember)
    {
        legionMemberById.TryRemove(legionMember.GetObjectId(), out _);
        LegionMemberDAO.DeleteLegionMember(legionMember.GetObjectId());
        Legion legion = legionMember.GetLegion();
        legion.RemoveMember(legionMember.GetObjectId());
        AddHistory(legion, legionMember.GetName(), LegionHistoryAction.KICK);
    }

    public Legion GetLegion(string legionName)
    {
        Legion legion = legionsById.Values.Where(l => l.GetName().Equals(legionName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        if (legion == null)
        {
            legion = LegionDAO.LoadLegion(legionName);
            if (legion == null || CheckDisband(legion))
                return null;
            LoadLegionInfo(legion);
            AddCachedLegion(legion);
        }
        else if (CheckDisband(legion))
        {
            return null;
        }
        return legion;
    }

    public Legion GetLegion(int legionId)
    {
        Legion legion = legionsById.GetValueOrDefault(legionId);
        if (legion == null)
        {
            legion = LegionDAO.LoadLegion(legionId);
            if (legion == null || CheckDisband(legion))
                return null;
            LoadLegionInfo(legion);
            AddCachedLegion(legion);
        }
        else if (CheckDisband(legion))
        {
            return null;
        }
        return legion;
    }

    private void LoadLegionInfo(Legion legion)
    {
        legion.SetMemberIds(LegionMemberDAO.LoadLegionMembers(legion.GetLegionId()));
        legion.SetAnnouncement(LegionDAO.LoadAnnouncement(legion.GetLegionId()));
        legion.SetLegionEmblem(LegionDAO.LoadLegionEmblem(legion.GetLegionId()));
        InventoryDAO.LoadStorage(legion.GetLegionId(), legion.GetLegionWarehouse());
        ItemStoneListDAO.Load(legion.GetLegionWarehouse().GetItems());
        LegionDAO.LoadHistory(legion);
    }

    private LegionMember GetLegionMember(string name)
    {
        PlayerCommonData playerCommonData = PlayerService.GetOrLoadPlayerCommonData(name);
        return playerCommonData == null ? null : GetLegionMember(playerCommonData);
    }

    public LegionMember GetLegionMember(int playerObjId)
    {
        return GetLegionMember(playerObjId, null);
    }

    public LegionMember GetLegionMember(PlayerCommonData playerCommonData)
    {
        return GetLegionMember(playerCommonData.GetPlayerObjId(), playerCommonData);
    }

    private LegionMember GetLegionMember(int playerObjectId, PlayerCommonData playerCommonData)
    {
        // Java computeIfAbsent: does not store a null mapping
        LegionMember legionMember = legionMemberById.GetValueOrDefault(playerObjectId);
        if (legionMember == null)
        {
            LegionMember lm = LegionMemberDAO.LoadLegionMember(playerObjectId);
            if (lm != null)
            {
                lm.SetPlayerData(playerCommonData == null ? PlayerService.GetOrLoadPlayerCommonData(playerObjectId) : playerCommonData);
                legionMember = legionMemberById.GetOrAdd(playerObjectId, lm);
            }
        }
        return legionMember == null || CheckDisband(legionMember.GetLegion()) ? null : legionMember;
    }

    /// <summary>Method that checks if a legion is disbanding. Returns true if it's time to be deleted.</summary>
    private bool CheckDisband(Legion legion)
    {
        if (legion.IsDisbanding())
        {
            if ((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) > legion.GetDisbandTime())
            {
                DisbandLegion(legion);
                return true;
            }
        }
        return false;
    }

    /// <summary>This method will disband a legion and update all members</summary>
    public void DisbandLegion(Legion legion)
    {
        legionsById.TryRemove(legion.GetLegionId(), out _);
        foreach (int id in legion.GetMemberIds())
            legionMemberById.TryRemove(id, out _);
        SiegeService.GetInstance().CleanLegionId(legion.GetLegionId());
        DeleteLegionFromDB(legion.GetLegionId());
        UpdateAfterDisbandLegion(legion);
    }

    public void RequestDisbandLegion(Npc npc, Player activePlayer)
    {
        if (legionRestrictions.CanDisbandLegion(activePlayer))
        {
            RequestResponseHandler<Npc> disbandResponseHandler = new DisbandResponseHandler(this, npc);

            bool disbandResult = activePlayer.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_GUILD_DISPERSE_STAYMODE, disbandResponseHandler);
            if (disbandResult)
            {
                PacketSendUtility.SendPacket(activePlayer, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_GUILD_DISPERSE_STAYMODE, 0, 0));
            }
        }
    }

    public void CreateLegion(Player activePlayer, string legionName)
    {
        if (legionRestrictions.CanCreateLegion(activePlayer, legionName))
        {
            Legion legion = new Legion(IDFactory.GetInstance().NextId(), legionName);
            legion.AddLegionMember(activePlayer.GetObjectId());

            activePlayer.GetInventory().DecreaseKinah(LegionConfig.LEGION_CREATE_REQUIRED_KINAH);

            StoreLegion(legion, true);
            AddLegionMember(legion, activePlayer, LegionRank.BRIGADE_GENERAL);
            AddHistory(legion, "", LegionHistoryAction.CREATE);
            AddHistory(legion, activePlayer.GetName(), LegionHistoryAction.JOIN);

            PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CREATED(legion.GetName()));
        }
    }

    public bool AddToLegion(Legion legion, Player invited, Player inviter)
    {
        int playerObjId = invited.GetObjectId();
        if (legion.AddLegionMember(playerObjId))
        {
            // Bind LegionMember to Player
            AddLegionMember(legion, invited);

            // Display current announcement
            DisplayLegionAnnouncement(invited, legion.GetAnnouncement());

            // Add to history of legion
            AddHistory(legion, invited.GetName(), LegionHistoryAction.JOIN);
            return true;
        }
        PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_CAN_NOT_ADD_MEMBER_ANY_MORE());
        return false;
    }

    public void InvitePlayerToLegion(Player activePlayer, string targetName)
    {
        Player targetPlayer = World.World.GetInstance().GetPlayer(targetName);
        if (legionRestrictions.CanInvitePlayer(activePlayer, targetPlayer))
        {
            Legion legion = activePlayer.GetLegion();
            RequestResponseHandler<Player> responseHandler = new InviteResponseHandler(this, activePlayer, legion);

            bool requested = targetPlayer.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_GUILD_INVITE_DO_YOU_ACCEPT_INVITATION,
                responseHandler);
            // If the player is busy and could not be asked
            if (!requested)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_OTHER_IS_BUSY());
            }
            else
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_SENT_INVITE_MSG_TO_HIM(targetPlayer.GetName()));

                // Send question packet to buddy
                PacketSendUtility.SendPacket(targetPlayer, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_GUILD_INVITE_DO_YOU_ACCEPT_INVITATION, 0, 0,
                    legion.GetName(), legion.GetLegionLevel() + "", activePlayer.GetName()));
            }
        }
    }

    /// <summary>Displays current legion announcement</summary>
    private void DisplayLegionAnnouncement(Player targetPlayer, Legion.Announcement announcement)
    {
        if (announcement != null)
            PacketSendUtility.SendPacket(targetPlayer, SM_SYSTEM_MESSAGE.STR_GUILD_NOTICE(announcement.Message(), announcement.Time().ToUnixTimeMilliseconds() / 1000));
    }

    public void StartBrigadeGeneralChangeProcess(Player legionLeader, string memberName)
    {
        Player newLegionLeader = World.World.GetInstance().GetPlayer(memberName);
        if (newLegionLeader == null)
        {
            PacketSendUtility.SendPacket(legionLeader, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MASTER_NO_SUCH_USER());
            return;
        }
        RequestResponseHandler<Player> responseHandler = new AppointGeneralStartHandler(this, newLegionLeader);
        bool requested = legionLeader.GetResponseRequester().PutRequest(904979, responseHandler);
        if (requested)
        {
            PacketSendUtility.SendPacket(legionLeader, new SM_QUESTION_WINDOW(904979, 0, 0, newLegionLeader.GetName()));
        }
    }

    private void AppointBrigadeGeneral(Player activePlayer, Player targetPlayer)
    {
        if (legionRestrictions.CanAppointBrigadeGeneral(activePlayer, targetPlayer))
        {
            RequestResponseHandler<Player> responseHandler = new AppointGeneralConfirmHandler(this, activePlayer);

            bool requested = targetPlayer.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_GUILD_CHANGE_MASTER_DO_YOU_ACCEPT_OFFER,
                responseHandler);
            // If the player is busy and could not be asked
            if (!requested)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MASTER_SENT_CANT_OFFER_WHEN_HE_IS_QUESTION_ASKED());
            }
            else
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MASTER_SENT_OFFER_MSG_TO_HIM(targetPlayer.GetName()));

                // Send question packet to buddy
                // TODO: Add char name parameter? Doesn't work?
                PacketSendUtility.SendPacket(targetPlayer, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_GUILD_CHANGE_MASTER_DO_YOU_ACCEPT_OFFER,
                    activePlayer.GetObjectId(), 0, activePlayer.GetName()));
            }
        }
    }

    public void AppointBrigadeGeneral(LegionMember member)
    {
        if (member.IsBrigadeGeneral())
            return;
        Legion legion = member.GetLegion();
        LegionMember prevBrigadeGeneral = legion.GetBrigadeGeneral();
        prevBrigadeGeneral.SetRank(LegionRank.CENTURION);
        if (!prevBrigadeGeneral.IsOnline())
            LegionMemberDAO.StoreLegionMember(prevBrigadeGeneral);
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_UPDATE_MEMBER(prevBrigadeGeneral));
        member.SetRank(LegionRank.BRIGADE_GENERAL);
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_UPDATE_MEMBER(member, 1300273, member.GetName()));
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_EDIT(0x08));
        AddHistory(legion, member.GetName(), LegionHistoryAction.APPOINTED);
    }

    /// <summary>This method will handle the process when a member is demoted or promoted.</summary>
    public void AppointRank(Player player, string charName, int rankId)
    {
        LegionMember legionMember = GetLegionMember(charName);
        if (legionRestrictions.CanAppointRank(player, legionMember))
        {
            LegionRank rank = ((LegionRank[])Enum.GetValues(typeof(LegionRank)))[rankId];
            int msgId = rank switch
            {
                LegionRank.DEPUTY => 1400902,
                LegionRank.LEGIONARY => 1300268,
                LegionRank.CENTURION => 1300267,
                LegionRank.VOLUNTEER => 1400903,
                _ => 0,
            };
            legionMember.SetRank(rank);
            if (!legionMember.IsOnline())
                LegionMemberDAO.StoreLegionMember(legionMember);
            PacketSendUtility.BroadcastToLegion(legionMember.GetLegion(), new SM_LEGION_UPDATE_MEMBER(legionMember, msgId, legionMember.GetName()));
        }
    }

    public void ChangeSelfIntro(Player activePlayer, string newSelfIntro)
    {
        if (legionRestrictions.CanChangeSelfIntro(activePlayer, newSelfIntro))
        {
            LegionMember legionMember = activePlayer.GetLegionMember();
            legionMember.SetSelfIntro(newSelfIntro);
            PacketSendUtility.BroadcastToLegion(legionMember.GetLegion(), new SM_LEGION_UPDATE_SELF_INTRO(activePlayer.GetObjectId(), newSelfIntro));
            PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_WRITE_INTRO_DONE());
        }
    }

    public void ChangePermissions(Player player, short deputyPermission, short centurionPermission, short legionarPermission,
        short volunteerPermission)
    {
        LegionMember legionMember = player.GetLegionMember();
        if (legionMember == null || !legionMember.IsBrigadeGeneral())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_RIGHT_DONT_HAVE_RIGHT());
            return;
        }
        legionMember.GetLegion().SetLegionPermissions(deputyPermission, centurionPermission, legionarPermission, volunteerPermission);
        PacketSendUtility.BroadcastToLegion(legionMember.GetLegion(), new SM_LEGION_EDIT(0x02, legionMember.GetLegion()));
    }

    /// <summary>This method will handle the leveling up of a legion</summary>
    public void RequestChangeLevel(Player activePlayer)
    {
        if (legionRestrictions.CanChangeLevel(activePlayer))
        {
            Legion legion = activePlayer.GetLegion();
            activePlayer.GetInventory().DecreaseKinah(legion.GetKinahPrice());
            ChangeLevel(legion, legion.GetLegionLevel() + 1, false);
            AddHistory(legion, legion.GetLegionLevel() + "", LegionHistoryAction.LEVEL_UP);
        }
    }

    /// <summary>This method will change the legion level and send update to online members</summary>
    public void ChangeLevel(Legion legion, int newLevel, bool save)
    {
        legion.SetLegionLevel(newLevel);
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_EDIT(0x00, legion));
        PacketSendUtility.BroadcastToLegion(legion, SM_SYSTEM_MESSAGE.STR_GUILD_EVENT_LEVELUP(newLevel));
        if (save)
            StoreLegion(legion);
    }

    public void ChangeNickname(Player activePlayer, string memberName, string newNickname)
    {
        LegionMember legionMember = GetLegionMember(memberName);
        if (legionRestrictions.CanChangeNickname(activePlayer, legionMember, memberName, newNickname))
        {
            legionMember.SetNickname(newNickname);
            PacketSendUtility.BroadcastToLegion(legionMember.GetLegion(), new SM_LEGION_UPDATE_NICKNAME(legionMember.GetObjectId(), newNickname));
            if (!legionMember.IsOnline())
                LegionMemberDAO.StoreLegionMember(legionMember);
        }
    }

    /// <summary>This method will remove legion from all legion members online after a legion has been disbanded</summary>
    private void UpdateAfterDisbandLegion(Legion legion)
    {
        foreach (Player onlineLegionMember in legion.GetOnlinePlayers())
        {
            PacketSendUtility.BroadcastPacket(onlineLegionMember,
                new SM_LEGION_UPDATE_TITLE(onlineLegionMember.GetObjectId(), 0, "", onlineLegionMember.GetLegionMember().GetRank()), true);
            PacketSendUtility.SendPacket(onlineLegionMember, new SM_LEGION_LEAVE_MEMBER(1300302, 0, legion.GetName()));
            onlineLegionMember.ResetLegionMember();
            ConquerorAndProtectorService.GetInstance().OnLeaveLegion(onlineLegionMember);
        }
    }

    private void UpdateMembersEmblem(Legion legion)
    {
        LegionEmblem legionEmblem = legion.GetLegionEmblem();
        foreach (Player onlineLegionMember in legion.GetOnlinePlayers())
        {
            PacketSendUtility.BroadcastPacket(onlineLegionMember, new SM_LEGION_UPDATE_EMBLEM(legion.GetLegionId(), legionEmblem), true);
            if (legionEmblem.GetEmblemType() == LegionEmblemType.CUSTOM)
                SendEmblemData(onlineLegionMember, legionEmblem, legion.GetLegionId(), legion.GetName());
        }
    }

    /// <summary>This method will send a packet to every legion member and update them about the disbanding</summary>
    private void UpdateMembersOfDisbandLegion(Legion legion, int unixTime)
    {
        foreach (Player onlineLegionMember in legion.GetOnlinePlayers())
        {
            PacketSendUtility.SendPacket(onlineLegionMember, new SM_LEGION_UPDATE_MEMBER(onlineLegionMember, 1300303, unixTime + ""));
            PacketSendUtility.SendPacket(onlineLegionMember, new SM_LEGION_EDIT(0x06, unixTime));
        }
    }

    /// <summary>This method will send a packet to every legion member and update them about the recreation</summary>
    private void UpdateMembersOfRecreateLegion(Legion legion)
    {
        foreach (Player onlineLegionMember in legion.GetOnlinePlayers())
        {
            PacketSendUtility.SendPacket(onlineLegionMember, new SM_LEGION_UPDATE_MEMBER(onlineLegionMember, 1300307, ""));
            PacketSendUtility.SendPacket(onlineLegionMember, new SM_LEGION_EDIT(0x07));
        }
    }

    public void StoreLegionEmblem(Player activePlayer, int emblemId, int color_a, int color_r, int color_g, int color_b, LegionEmblemType emblemType)
    {
        if (legionRestrictions.CanStoreLegionEmblem(activePlayer, emblemId))
        {
            Legion legion = activePlayer.GetLegion();
            AddHistory(legion, "", LegionHistoryAction.EMBLEM_MODIFIED);
            activePlayer.GetInventory().DecreaseKinah(PricesService.GetPriceForService(LegionConfig.LEGION_EMBLEM_REQUIRED_KINAH, activePlayer.GetRace()));
            legion.GetLegionEmblem().SetEmblem(emblemId, color_a, color_r, color_g, color_b, emblemType, null);
            UpdateMembersEmblem(legion);
            PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_EMBLEM());
        }
    }

    public void OpenLegionWarehouse(Player player, Npc npc)
    {
        if (legionRestrictions.CanOpenWarehouse(player, npc))
        {
            LegionWhUpdate(player);
            PacketSendUtility.SendPacket(player, new SM_LEGION_EDIT(0x04, player.GetLegion())); // kinah
            int whLvl = player.GetLegion().GetWarehouseExpansions();
            List<Item> items = player.GetLegion().GetLegionWarehouse().GetItems();
            int storageId = StorageType.LEGION_WAREHOUSE.GetId();

            SplitList<Item> legionMemberSplitList = new FixedElementCountSplitList<Item>(items, false, 10);
            legionMemberSplitList
                .ForEach(part => PacketSendUtility.SendPacket(player, new SM_WAREHOUSE_INFO(part, storageId, whLvl, part.IsFirst(), player)));
            PacketSendUtility.SendPacket(player, new SM_WAREHOUSE_INFO(null, storageId, whLvl, items.Count == 0, player));
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), DialogPage.LEGION_WAREHOUSE.Id()));
        }
    }

    public void RecreateLegion(Npc npc, Player activePlayer)
    {
        if (legionRestrictions.CanRecreateLegion(activePlayer))
        {
            RequestResponseHandler<Npc> disbandResponseHandler = new RecreateResponseHandler(this, npc);

            bool disbandResult = activePlayer.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_GUILD_DISPERSE_STAYMODE_CANCEL,
                disbandResponseHandler);
            if (disbandResult)
            {
                PacketSendUtility.SendPacket(activePlayer, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_GUILD_DISPERSE_STAYMODE_CANCEL, 0, 0));
            }
        }
    }

    public void LegionWhUpdate(Player player)
    {
        Legion legion = player.GetLegion();

        if (legion == null)
            return;

        List<Item> allItems = legion.GetLegionWarehouse().GetItemsWithKinah();
        allItems.AddRange(legion.GetLegionWarehouse().GetDeletedItems());
        try
        {
            InventoryDAO.Store(allItems, player.GetObjectId(), player.GetAccount().GetId(), legion.GetLegionId());
            ItemStoneListDAO.Save(allItems);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Exception during periodic saving of legion WH");
        }
    }

    /// <summary>This method will update all players about the level/class/map/online change</summary>
    public void UpdateMemberInfo(Player player)
    {
        LegionMember legionMember = player.GetLegionMember();
        legionMember.SetPlayerData(player);
        PacketSendUtility.BroadcastToLegion(player.GetLegion(), new SM_LEGION_UPDATE_MEMBER(legionMember));
    }

    /// <summary>This method will set the contribution points, specially for legion command</summary>
    public void SetContributionPoints(Legion legion, long newPoints, bool save)
    {
        legion.SetContributionPoints(newPoints);
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_EDIT(0x03, legion));
        if (save)
            StoreLegion(legion);
    }

    public void UploadEmblemInfo(Player activePlayer, int totalSize, int color_a, int color_r, int color_g, int color_b, LegionEmblemType emblemType)
    {
        LegionEmblem legionEmblem = activePlayer.GetLegion().GetLegionEmblem();
        if (legionRestrictions.CanUploadEmblem(activePlayer, true))
        {
            legionEmblem.ResetUploadSettings();
            legionEmblem.SetEmblem(legionEmblem.GetEmblemId(), color_a, color_r, color_g, color_b, emblemType, null);
            legionEmblem.SetUploadSize(totalSize);
            legionEmblem.SetUploading(true);
        }
        else
        {
            legionEmblem.ResetUploadSettings();
        }
    }

    public void UploadEmblemData(Player activePlayer, int size, byte[] data)
    {
        LegionEmblem legionEmblem = activePlayer.GetLegion().GetLegionEmblem();
        if (legionRestrictions.CanUploadEmblem(activePlayer, false))
        {
            legionEmblem.AddUploadedSize(size);
            legionEmblem.AddUploadData(data);

            if (legionEmblem.GetUploadedSize() >= legionEmblem.GetUploadSize())
            {
                if (legionEmblem.GetUploadedSize() == 0 || legionEmblem.GetUploadedSize() > legionEmblem.GetUploadSize())
                {
                    PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_WARN_CORRUPT_EMBLEM_FILE());
                    return;
                }
                activePlayer.GetInventory()
                    .DecreaseKinah(PricesService.GetPriceForService(LegionConfig.LEGION_EMBLEM_REQUIRED_KINAH, activePlayer.GetRace()));
                // Finished
                legionEmblem.SetCustomEmblemData(legionEmblem.GetUploadData());
                LegionDAO.StoreLegionEmblem(activePlayer.GetLegion().GetLegionId(), legionEmblem);
                AddHistory(activePlayer.GetLegion(), "", LegionHistoryAction.EMBLEM_REGISTER);
                UpdateMembersEmblem(activePlayer.GetLegion());
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_WARN_SUCCESS_UPLOAD_EMBLEM());
                legionEmblem.ResetUploadSettings();
            }
        }
        else
        {
            PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_WARN_FAILURE_UPLOAD_EMBLEM());
            legionEmblem.ResetUploadSettings();
        }
    }

    public void SendEmblemData(Player player, LegionEmblem legionEmblem, int legionId, string legionName)
    {
        int dataLength = legionEmblem.GetCustomEmblemData() == null ? 0 : legionEmblem.GetCustomEmblemData().Length;
        PacketSendUtility.SendPacket(player, new SM_LEGION_SEND_EMBLEM(legionId, legionEmblem, dataLength, legionName));
        if (dataLength > 0)
        {
            // ByteBuffer.allocate/put/get/position/capacity → manual index over the byte[]
            byte[] emblemData = legionEmblem.GetCustomEmblemData();
            int position = 0;
            int capacity = dataLength;
            log.LogDebug("legionEmblem size: " + capacity + " bytes");
            int maxSize = 7993;
            int currentSize;
            byte[] bytes;
            do
            {
                log.LogDebug("legionEmblem data position: " + position);
                currentSize = capacity - position;
                log.LogDebug("legionEmblem data remaining capacity: " + currentSize + " bytes");

                if (currentSize >= maxSize)
                {
                    bytes = new byte[maxSize];
                    for (int i = 0; i < maxSize; i++)
                    {
                        bytes[i] = emblemData[position++];
                    }
                    log.LogDebug("legionEmblem data send size: " + (bytes.Length) + " bytes");
                    PacketSendUtility.SendPacket(player, new SM_LEGION_SEND_EMBLEM_DATA(maxSize, bytes));
                }
                else
                {
                    bytes = new byte[currentSize];
                    for (int i = 0; i < currentSize; i++)
                    {
                        bytes[i] = emblemData[position++];
                    }
                    log.LogDebug("legionEmblem data send size: " + (bytes.Length) + " bytes");
                    PacketSendUtility.SendPacket(player, new SM_LEGION_SEND_EMBLEM_DATA(currentSize, bytes));
                }
            } while (capacity != position);
        }
    }

    /// <summary>This will add a new announcement to the DB and change the current announcement</summary>
    public void ChangeAnnouncement(Player activePlayer, string message)
    {
        if (!activePlayer.GetLegionMember().HasRights(LegionPermissionsMask.EDIT))
        {
            PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_WRITE_NOTICE_DONT_HAVE_RIGHT());
            return;
        }
        Legion legion = activePlayer.GetLegion();
        Legion.Announcement announcement = null;
        if (!(message.Length == 0))
        {
            if (message.Length > 256)
            {
                log.LogWarning("Truncated legion announcement sent by " + activePlayer + " (old length: " + message.Length + ")");
                message = message.Substring(0, 256);
            }
            announcement = new Legion.Announcement(message, DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        }
        legion.SetAnnouncement(announcement);
        LegionDAO.SaveAnnouncement(legion.GetLegionId(), announcement);
        if (announcement == null)
        {
            PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_MSG_CLEAR_GUILD_NOTICE());
            PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_INFO(legion), activePlayer.GetObjectId());
        }
        else
        {
            PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_WRITE_NOTICE_DONE());
            PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_EDIT(announcement));
        }
    }

    private void AddHistory(Legion legion, string text, LegionHistoryAction action)
    {
        AddHistory(legion, text, action, "");
    }

    public void AddRewardHistory(Legion legion, long kinahAmount, LegionHistoryAction action, int fortressId)
    {
        AddHistory(legion, kinahAmount.ToString(), action, fortressId.ToString());
    }

    /// <summary>This method will add a new history for a legion. name: in case of reward = kinah amount; description: in case of reward = fortress id.</summary>
    public void AddHistory(Legion legion, string name, LegionHistoryAction action, string description)
    {
        LegionHistoryEntry historyEntry = LegionDAO.InsertHistory(legion.GetLegionId(), action, name, description);
        List<LegionHistoryEntry> removedEntries = legion.AddHistory(historyEntry);
        LegionDAO.DeleteHistory(legion.GetLegionId(), removedEntries);
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_HISTORY(legion.GetHistory(action.GetType_()), action.GetType_()));
    }

    /// <summary>This method will add a new legion member to a legion with VOLUNTEER rank</summary>
    private void AddLegionMember(Legion legion, Player player)
    {
        AddLegionMember(legion, player, LegionRank.VOLUNTEER);
    }

    private void AddLegionMember(Legion legion, Player player, LegionRank rank)
    {
        // Set legion member of player and save in the database
        player.SetLegionMember(new LegionMember(player.GetObjectId(), legion));
        player.GetLegionMember().SetPlayerData(player);
        player.GetLegionMember().SetRank(rank);
        LegionMemberDAO.SaveNewLegionMember(player.GetLegionMember());
        legionMemberById[player.GetObjectId()] = player.GetLegionMember();

        // Send the new legion member the required legion packets
        PacketSendUtility.SendPacket(player, new SM_LEGION_INFO(legion));
        // do not include invited player in member list since he will be added via SM_LEGION_ADD_MEMBER
        UpdateLegionMemberList(player, false, player.GetObjectId());

        // Send legion member info to the members
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_ADD_MEMBER(player, false, 1300260, player.GetName()));
        // Send legion emblem information
        LegionEmblem legionEmblem = legion.GetLegionEmblem();
        PacketSendUtility.BroadcastPacket(player, new SM_LEGION_UPDATE_EMBLEM(legion.GetLegionId(), legionEmblem), true);

        // Send legion edit
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_EDIT(0x08));

        // Update legion member's appearance in game
        PacketSendUtility.BroadcastPacket(player,
            new SM_LEGION_UPDATE_TITLE(player.GetObjectId(), legion.GetLegionId(), legion.GetName(), player.GetLegionMember().GetRank()), true);
        legion.AddBonus();
    }

    private bool RemoveLegionMember(Player player)
    {
        return RemoveLegionMember(player.GetLegionMember(), null);
    }

    private bool RemoveLegionMember(LegionMember legionMember, string kickerName)
    {
        if (legionMember == null)
            return false;
        // Delete legion member from database and cache
        DeleteLegionMemberFromDB(legionMember);

        Legion legion = legionMember.GetLegion();
        legion.GetLegionWarehouse().UnsetInUse(legionMember.GetObjectId());

        if (kickerName != null)
        {
            PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_LEAVE_MEMBER(1300247, legionMember.GetObjectId(), kickerName, legionMember.GetName()),
                legionMember.GetObjectId());
        }
        else
        {
            PacketSendUtility.BroadcastToLegion(legion,
                new SM_LEGION_LEAVE_MEMBER(1300240, legionMember.GetObjectId(), legionMember.GetName(), legion.GetName()), legionMember.GetObjectId());
        }
        Player player = World.World.GetInstance().GetPlayer(legionMember.GetObjectId());
        if (player != null)
        {
            PacketSendUtility.SendPacket(player, new SM_LEGION_LEAVE_MEMBER(kickerName != null ? 1300246 : 1300241, 0, legion.GetName()));
            PacketSendUtility.BroadcastPacket(player, new SM_LEGION_UPDATE_TITLE(player.GetObjectId(), 0, "", legionMember.GetRank()), true);
            if (legion.HasBonus())
                PacketSendUtility.SendPacket(player, new SM_ICON_INFO(1, false));
            player.ResetLegionMember();
            ConquerorAndProtectorService.GetInstance().OnLeaveLegion(player);
        }
        legion.RemoveBonus();
        return true;
    }

    public void KickMember(Player player, string memberName)
    {
        LegionMember legionMember = GetLegionMember(memberName);
        if (legionRestrictions.CanKickPlayer(player, memberName, legionMember))
            RemoveLegionMember(legionMember, player.GetName());
    }

    public bool LeaveLegion(Player player, bool skipChecks)
    {
        if (skipChecks || legionRestrictions.CanLeave(player))
            return RemoveLegionMember(player);
        return false;
    }

    public void OnLogin(Player activePlayer)
    {
        Legion legion = activePlayer.GetLegion();

        // Tell all legion members player has come online
        LegionService.GetInstance().UpdateMemberInfo(activePlayer);

        // Notify legion members player has logged in
        PacketSendUtility.BroadcastToLegion(legion, SM_SYSTEM_MESSAGE.STR_MSG_NOTIFY_LOGIN_GUILD(activePlayer.GetName()), activePlayer.GetObjectId());

        // Send member add to player
        PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_ADD_MEMBER(activePlayer, true, 0, ""));

        // Send legion info packets
        PacketSendUtility.SendPacket(activePlayer, new SM_LEGION_INFO(legion));
        UpdateLegionMemberList(activePlayer, false);

        // Send current announcement to player
        DisplayLegionAnnouncement(activePlayer, legion.GetAnnouncement());

        if (legion.IsDisbanding())
            PacketSendUtility.SendPacket(activePlayer, new SM_LEGION_EDIT(0x06, legion.GetDisbandTime()));

        if (legion.HasBonus())
        {
            PacketSendUtility.SendPacket(activePlayer, new SM_ICON_INFO(1, true));
        }
        else
        {
            legion.AddBonus();
        }
    }

    public void OnLogout(Player player)
    {
        LegionMember legionMember = player.GetLegionMember();
        Legion legion = legionMember.GetLegion();
        legion.GetLegionWarehouse().UnsetInUse(player.GetObjectId());
        UpdateMemberInfo(player);
        StoreLegion(legion);
        StoreLegionMember(player.GetLegionMember());
        legion.RemoveBonus();
    }

    /// <summary>This class contains all restrictions for legion features (Simple).</summary>
    private class LegionRestrictions
    {
        private const int MIN_EMBLEM_ID = 0;
        private const int MAX_EMBLEM_ID = 49;

        public bool CanCreateLegion(Player activePlayer, string legionName)
        {
            /* Some reasons why legions can' be created */
            if (!NameRestrictionService.IsValidLegionName(legionName) || NameRestrictionService.IsForbidden(legionName))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CREATE_INVALID_GUILD_NAME());
                return false;
            } // STR_GUILD_CREATE_TOO_FAR_FROM_CREATOR_NPC TODO
            else if (!IsFreeName(legionName))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CREATE_SAME_GUILD_EXIST());
                return false;
            }
            else if (activePlayer.IsLegionMember())
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CREATE_ALREADY_BELONGS_TO_GUILD());
                return false;
            }
            else if (activePlayer.GetInventory().GetKinah() < LegionConfig.LEGION_CREATE_REQUIRED_KINAH)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CREATE_NOT_ENOUGH_MONEY());
                return false;
            }
            return true;
        }

        public bool CanInvitePlayer(Player activePlayer, Player targetPlayer)
        {
            Legion legion = activePlayer.GetLegion();
            if (targetPlayer == null)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_NO_USER_TO_INVITE());
                return false;
            }
            else if (targetPlayer.GetPlayerSettings().IsInDeniedStatus(DeniedStatus.GUILD))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_MSG_REJECTED_INVITE_GUILD(targetPlayer.GetName()));
                return false;
            }
            else if (activePlayer.IsDead())
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_CANT_INVITE_WHEN_DEAD());
                return false;
            }
            else if (activePlayer.Equals(targetPlayer))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_CAN_NOT_INVITE_SELF());
                return false;
            }
            else if (targetPlayer.IsLegionMember())
            {
                if (legion.IsMember(targetPlayer.GetObjectId()))
                {
                    PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_HE_IS_MY_GUILD_MEMBER(targetPlayer.GetName()));
                }
                else
                {
                    PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_HE_IS_OTHER_GUILD_MEMBER(targetPlayer.GetName()));
                }
                return false;
            }
            else if (!activePlayer.GetLegionMember().HasRights(LegionPermissionsMask.INVITE))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_DONT_HAVE_RIGHT_TO_INVITE());
                return false;
            }
            else if (activePlayer.GetRace() != targetPlayer.GetRace() && !LegionConfig.LEGION_INVITEOTHERFACTION)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_CAN_NOT_INVITE_OTHER_RACE());
                return false;
            }
            return true;
        }

        public bool CanKickPlayer(Player player, string charName, LegionMember legionMember)
        {
            Legion legion = player.GetLegion();
            if (legion == null)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_BANISH_I_AM_NOT_BELONG_TO_GUILD());
                return false;
            }
            else if (legionMember == null || !legion.IsMember(legionMember.GetObjectId()))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_BANISH_HE_IS_NOT_MY_GUILD_MEMBER(charName));
                return false;
            }
            else if (player.GetObjectId() == legionMember.GetObjectId())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_BANISH_CANT_BANISH_SELF());
                return false;
            }
            else if (legionMember.IsBrigadeGeneral())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_BANISH_CAN_BANISH_MASTER());
                return false;
            }
            else if (legionMember.GetRank().GetRankId() <= player.GetLegionMember().GetRank().GetRankId())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_BANISH_CAN_NOT_BANISH_SAME_MEMBER_RANK());
                return false;
            }
            else if (!player.GetLegionMember().HasRights(LegionPermissionsMask.KICK))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_BANISH_DONT_HAVE_RIGHT_TO_BANISH());
                return false;
            }
            return true;
        }

        public bool CanAppointBrigadeGeneral(Player activePlayer, Player targetPlayer)
        {
            Legion legion = activePlayer.GetLegion();
            if (!IsBrigadeGeneral(activePlayer))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MASTER_DONT_HAVE_RIGHT());
                return false;
            }
            else if (activePlayer.Equals(targetPlayer))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MASTER_ERROR_SELF());
                return false;
            }
            else if (!legion.IsMember(targetPlayer.GetObjectId()))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MASTER_NOT_MY_GUILD_MEMBER(targetPlayer.GetName()));
                return false;
            }
            return true;
        }

        public bool CanAppointRank(Player activePlayer, LegionMember targetMember)
        {
            Legion legion = activePlayer.GetLegion();
            if (legion == null)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MEMBER_RANK_I_AM_NOT_BELONG_TO_GUILD());
                return false;
            }
            else if (!IsBrigadeGeneral(activePlayer))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MEMBER_RANK_DONT_HAVE_RIGHT());
                return false;
            }
            else if (targetMember == null)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MEMBER_RANK_NO_USER());
                return false;
            }
            else if (!legion.IsMember(targetMember.GetObjectId()))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MEMBER_RANK_HE_IS_NOT_MY_GUILD_MEMBER(targetMember.GetName()));
                return false;
            }
            else if (activePlayer.GetObjectId() == targetMember.GetObjectId())
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MEMBER_RANK_ERROR_SELF());
                return false;
            }
            return true;
        }

        public bool CanChangeSelfIntro(Player activePlayer, string newSelfIntro)
        {
            return IsValidSelfIntro(newSelfIntro);
        }

        public bool CanChangeLevel(Player activePlayer)
        {
            Legion legion = activePlayer.GetLegion();
            int levelContributionPrice = legion.GetContributionPrice();
            if (!activePlayer.GetLegionMember().IsBrigadeGeneral())
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_LEVEL_DONT_HAVE_RIGHT());
                return false;
            }
            if (legion.GetLegionLevel() == MAX_LEGION_LEVEL)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_LEVEL_CANT_LEVEL_UP());
                return false;
            }
            if (LegionConfig.ENABLE_GUILD_TASK_REQ && legion.GetLegionLevel() >= 5)
            {
                if (!ChallengeTaskService.GetInstance().CanRaiseLegionLevel(legion, activePlayer))
                {
                    PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_LEVEL_UP_CHALLENGE_TASK(legion.GetLegionLevel()));
                    return false;
                }
            }
            if (activePlayer.GetInventory().GetKinah() < legion.GetKinahPrice())
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_LEVEL_NOT_ENOUGH_MONEY());
                return false;
            }
            if (!legion.HasRequiredMembers())
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_LEVEL_NOT_ENOUGH_MEMBER());
                return false;
            }
            if (legion.GetContributionPoints() < levelContributionPrice)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_LEVEL_NOT_ENOUGH_POINT());
                return false;
            }
            return true;
        }

        public bool CanChangeNickname(Player player, LegionMember member, string memberName, string newNickname)
        {
            Legion legion = player.GetLegion();
            if (legion == null)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MEMBER_NICKNAME_I_AM_NOT_BELONG_TO_GUILD());
                return false;
            }
            else if (member == null || !legion.IsMember(member.GetObjectId()))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MEMBER_NICKNAME_HE_IS_NOT_MY_GUILD_MEMBER(memberName));
                return false;
            }
            else if (!player.GetLegionMember().IsBrigadeGeneral())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MEMBER_NICKNAME_DONT_HAVE_RIGHT_TO_CHANGE_NICKNAME());
                return false;
            }
            return IsValidNickname(newNickname);
        }

        public bool CanDisbandLegion(Player activePlayer)
        {
            Legion legion = activePlayer.GetLegion();
            if (legion == null)
            {
                return false;
            }
            if (legion.IsDisbanding())
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_DISPERSE_ALREADY_REQUESTED());
                return false;
            }
            else if (!IsBrigadeGeneral(activePlayer))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_DISPERSE_ONLY_MASTER_CAN_DISPERSE());
                return false;
            }
            else if (legion.GetLegionWarehouse().GetCurrentUser() != 0)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_DISPERSE_CANT_DISPERSE_GUILD_WHILE_USING_WAREHOUSE());
                return false;
            }
            else if (legion.GetLegionWarehouse().Size() > 0 || legion.GetLegionWarehouse().GetKinah() > 0)
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_DISPERSE_CANT_DISPERSE_GUILD_STORE_ITEM_IN_WAREHOUSE());
                return false;
            }
            return true;
        }

        public bool CanLeave(Player activePlayer)
        {
            if (IsBrigadeGeneral(activePlayer))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_LEAVE_MASTER_CANT_LEAVE_BEFORE_CHANGE_MASTER());
                return false;
            }
            else if (activePlayer.GetLegion().GetLegionWarehouse().GetCurrentUser() == activePlayer.GetObjectId())
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_LEAVE_CANT_LEAVE_GUILD_WHILE_USING_WAREHOUSE());
                return false;
            }
            return true;
        }

        public bool CanRecreateLegion(Player activePlayer)
        {
            if (!IsBrigadeGeneral(activePlayer))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_DISPERSE_ONLY_MASTER_CAN_DISPERSE());
                return false;
            }
            else if (!activePlayer.GetLegion().IsDisbanding())
            {
                // Legion is not disbanding
                return false;
            }
            return true;
        }

        public bool CanUploadEmblem(Player activePlayer, bool initUpload)
        {
            if (!CanStoreLegionEmblem(activePlayer, MIN_EMBLEM_ID))
            {
                return false;
            }
            else if (activePlayer.GetLegion().GetLegionLevel() < 3)
            {
                // Legion level isn't high enough
                return false;
            }
            else if (initUpload && activePlayer.GetLegion().GetLegionEmblem().IsUploading())
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_WARN_FAILURE_UPLOAD_EMBLEM());
                return false;
            }
            else if (!initUpload && !activePlayer.GetLegion().GetLegionEmblem().IsUploading())
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_WARN_FAILURE_UPLOAD_EMBLEM());
                return false;
            }
            return true;
        }

        public bool CanOpenWarehouse(Player player, Npc npc)
        {
            if (!player.IsLegionMember())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_NO_GUILD_TO_DEPOSIT());
                return false;
            }
            LegionMember lm = player.GetLegionMember();
            LegionWarehouse legWh = lm.GetLegion().GetLegionWarehouse();
            if (!LegionConfig.LEGION_WAREHOUSE || !npc.GetObjectTemplate().SupportsAction(DialogAction.OPEN_LEGION_WAREHOUSE))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANT_USE_GUILD_STORAGE());
                return false;
            }
            else if (lm.GetLegion().IsDisbanding())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_WAREHOUSE_CANT_USE_WHILE_DISPERSE());
                return false;
            }
            else if (!lm.HasRights(LegionPermissionsMask.WH_DEPOSIT) && !lm.HasRights(LegionPermissionsMask.WH_WITHDRAWAL))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_WAREHOUSE_NO_RIGHT());
                return false;
            }
            else if (!legWh.SetInUse(player.GetObjectId()) && legWh.GetCurrentUser() != player.GetObjectId())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GUILD_WAREHOUSE_IN_USE());
                return false;
            }
            return true;
        }

        public bool CanStoreLegionEmblem(Player activePlayer, int emblemId)
        {
            if (emblemId < MIN_EMBLEM_ID || emblemId > MAX_EMBLEM_ID)
            {
                // Not a valid emblemId
                return false;
            }
            else if (!IsBrigadeGeneral(activePlayer))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_EMBLEM_DONT_HAVE_RIGHT());
                return false;
            }
            else if (activePlayer.GetLegion().GetLegionLevel() < 2)
            {
                // legion level not high enough
                return false;
            }
            else if (activePlayer.GetInventory().GetKinah() < PricesService.GetPriceForService(LegionConfig.LEGION_EMBLEM_REQUIRED_KINAH,
                activePlayer.GetRace()))
            {
                PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_MSG_NOT_ENOUGH_MONEY());
                return false;
            }
            return true;
        }

        private bool IsBrigadeGeneral(Player player)
        {
            return player.GetLegionMember().IsBrigadeGeneral();
        }

        private bool IsFreeName(string name)
        {
            return !LegionDAO.IsNameUsed(name);
        }

        private bool IsValidSelfIntro(string name)
        {
            return LegionConfig.SELF_INTRO_PATTERN.IsMatch(name);
        }

        private bool IsValidNickname(string name)
        {
            return LegionConfig.NICKNAME_PATTERN.IsMatch(name);
        }
    }

    public void AddWHItemHistory(Player player, int itemId, long count, IStorage sourceStorage, IStorage destStorage)
    {
        Legion legion = player.GetLegion();
        if (legion != null)
        {
            string description = itemId + ":" + count;
            if (sourceStorage.GetStorageType() == StorageType.LEGION_WAREHOUSE)
            {
                AddHistory(legion, player.GetName(), LegionHistoryAction.ITEM_WITHDRAW, description);
            }
            else if (destStorage.GetStorageType() == StorageType.LEGION_WAREHOUSE)
            {
                AddHistory(legion, player.GetName(), LegionHistoryAction.ITEM_DEPOSIT, description);
            }
        }
    }

    private static class SingletonHolder
    {
        internal static readonly LegionService instance = new LegionService();
    }

    public void UpdateLegionMemberList(Player player, bool broadcastToLegion)
    {
        UpdateLegionMemberList(player, broadcastToLegion, null);
    }

    public void UpdateLegionMemberList(Player player, bool broadcastToLegion, int? excludedPlayerId)
    {
        if (player != null && player.GetLegion() != null)
        {
            Legion legion = player.GetLegion();
            List<LegionMember> allMembers = legion.GetMembers();
            if (excludedPlayerId != null)
                allMembers.RemoveAll(member => member.GetObjectId() == excludedPlayerId);
            SplitList<LegionMember> legionMemberSplitList = new FixedElementCountSplitList<LegionMember>(allMembers, true, 80);
            legionMemberSplitList.ForEach(part =>
            {
                if (broadcastToLegion)
                    PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_MEMBERLIST(part, part.IsFirst(), part.IsLast()));
                else
                    PacketSendUtility.SendPacket(player, new SM_LEGION_MEMBERLIST(part, part.IsFirst(), part.IsLast()));
            });
        }
    }

    public bool TryRename(Legion legion, string name, Player player, int? legionNameChangeTicketItemObjId)
    {
        if (legion.GetName().Equals(name))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_EDIT_GUILD_NAME_ERROR_SAME_YOUR_NAME());
            return false;
        }
        else if (!NameRestrictionService.IsValidLegionName(name) || NameRestrictionService.IsForbidden(name))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_EDIT_GUILD_NAME_ERROR_WRONG_INPUT());
            return false;
        }
        else if (LegionDAO.IsNameUsed(name))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_EDIT_GUILD_NAME_ALREADY_EXIST());
            return false;
        }
        else if (legion.IsDisbanding())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_EDIT_GUILD_NAME_CANT_FOR_DISPERSING_GUILD());
            return false;
        }
        else if (legionNameChangeTicketItemObjId != null)
        {
            Item item = player.GetInventory().GetItemByObjId(legionNameChangeTicketItemObjId.Value);
            if (item == null || item.GetItemId() != 169680000 && item.GetItemId() != 169680001 || !player.GetInventory().DecreaseByObjectId(
                legionNameChangeTicketItemObjId.Value, 1))
            {
                AuditLogger.Log(player, "tried to rename legion without coupon.");
                return false;
            }
        }
        string oldName = legion.GetName();
        legion.SetName(name);
        LegionDAO.StoreLegion(legion);
        AddHistory(legion, oldName, LegionHistoryAction.LEGION_RENAME, name);
        PacketSendUtility.BroadcastToWorld(new SM_RENAME(legion, oldName)); // broadcast to world to update all keeps, member's tags, etc.
        return true;
    }

    public void JoinLegionDominion(Player player, int locId)
    {
        LegionMember legionMember = player.GetLegionMember();
        if (!legionMember.IsBrigadeGeneral() && legionMember.GetRank() != LegionRank.DEPUTY)
            return;
        Legion legion = legionMember.GetLegion();
        if (legion.GetCurrentLegionDominion() > 0) // already selected
            return;
        if (LegionDominionService.GetInstance().Join(legion.GetLegionId(), locId))
        {
            legion.SetCurrentLegionDominion(locId);
            StoreLegion(legion);
            string locL10n = LegionDominionService.GetInstance().GetLegionDominionLoc(locId).GetL10n();
            PacketSendUtility.BroadcastToLegion(legion, SM_SYSTEM_MESSAGE.STR_MSG_GUILD_APPLY_DOMINION(locL10n));
            PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_INFO(legion));
        }
    }

    // ===== Nested RequestResponseHandler subclasses (Java anonymous classes) =====

    private sealed class DisbandResponseHandler : RequestResponseHandler<Npc>
    {
        private readonly LegionService outer;

        public DisbandResponseHandler(LegionService outer, Npc npc)
            : base(npc)
        {
            this.outer = outer;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            Legion legion = responder.GetLegion();
            int unixTime = (int)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) + LegionConfig.LEGION_DISBAND_TIME);
            legion.SetDisbandTime(unixTime);
            outer.UpdateMembersOfDisbandLegion(legion, unixTime);
        }
    }

    private sealed class InviteResponseHandler : RequestResponseHandler<Player>
    {
        private readonly LegionService outer;
        private readonly Legion legion;

        public InviteResponseHandler(LegionService outer, Player activePlayer, Legion legion)
            : base(activePlayer)
        {
            this.outer = outer;
            this.legion = legion;
        }

        public override void AcceptRequest(Player requester, Player responder)
        {
            outer.AddToLegion(legion, responder, requester);
        }

        public override void DenyRequest(Player requester, Player responder)
        {
            PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_GUILD_INVITE_HE_REJECTED_INVITATION(responder.GetName()));
        }
    }

    private sealed class AppointGeneralStartHandler : RequestResponseHandler<Player>
    {
        private readonly LegionService outer;

        public AppointGeneralStartHandler(LegionService outer, Player newLegionLeader)
            : base(newLegionLeader)
        {
            this.outer = outer;
        }

        public override void AcceptRequest(Player newBrigadeGeneral, Player responder)
        {
            outer.AppointBrigadeGeneral(responder, newBrigadeGeneral);
        }
    }

    private sealed class AppointGeneralConfirmHandler : RequestResponseHandler<Player>
    {
        private readonly LegionService outer;

        public AppointGeneralConfirmHandler(LegionService outer, Player activePlayer)
            : base(activePlayer)
        {
            this.outer = outer;
        }

        public override void AcceptRequest(Player requester, Player responder)
        {
            if (!responder.IsOnline())
            {
                PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MASTER_NO_SUCH_USER());
            }
            else if (!outer.legionRestrictions.CanAppointBrigadeGeneral(requester, responder))
            {
                AuditLogger.Log(requester, "possibly tried to exploit legion leadership transfer");
            }
            else
            {
                outer.AppointBrigadeGeneral(responder.GetLegionMember());
            }
        }

        public override void DenyRequest(Player requester, Player responder)
        {
            PacketSendUtility.SendPacket(requester, SM_SYSTEM_MESSAGE.STR_GUILD_CHANGE_MASTER_HE_DECLINE_YOUR_OFFER(responder.GetName()));
        }
    }

    private sealed class RecreateResponseHandler : RequestResponseHandler<Npc>
    {
        private readonly LegionService outer;

        public RecreateResponseHandler(LegionService outer, Npc npc)
            : base(npc)
        {
            this.outer = outer;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            Legion legion = responder.GetLegion();
            legion.SetDisbandTime(0);
            PacketSendUtility.BroadcastToLegion(legion, new SM_LEGION_EDIT(0x07));
            outer.UpdateMembersOfRecreateLegion(legion);
        }
    }
}
