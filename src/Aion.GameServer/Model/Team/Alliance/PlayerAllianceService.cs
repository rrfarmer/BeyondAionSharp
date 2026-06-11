using System;
using System.Threading;
using System.Collections.Concurrent;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance.Events;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Model.Team.League.Events;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Findgroup;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.Team.Alliance;

/// <summary>Java parity: model/team/alliance/PlayerAllianceService (ATracer). Static alliance lifecycle. ConcurrentHashMap→ConcurrentDictionary; AtomicBoolean.compareAndSet→Interlocked.CompareExchange(ref int,1,0)==0; Objects.requireNonNull→ArgumentNullException; Runnable OfflinePlayerAllianceChecker→nested class w/ Run; forEachTeamMember lambda; TeamKinahDistributionEvent<>→explicit generic. PlayerAlliance/event classes/VortexService/FindGroupService/PlayerRestrictions red-tolerated.</summary>
public class PlayerAllianceService
{
    private static readonly ILogger log = NullLogger.Instance;
    private static readonly ConcurrentDictionary<int, PlayerAlliance> alliances = new();
    private static int offlineCheckStarted;

    public static void InviteToAlliance(Player inviter, Player invited)
    {
        if (PlayerRestrictions.CanInviteToAlliance(inviter, invited))
        {
            PlayerGroup playerGroup = invited.GetPlayerGroup();

            if (playerGroup != null)
            {
                Player leader = playerGroup.GetLeaderObject();
                if (!leader.Equals(invited))
                {
                    PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_FORCE_INVITE_PARTY_HIM(invited.GetName(), leader.GetName()));
                    PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_FORCE_INVITE_PARTY(leader.GetName(), playerGroup.GetMembers().Count));
                    invited = leader;
                }
                else
                {
                    PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_INVITED_HIS_PARTY(invited.GetName()));
                }
            }
            else
            {
                PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_FORCE_INVITED_HIM(invited.GetName()));
            }

            PlayerAllianceInvite invite = new(inviter);
            if (invited.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_PARTY_ALLIANCE_DO_YOU_ACCEPT_HIS_INVITATION, invite))
            {
                PacketSendUtility.SendPacket(invited,
                    new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_PARTY_ALLIANCE_DO_YOU_ACCEPT_HIS_INVITATION, 0, 0, inviter.GetName()));
            }
        }
    }

    public static PlayerAlliance CreateAlliance(Player leader, Player invited, TeamType type)
    {
        PlayerAlliance newAlliance = new(new PlayerAllianceMember(leader), type);
        alliances[newAlliance.GetTeamId()] = newAlliance;
        AddPlayer(newAlliance, leader);
        AddPlayer(newAlliance, invited);
        if (Interlocked.CompareExchange(ref offlineCheckStarted, 1, 0) == 0)
        {
            InitializeOfflineCheck();
        }
        return newAlliance;
    }

    private static void InitializeOfflineCheck()
    {
        ThreadPoolManager.GetInstance().ScheduleAtFixedRate(new OfflinePlayerAllianceChecker(), 1000, 30 * 1000);
    }

    public static PlayerAllianceMember AddPlayerToAlliance(PlayerAlliance alliance, Player invited)
    {
        PlayerAllianceMember member = new(invited);
        alliance.AddMember(member);
        FindGroupService.GetInstance().OnJoinedTeam(invited);
        return member;
    }

    /// <summary>Change alliance's loot rules and notify team members</summary>
    public static void ChangeGroupRules(PlayerAlliance alliance, LootGroupRules lootRules)
    {
        alliance.OnEvent(new ChangeAllianceLootRulesEvent(alliance, lootRules));
    }

    /// <summary>Player entered world - search for non expired alliance</summary>
    public static void OnPlayerLogin(Player player)
    {
        foreach (PlayerAlliance alliance in alliances.Values)
        {
            PlayerAllianceMember member = alliance.GetMember(player.GetObjectId());
            if (member != null)
            {
                alliance.OnEvent(new PlayerConnectedEvent(alliance, player));
            }
        }
    }

    /// <summary>Player leaved world - set last online on member</summary>
    public static void OnPlayerLogout(Player player)
    {
        PlayerAlliance alliance = player.GetPlayerAlliance();
        if (alliance != null)
        {
            PlayerAllianceMember member = alliance.GetMember(player.GetObjectId());
            member.UpdateLastOnlineTime();
            alliance.OnEvent(new PlayerDisconnectedEvent(alliance, player));
        }
    }

    /// <summary>Update alliance members to some event of player</summary>
    public static void UpdateAlliance(Player player, PlayerAllianceEvent allianceEvent)
    {
        PlayerAlliance alliance = player.GetPlayerAlliance();
        if (alliance != null)
        {
            alliance.OnEvent(new PlayerAllianceUpdateEvent(alliance, player, allianceEvent));
        }
    }

    public static void UpdateAllianceEffects(Player player, int slot)
    {
        PlayerAlliance alliance = player.GetPlayerAlliance();
        if (alliance != null)
        {
            alliance.OnEvent(new PlayerAllianceUpdateEvent(alliance, player, PlayerAllianceEvent.UPDATE_EFFECTS, slot));
        }
    }

    /// <summary>Add player to alliance</summary>
    public static void AddPlayer(PlayerAlliance alliance, Player player)
    {
        if (alliance == null)
            throw new ArgumentNullException(null, "Alliance should not be null");
        alliance.OnEvent(new PlayerAllianceEnteredEvent(alliance, player));
    }

    /// <summary>Remove player from alliance (normal leave, or kick offline player)</summary>
    public static void RemovePlayer(Player player)
    {
        PlayerAlliance alliance = player.GetPlayerAlliance();
        if (alliance != null)
        {
            if (alliance.GetTeamType().IsDefence())
            {
                VortexService.GetInstance().RemoveDefenderPlayer(player);
            }
            alliance.OnEvent(new PlayerAllianceLeavedEvent(alliance, player));
        }
    }

    /// <summary>Remove player from alliance (ban)</summary>
    public static void BanPlayer(Player bannedPlayer, Player banGiver)
    {
        if (bannedPlayer == null)
            throw new ArgumentNullException(null, "Banned player should not be null");
        if (banGiver == null)
            throw new ArgumentNullException(null, "Bangiver player should not be null");
        PlayerAlliance alliance = banGiver.GetPlayerAlliance();
        if (alliance != null)
        {
            if (banGiver.Equals(bannedPlayer))
            {
                PacketSendUtility.SendPacket(banGiver, SM_SYSTEM_MESSAGE.STR_FORCE_CANT_BAN_SELF());
            }
            else if (!alliance.IsLeader(banGiver))
            {
                PacketSendUtility.SendPacket(banGiver, SM_SYSTEM_MESSAGE.STR_FORCE_ONLY_LEADER_CAN_BANISH());
            }
            else if (alliance.GetTeamType() == TeamType.AUTO_ALLIANCE)
            {
                PacketSendUtility.SendPacket(banGiver, SM_SYSTEM_MESSAGE.STR_MSG_PARTY_FORCE_NO_RIGHT_TO_DECIDE());
            }
            else
            {
                if (alliance.GetTeamType().IsDefence())
                    VortexService.GetInstance().RemoveDefenderPlayer(bannedPlayer);
                if (alliance.HasMember(bannedPlayer.GetObjectId()))
                    alliance.OnEvent(new PlayerAllianceLeavedEvent(alliance, bannedPlayer, LeaveReson.BAN, banGiver.GetName()));
                else
                    log.LogWarning("TEAM: banning {BannedPlayer} not in alliance {Members}", bannedPlayer, alliance.GetMembers());
            }
        }
    }

    /// <summary>Disband alliance after minimum of members has been reached</summary>
    public static void Disband(PlayerAlliance alliance, bool onBefore)
    {
        FindGroupService.GetInstance().RemoveRecruitment(alliance);
        League league = alliance.GetLeague();
        if (onBefore && league != null)
            league.OnEvent(new LeagueLeftEvent(league, alliance));
        alliance.OnEvent(new AllianceDisbandEvent(alliance));
        alliances.TryRemove(alliance.GetTeamId(), out _);
        if (!onBefore && league != null)
            league.OnEvent(new LeagueLeftEvent(league, alliance));
    }

    public static void ChangeLeader(Player player)
    {
        PlayerAlliance alliance = player.GetPlayerAlliance();
        if (alliance != null)
        {
            alliance.OnEvent(new ChangeAllianceLeaderEvent(alliance, player));
        }
    }

    /// <summary>Change vice captain position of player (promote, demote)</summary>
    public static void ChangeViceCaptain(Player player, AssignViceCaptainEvent.AssignType assignType)
    {
        PlayerAlliance alliance = player.GetPlayerAlliance();
        if (alliance != null)
        {
            alliance.OnEvent(new AssignViceCaptainEvent(alliance, player, assignType));
        }
    }

    public static PlayerAlliance SearchAlliance(int playerObjId)
    {
        foreach (PlayerAlliance alliance in alliances.Values)
        {
            if (alliance.HasMember(playerObjId))
            {
                return alliance;
            }
        }
        return null;
    }

    /// <summary>Move members between alliance groups</summary>
    public static void ChangeMemberGroup(Player player, int firstPlayer, int secondPlayer, int allianceGroupId)
    {
        PlayerAlliance alliance = player.GetPlayerAlliance();
        if (alliance == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_FORCE_YOU_ARE_NOT_FORCE_MEMBER());
            return;
        }
        if (alliance.IsSomeCaptain(player))
        {
            alliance.OnEvent(new ChangeMemberGroupEvent(alliance, firstPlayer, secondPlayer, allianceGroupId));
        }
        else
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_FORCE_RIGHT_NOT_HAVE());
        }
    }

    /// <summary>Check that alliance is ready</summary>
    public static void CheckReady(Player player, TeamCommand eventCode)
    {
        PlayerAlliance alliance = player.GetPlayerAlliance();
        if (alliance != null)
        {
            alliance.OnEvent(new CheckAllianceReadyEvent(alliance, player, eventCode));
        }
    }

    /// <summary>Share specific amount of kinah between alliance members</summary>
    public static void DistributeKinah(Player player, long amount)
    {
        PlayerAlliance alliance = player.GetPlayerAlliance();
        if (alliance != null)
        {
            alliance.OnEvent(new TeamKinahDistributionEvent<PlayerAlliance>(alliance, player, amount));
        }
    }

    public static void DistributeKinahInGroup(Player player, long amount)
    {
        PlayerAllianceGroup allianceGroup = player.GetPlayerAllianceGroup();
        if (allianceGroup != null)
        {
            allianceGroup.OnEvent(new TeamKinahDistributionEvent<PlayerAllianceGroup>(allianceGroup, player, amount));
        }
    }

    public class OfflinePlayerAllianceChecker
    {
        public void Run()
        {
            foreach (PlayerAlliance alliance in alliances.Values)
            {
                alliance.ForEachTeamMember(member =>
                {
                    int kickDelay = alliance.GetTeamType().IsAutoTeam() ? 60 : GroupConfig.ALLIANCE_REMOVE_TIME;
                    if (!member.IsOnline() && TimeUtil.IsExpired(member.GetLastOnlineTime() + kickDelay * 1000))
                    {
                        if (alliance.GetTeamType().IsOffence())
                        {
                            VortexService.GetInstance().RemoveInvaderPlayer(member.GetObject());
                        }
                        alliance.OnEvent(new PlayerAllianceLeavedEvent(alliance, member.GetObject(), LeaveReson.LEAVE_TIMEOUT));
                    }
                });
            }
        }
    }
}
