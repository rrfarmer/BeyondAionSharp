using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.League.Events;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.League;

/// <summary>Java parity: model/team/league/LeagueService (ATracer). Static. ConcurrentHashMap→ConcurrentDictionary (put→[]=, remove→TryRemove); Objects.requireNonNull(x,msg)→null-check throw ArgumentNullException; IllegalArgumentException→ArgumentException; Collections.unmodifiableCollection→ConcurrentDictionary.Values; .equals→.Equals. League/LeagueMember/event classes/SM packets red-tolerated.</summary>
public class LeagueService
{
    private static readonly ConcurrentDictionary<int, League> leagues = new();

    public static void InviteToLeague(Player inviter, Player invited)
    {
        if (CanInvite(inviter, invited))
        {
            PlayerAlliance playerAlliance = invited.GetPlayerAlliance();

            if (playerAlliance != null)
            {
                Player leader = playerAlliance.GetLeaderObject();
                if (!leader.Equals(invited))
                {
                    PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_UNION_INVITE_HIS_LEADER(invited.GetName(), leader.GetName()));
                }
                invited = leader;
            }

            LeagueInviteEvent invite = new(inviter, invited);
            if (invited.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, invite))
            {
                PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_UNION_INVITE_HIM(invited.GetName(), invited.GetPlayerAlliance().Size()));
                PacketSendUtility.SendPacket(invited, new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, 0, 0, inviter.GetName()));
            }
        }
    }

    public static bool CanInvite(Player inviter, Player invited)
    {
        if (inviter.IsDead())
        {
            // You cannot use the Alliance League invitation function while you are dead.
            PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_UNION_CANT_INVITE_WHEN_DEAD());
            return false;
        }
        else if (!invited.IsOnline())
        {
            // The player you invited to the Alliance League is currently offline.
            PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_UNION_OFFLINE_MEMBER());
            return false;
        }
        else if (invited.GetPlayerAlliance() == null)
        {
            // Currently, %0 cannot accept your invitation to join the alliance.
            PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_UNION_CANT_INVITE_WHEN_HE_IS_ASKED_QUESTION(invited.GetName()));
            return false;
        }
        else if (inviter.GetPlayerAlliance().HasMember(invited.GetObjectId()))
        {
            // You cannot invite your own alliance.
            PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_UNION_CANT_INVITE_SELF());
            return false;
        }
        else if (invited.GetPlayerAlliance().IsInLeague())
        {
            // The selected target is already a member of another force league.
            PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_UNION_ALREADY_MY_UNION());
            return false;
        }
        else if (inviter.GetPlayerAlliance().IsInLeague() && inviter.GetPlayerAlliance().GetLeague().IsFull())
        {
            // You cannot invite anymore as the Alliance League is full.
            PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_UNION_CANT_ADD_NEW_MEMBER());
            return false;
        }
        else if (inviter.GetPlayerAlliance().IsInLeague() && invited.GetPlayerAlliance().IsInLeague()
            && inviter.GetPlayerAlliance().GetLeague().Equals(invited.GetPlayerAlliance().GetLeague()))
        {
            // %0 is already a member of another Alliance League.
            PacketSendUtility.SendPacket(inviter, SM_SYSTEM_MESSAGE.STR_UNION_ALREADY_OTHER_UNION(invited.GetName()));
            return false;
        }
        return true;
    }

    public static League CreateLeague(Player leader)
    {
        PlayerAlliance alliance = leader.GetPlayerAlliance();
        if (alliance == null)
            throw new ArgumentNullException(null, "Alliance can not be null");
        LeagueMember mainAlliance = new(alliance, 0);
        League league = new(mainAlliance);
        league.SetLootGroupRules(new LootGroupRules(LootRuleType.FREEFORALL, 0, 0, 2, 2, 2, 2, 2));
        league.AddMember(mainAlliance);
        leagues[league.GetTeamId()] = league;
        league.OnEvent(new LeagueCreateEvent(league));
        return league;
    }

    /// <summary>Add alliance to league</summary>
    public static void AddAlliance(League league, PlayerAlliance alliance)
    {
        if (league == null)
            throw new ArgumentNullException(null, "League should not be null");
        league.OnEvent(new LeagueJoinEvent(league, alliance));
    }

    /// <summary>Remove alliance from league (normal leave)</summary>
    public static void RemoveAlliance(PlayerAlliance alliance)
    {
        if (alliance != null)
        {
            League league = alliance.GetLeague();
            if (league == null)
                throw new ArgumentNullException(null, "League should not be null");
            league.OnEvent(new LeagueLeftEvent(league, alliance, LeagueLeftEvent.LeaveReson.LEAVE));
        }
    }

    /// <summary>Remove alliance from league (expel)</summary>
    public static void ExpelAlliance(LeagueMember leagueAlliance, Player leagueLeader)
    {
        PlayerAlliance leagueLeaderAlliance = leagueLeader.GetPlayerAlliance();
        if (!leagueLeaderAlliance.IsLeader(leagueLeader))
            throw new ArgumentException("Given player is not the league alliance leader");
        League league = leagueLeaderAlliance.GetLeague();
        if (!league.IsLeader(leagueLeaderAlliance))
            throw new ArgumentException("Leader's alliance is not the league leader");
        league.OnEvent(new LeagueLeftEvent(league, leagueAlliance.GetObject(), LeagueLeftEvent.LeaveReson.EXPEL));
    }

    public static void SetLeader(Player player, Player allianceLeader)
    {
        PlayerAlliance alliance = player.GetPlayerAlliance();
        if (alliance != null)
        {
            League league = alliance.GetLeague();
            if (league != null)
                league.OnEvent(new LeagueChangeLeaderEvent(alliance, allianceLeader));
        }
    }

    /// <summary>Disband league after minimum of members has been reached</summary>
    public static void Disband(League league)
    {
        league.OnEvent(new LeagueDisbandEvent(league));
        leagues.TryRemove(league.GetTeamId(), out _);
    }

    public static ICollection<League> GetLeagues()
    {
        return leagues.Values;
    }

    public static void MoveAlliance(Player player, int selectedId, int targetId)
    {
        League league = player.GetPlayerAlliance().GetLeague();
        if (league.GetLeaderObject().GetLeaderObject().Equals(player))
        {
            league.OnEvent(new LeagueMoveEvent(league, selectedId, targetId));
        }
    }

    public static void ChangeGroupRules(League league, LootGroupRules lootRules)
    {
        league.OnEvent(new LeagueLootRulesChangeEvent(league, lootRules));
    }

    public static void DistributeKinah(Player player, long amount)
    {
        League league = player.GetPlayerAlliance().GetLeague();
        if (league != null)
        {
            league.OnEvent(new LeagueKinahDistributionEvent(player, amount));
        }
    }
}
