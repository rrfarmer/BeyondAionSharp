using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;

namespace Aion.GameServer.Model.Team.League;

/// <summary>Java parity: model/team/league/LeagueMember (ATracer). implements TeamMember&lt;PlayerAlliance&gt;.</summary>
public class LeagueMember : ITeamMember<PlayerAlliance>
{
    private readonly PlayerAlliance alliance;
    private int leaguePosition;

    public LeagueMember(PlayerAlliance alliance, int position)
    {
        this.alliance = alliance;
        this.leaguePosition = position;
    }

    public int GetObjectId()
    {
        return alliance.GetObjectId();
    }

    public string GetName()
    {
        return alliance.GetName();
    }

    public PlayerAlliance GetObject()
    {
        return alliance;
    }

    public void SetLeaguePosition(int leaguePosition)
    {
        this.leaguePosition = leaguePosition;
    }

    public int GetLeaguePosition()
    {
        return leaguePosition;
    }
}
