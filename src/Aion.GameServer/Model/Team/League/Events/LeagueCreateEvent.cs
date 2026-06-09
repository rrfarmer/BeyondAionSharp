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
            alliance.SendPackets(new SmAllianceInfo(alliance, SmAllianceInfo.LEAGUE_ALLIANCE_ENTERED, alliance.GetLeader().GetName()));
        });
    }
}
