using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion.Serverpackets;

namespace Aion.GameServer.Model.Team.League.Events;

/// <summary>
/// Java parity: model/team/league/events/LeagueJoinEvent (Tibald) : ITeamEvent. Adds an alliance to the league and broadcasts joined
/// messages. SM_ALLIANCE_INFO.LEAGUE_ALLIANCE_ENTERED/LEAGUE_JOINED_ALLIANCE message ids; League/LeagueMember red-tolerated.
/// </summary>
public class LeagueJoinEvent : ITeamEvent
{
    private readonly League league;
    private readonly PlayerAlliance invitedAlliance;

    public LeagueJoinEvent(League league, PlayerAlliance invitedAlliance)
    {
        this.league = league;
        this.invitedAlliance = invitedAlliance;
    }

    /// <summary>Entered alliance should not be in league yet</summary>
    public bool CheckCondition()
    {
        return !league.HasMember(invitedAlliance.GetObjectId());
    }

    public void HandleEvent()
    {
        league.AddMember(new LeagueMember(invitedAlliance, league.Size()));
        league.ForEach(alliance =>
        {
            if (alliance.Equals(invitedAlliance))
            {
                alliance.SendPackets(new SM_ALLIANCE_INFO(alliance, SM_ALLIANCE_INFO.LEAGUE_ALLIANCE_ENTERED, league.GetCaptain().GetName()));
            }
            else
            {
                alliance.SendPackets(new SM_ALLIANCE_INFO(alliance, SM_ALLIANCE_INFO.LEAGUE_JOINED_ALLIANCE, invitedAlliance.GetLeaderObject().GetName()));
            }
        });
    }
}
