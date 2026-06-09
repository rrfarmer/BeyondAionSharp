using Aion.GameServer.Model.Team.Common.Events;

namespace Aion.GameServer.Model.Team.League.Events;

/// <summary>Java parity: model/team/league/events/LeagueDisbandEvent (ATracer).</summary>
public class LeagueDisbandEvent : AlwaysTrueTeamEvent
{
    private readonly League league;

    public LeagueDisbandEvent(League league)
    {
        this.league = league;
    }

    public override void HandleEvent()
    {
        league.ForEach(alliance => league.OnEvent(new LeagueLeftEvent(league, alliance, LeagueLeftEvent.LeaveReson.DISBAND)));
    }
}
