using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.League.Events;

/// <summary>
/// Java parity: model/team/league/events/LeagueChangeLeaderEvent (xTz) : ChangeLeaderEvent&lt;PlayerAlliance&gt;. Transfers league
/// captaincy, swapping league positions, and broadcasts renumber/leader-change messages via nested per-alliance/per-member iteration.
/// Inherits CheckCondition from base. SM_SYSTEM_MESSAGE.STR_* catalog + League/LeagueMember red-tolerated.
/// </summary>
public class LeagueChangeLeaderEvent : ChangeLeaderEvent<PlayerAlliance>
{
    private League league;

    public LeagueChangeLeaderEvent(PlayerAlliance team, Player eventPlayer)
        : base(team, eventPlayer)
    {
        league = team.GetLeague();
    }

    protected override void ChangeLeaderTo(Player player)
    {
        int obj = eventPlayer.GetPlayerAlliance().GetObjectId();
        LeagueMember leagueMember = league.GetMember(obj);
        int position = leagueMember.GetLeaguePosition();
        leagueMember.SetLeaguePosition(0);
        league.ChangeLeader(leagueMember);
        league.GetMember(team.GetObjectId()).SetLeaguePosition(position);
        league.ForEach(alliance => alliance.ForEach(member =>
        {
            PacketSendUtility.SendPacket(member, new SM_ALLIANCE_INFO(member.GetPlayerAlliance()));
            if (team.Equals(leagueMember.GetObject()))
            {
                PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_FORCE_NUMBER_ME(position));
            }
            PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_FORCE_NUMBER_HIM(player.GetName(), leagueMember.GetLeaguePosition()));
            if (team.GetLeaderObject().Equals(member))
            {
                PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_UNION_CHANGE_LEADER(player.GetName(), player.GetName()));
            }
        }));
    }

    public override void HandleEvent()
    {
        if (!eventPlayer.IsInLeague() || !eventPlayer.GetPlayerAlliance().GetLeaderObject().Equals(eventPlayer))
        {
            return;
        }

        ChangeLeaderTo(eventPlayer);
    }
}
