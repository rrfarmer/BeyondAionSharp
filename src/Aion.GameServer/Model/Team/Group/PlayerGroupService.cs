using System;
using System.Threading;
using System.Collections.Concurrent;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group.Events;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services.Findgroup;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.Team.Alliance.Events;

namespace Aion.GameServer.Model.Team.Group;

/// <summary>Java parity: model/team/group/PlayerGroupService (ATracer). Static. ConcurrentHashMap→ConcurrentDictionary; AtomicBoolean.compareAndSet(false,true)→Interlocked.CompareExchange(ref int,1,0)==0; Objects.requireNonNull→ArgumentNullException; Runnable OfflinePlayerChecker→nested class w/ Run; forEachTeamMember lambda→ForEachTeamMember; .equals→.Equals. PlayerGroup/event classes/PlayerRestrictions/FindGroupService/TimeUtil/ThreadPoolManager red-tolerated.</summary>
public class PlayerGroupService
{
    private static readonly ILogger log = NullLogger.Instance;

    private static readonly ConcurrentDictionary<int, PlayerGroup> groups = new();
    private static int offlineCheckStarted;

    public static void InviteToGroup(Player inviter, Player invited)
    {
        if (PlayerRestrictions.CanInviteToGroup(inviter, invited))
        {
            PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_PARTY_INVITED_HIM(invited.GetName()));
            PlayerGroupInvite invite = new(inviter);
            if (invited.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_PARTY_DO_YOU_ACCEPT_INVITATION, invite))
            {
                PacketSendUtility.SendPacket(invited, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_PARTY_DO_YOU_ACCEPT_INVITATION, 0, 0, inviter.GetName()));
            }
        }
    }

    public static PlayerGroup CreateGroup(Player leader, Player invited, TeamType type, int id)
    {
        PlayerGroup newGroup = new(new PlayerGroupMember(leader), type, id);
        groups[newGroup.GetTeamId()] = newGroup;
        AddPlayer(newGroup, leader);
        AddPlayer(newGroup, invited);
        if (Interlocked.CompareExchange(ref offlineCheckStarted, 1, 0) == 0)
        {
            InitializeOfflineCheck();
        }
        return newGroup;
    }

    private static void InitializeOfflineCheck()
    {
        ThreadPoolManager.GetInstance().ScheduleAtFixedRate(new OfflinePlayerChecker(), 1000, 30 * 1000);
    }

    public static void AddPlayerToGroup(PlayerGroup group, Player invited)
    {
        group.AddMember(new PlayerGroupMember(invited));
        FindGroupService.GetInstance().OnJoinedTeam(invited);
    }

    /// <summary>Change group's loot rules and notify team members</summary>
    public static void ChangeGroupRules(PlayerGroup group, LootGroupRules lootRules)
    {
        group.OnEvent(new ChangeGroupLootRulesEvent(group, lootRules));
    }

    /// <summary>Player entered world - search for non expired group</summary>
    public static void OnPlayerLogin(Player player)
    {
        foreach (PlayerGroup group in groups.Values)
        {
            PlayerGroupMember member = group.GetMember(player.GetObjectId());
            if (member != null)
            {
                group.OnEvent(new PlayerConnectedEvent(group, player));
            }
        }
    }

    /// <summary>Player leaved world - set last online on member</summary>
    public static void OnPlayerLogout(Player player)
    {
        PlayerGroup group = player.GetPlayerGroup();
        if (group != null)
        {
            PlayerGroupMember member = group.GetMember(player.GetObjectId());
            member.UpdateLastOnlineTime();
            group.OnEvent(new PlayerDisconnectedEvent(group, player));
        }
    }

    /// <summary>Update group members to some event of player</summary>
    public static void UpdateGroup(Player player, GroupEvent groupEvent)
    {
        PlayerGroup group = player.GetPlayerGroup();
        if (group != null)
        {
            group.OnEvent(new PlayerGroupUpdateEvent(group, player, groupEvent));
        }
    }

    public static void UpdateGroupEffects(Player player, int slot)
    {
        PlayerGroup group = player.GetPlayerGroup();
        if (group != null)
        {
            group.OnEvent(new PlayerGroupUpdateEvent(group, player, GroupEvent.UPDATE_EFFECTS, slot));
        }
    }

    /// <summary>Add player to group</summary>
    public static void AddPlayer(PlayerGroup group, Player player)
    {
        if (group == null)
            throw new ArgumentNullException(null, "Group should not be null");
        group.OnEvent(new PlayerGroupEnteredEvent(group, player));
    }

    /// <summary>Remove player from group (normal leave, or kick offline player)</summary>
    public static void RemovePlayer(Player player)
    {
        PlayerGroup group = player.GetPlayerGroup();
        if (group != null)
        {
            group.OnEvent(new PlayerGroupLeavedEvent(group, player));
        }
    }

    /// <summary>Remove player from group (ban)</summary>
    public static void BanPlayer(Player bannedPlayer, Player banGiver)
    {
        if (bannedPlayer == null)
            throw new ArgumentNullException(null, "Banned player should not be null");
        if (banGiver == null)
            throw new ArgumentNullException(null, "Bangiver player should not be null");
        PlayerGroup group = banGiver.GetPlayerGroup();
        if (group != null)
        {
            if (banGiver.Equals(bannedPlayer))
            {
                PacketSendUtility.SendPacket(banGiver, SM_SYSTEM_MESSAGE.STR_PARTY_CANT_BAN_SELF());
            }
            else if (!group.IsLeader(banGiver))
            {
                PacketSendUtility.SendPacket(banGiver, SM_SYSTEM_MESSAGE.STR_FORCE_ONLY_LEADER_CAN_BANISH());
            }
            else if (group.GetTeamType() == TeamType.AUTO_GROUP)
            {
                PacketSendUtility.SendPacket(banGiver, SM_SYSTEM_MESSAGE.STR_MSG_PARTY_FORCE_NO_RIGHT_TO_DECIDE());
            }
            else if (group.HasMember(bannedPlayer.GetObjectId()))
            {
                group.OnEvent(new PlayerGroupLeavedEvent(group, bannedPlayer, LeaveReson.BAN, banGiver.GetName()));
            }
            else
            {
                log.LogWarning("TEAM: banning {BannedPlayer} not in group {Members}", bannedPlayer, group.GetMembers());
            }
        }
    }

    /// <summary>Disband group by removing all players one by one</summary>
    public static void Disband(PlayerGroup group)
    {
        FindGroupService.GetInstance().RemoveRecruitment(group);
        groups.TryRemove(group.GetTeamId(), out _);
        group.OnEvent(new GroupDisbandEvent(group));
    }

    /// <summary>Share specific amount of kinah between group members</summary>
    public static void DistributeKinah(Player player, long kinah)
    {
        PlayerGroup group = player.GetPlayerGroup();
        if (group != null)
        {
            group.OnEvent(new TeamKinahDistributionEvent<PlayerGroup>(group, player, kinah));
        }
    }

    public static void ChangeLeader(Player player)
    {
        PlayerGroup group = player.GetPlayerGroup();
        if (group != null)
        {
            group.OnEvent(new ChangeGroupLeaderEvent(group, player));
        }
    }

    /// <summary>Start mentoring in group</summary>
    public static void StartMentoring(Player player)
    {
        PlayerGroup group = player.GetPlayerGroup();
        if (group != null)
        {
            group.OnEvent(new PlayerStartMentoringEvent(group, player));
        }
    }

    /// <summary>Stop mentoring in group</summary>
    public static void StopMentoring(Player player)
    {
        PlayerGroup group = player.GetPlayerGroup();
        if (group != null)
        {
            group.OnEvent(new PlayerGroupStopMentoringEvent(group, player));
        }
    }

    public static PlayerGroup SearchGroup(int playerObjId)
    {
        foreach (PlayerGroup group in groups.Values)
        {
            if (group.HasMember(playerObjId))
            {
                return group;
            }
        }
        return null;
    }

    public class OfflinePlayerChecker : Runnable
    {
        public void Run()
        {
            foreach (PlayerGroup group in groups.Values)
            {
                group.ForEachTeamMember(member =>
                {
                    if (!member.IsOnline() && TimeUtil.IsExpired(member.GetLastOnlineTime() + GroupConfig.GROUP_REMOVE_TIME * 1000))
                    {
                        group.OnEvent(new PlayerGroupLeavedEvent(group, member.GetObject(), LeaveReson.LEAVE_TIMEOUT));
                    }
                });
            }
        }
    }
}
