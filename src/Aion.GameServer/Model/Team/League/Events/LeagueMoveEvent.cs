using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion.Serverpackets;

namespace Aion.GameServer.Model.Team.League.Events;

/// <summary>
/// Java parity: model/team/league/events/LeagueMoveEvent (Source) : AlwaysTrueTeamEvent. Swaps the league positions of two alliances
/// and broadcasts the renumbering. SM_SYSTEM_MESSAGE.STR_* catalog red-tolerated; League/LeagueMember red-tolerated.
/// </summary>
public class LeagueMoveEvent : AlwaysTrueTeamEvent
{
    private readonly League league;
    private readonly int selectedAllianceId;
    private readonly int targetAllianceId;
    private string selectedName;
    private string targetName;
    private int selectedCurrentPosition;
    private int targetCurrentPosition;

    public LeagueMoveEvent(League league, int selectedAllianceId, int targetAllianceId)
    {
        this.league = league;
        this.selectedAllianceId = selectedAllianceId;
        this.targetAllianceId = targetAllianceId;
    }

    public override void HandleEvent()
    {
        LeagueMember selected = league.GetMember(selectedAllianceId);
        LeagueMember target = league.GetMember(targetAllianceId);
        selectedCurrentPosition = selected.GetLeaguePosition();
        targetCurrentPosition = target.GetLeaguePosition();
        selected.SetLeaguePosition(targetCurrentPosition);
        target.SetLeaguePosition(selectedCurrentPosition);
        selectedName = selected.GetObject().GetLeaderObject().GetName();
        targetName = target.GetObject().GetLeaderObject().GetName();
        league.ForEach(alliance =>
        {
            alliance.SendPackets(new SM_ALLIANCE_INFO(alliance));

            if (alliance.GetObjectId() == selectedAllianceId)
            {
                alliance.SendPackets(SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_FORCE_NUMBER_ME(targetCurrentPosition));
            }
            else
            {
                alliance.SendPackets(SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_FORCE_NUMBER_HIM(selectedName, targetCurrentPosition));
            }

            if (alliance.GetObjectId() == targetAllianceId)
            {
                alliance.SendPackets(SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_FORCE_NUMBER_ME(selectedCurrentPosition));
            }
            else
            {
                alliance.SendPackets(SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_FORCE_NUMBER_HIM(targetName, selectedCurrentPosition));
            }
        });
    }
}
