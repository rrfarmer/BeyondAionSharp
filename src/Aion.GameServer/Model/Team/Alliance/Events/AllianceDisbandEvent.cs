using Aion.GameServer.Model.Team.Common.Events;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>Java parity: model/team/alliance/events/AllianceDisbandEvent.</summary>
public class AllianceDisbandEvent : AlwaysTrueTeamEvent
{
    private readonly PlayerAlliance alliance;

    public AllianceDisbandEvent(PlayerAlliance alliance)
    {
        this.alliance = alliance;
    }

    public override void HandleEvent()
    {
        alliance.ForEach(player => alliance.OnEvent(new PlayerAllianceLeavedEvent(alliance, player, PlayerLeavedEvent.LeaveReson.DISBAND)));
    }
}
