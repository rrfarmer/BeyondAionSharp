using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.League.Events;

/// <summary>
/// Java parity: model/team/league/events/LeagueLeftEvent (ATracer) : AlwaysTrueTeamEvent. Removes an alliance from the league,
/// reorganizes, and broadcasts leave/expel/disband messages. Java `implements Consumer<PlayerAlliance>` -> C# Accept(PlayerAlliance)
/// passed as a method group (league.forEach(this) -> league.ForEach(Accept)). Own nested enum LeaveReson SCREAMING. SM_*/LeagueService
/// red-tolerated.
/// </summary>
public class LeagueLeftEvent : AlwaysTrueTeamEvent
{
    private readonly League league;
    private readonly PlayerAlliance alliance;
    private readonly LeaveReson reason;

    public enum LeaveReson
    {
        LEAVE,
        EXPEL,
        DISBAND
    }

    public LeagueLeftEvent(League league, PlayerAlliance alliance)
        : this(league, alliance, LeaveReson.LEAVE)
    {
    }

    public LeagueLeftEvent(League league, PlayerAlliance alliance, LeaveReson reason)
    {
        this.league = league;
        this.alliance = alliance;
        this.reason = reason;
    }

    public override void HandleEvent()
    {
        league.RemoveMember(alliance.GetTeamId());
        Player newLeader = league.Reorganize();
        league.ForEach(Accept);

        if (newLeader != null)
        {
            league.SendPackets(SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_LEADER_TIMEOUT(newLeader.GetName()));
        }

        switch (reason)
        {
            case LeaveReson.LEAVE:
                alliance.SendPackets(new SM_ALLIANCE_INFO(alliance, SM_ALLIANCE_INFO.LEAGUE_LEFT_ME, alliance.GetLeaderObject().GetName()));
                CheckDisband();
                break;
            case LeaveReson.EXPEL:
                // TODO getCaptainName in team
                alliance.SendPackets(new SM_ALLIANCE_INFO(alliance, SM_ALLIANCE_INFO.LEAGUE_EXPELLED, league.GetLeaderObject().GetLeader().GetName()));
                CheckDisband();
                break;
            case LeaveReson.DISBAND:
                alliance.SendPackets(new SM_ALLIANCE_INFO(alliance, SM_ALLIANCE_INFO.LEAGUE_DISPERSED, ""));
                break;
        }
    }

    private void CheckDisband()
    {
        if (league.ShouldDisband())
        {
            LeagueService.Disband(league);
        }
    }

    public void Accept(PlayerAlliance leagueAlliance)
    {
        leagueAlliance.ForEach(member =>
        {
            switch (reason)
            {
                case LeaveReson.LEAVE:
                    PacketSendUtility.SendPacket(member,
                        new SM_ALLIANCE_INFO(leagueAlliance, SM_ALLIANCE_INFO.LEAGUE_LEFT_HIM, alliance.GetLeader().GetName()));
                    break;
                case LeaveReson.EXPEL:
                    // TODO may be EXPEL message only to leader
                    PacketSendUtility.SendPacket(member, new SM_ALLIANCE_INFO(leagueAlliance, SM_ALLIANCE_INFO.LEAGUE_EXPEL, alliance.GetLeader().GetName()));
                    break;
            }
        });
    }
}
