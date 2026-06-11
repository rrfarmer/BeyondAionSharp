using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Model.Team.League.Events;

/// <summary>Java parity: model/team/league/events/LeagueCreateEvent (ATracer).</summary>
public class LeagueCreateEvent : AlwaysTrueTeamEvent
{
    private readonly League league;

    public LeagueCreateEvent(League league)
    {
        this.league = league;
    }

    public override void HandleEvent()
    {
        league.ForEach(alliance =>
        {
            alliance.SendPackets(new SM_ALLIANCE_INFO(alliance, SM_ALLIANCE_INFO.LEAGUE_ALLIANCE_ENTERED, alliance.GetLeader().GetName()));
        });
    }
}
