using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>
/// Java parity: model/team/alliance/events/PlayerAllianceLeavedEvent (ATracer) : PlayerLeavedEvent&lt;PlayerAllianceMember,
/// PlayerAlliance&gt; (two-param base maps 1:1). Removes member, transfers leadership, broadcasts leave/ban/disband messages, disbands
/// if needed. Java switch EXPRESSION -> C# switch expression; switch fallthrough -> shared case labels; inherited nested enum LeaveReson
/// by simple name. TeamType.AUTO_ALLIANCE SCREAMING. PlayerAllianceService/SM_* red-tolerated.
/// </summary>
public class PlayerAllianceLeavedEvent : PlayerLeavedEvent<PlayerAllianceMember, PlayerAlliance>
{
    public PlayerAllianceLeavedEvent(PlayerAlliance alliance, Player player)
        : base(alliance, player)
    {
    }

    public PlayerAllianceLeavedEvent(PlayerAlliance team, Player player, LeaveReson reason, string banPersonName)
        : base(team, player, reason, banPersonName)
    {
    }

    public PlayerAllianceLeavedEvent(PlayerAlliance alliance, Player player, LeaveReson reason)
        : base(alliance, player, reason)
    {
    }

    public override void HandleEvent()
    {
        team.GetViceCaptainIds().Remove(leavedPlayer.GetObjectId());

        if (reason != LeaveReson.DISBAND && team.IsLeader(leavedPlayer))
            team.OnEvent(new ChangeAllianceLeaderEvent(team));

        PlayerAllianceMember leavedTeamMember = team.RemoveMember(leavedPlayer.GetObjectId());

        SM_SYSTEM_MESSAGE leaveMsg = reason switch
        {
            LeaveReson.LEAVE => SM_SYSTEM_MESSAGE.STR_FORCE_LEAVE_HIM(leavedPlayer.GetName()),
            LeaveReson.LEAVE_TIMEOUT => SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_HE_LEAVED_PARTY_OFFLINE_TIMEOUT(leavedPlayer.GetName()),
            LeaveReson.BAN => SM_SYSTEM_MESSAGE.STR_FORCE_BAN_HIM(banPersonName, leavedPlayer.GetName()),
            _ => SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_DISPERSED(),
        };
        team.ForEach(player =>
        {
            PacketSendUtility.SendPacket(player, leaveMsg);
            if (reason != LeaveReson.DISBAND)
            {
                PacketSendUtility.SendPacket(player, new SM_ALLIANCE_MEMBER_INFO(leavedTeamMember, PlayerAllianceEvent.LEAVE));
                PacketSendUtility.SendPacket(player, new SM_ALLIANCE_INFO(team));
            }
        });
        switch (reason)
        {
            case LeaveReson.BAN:
            case LeaveReson.LEAVE:
                if (team.IsInLeague())
                {
                    // update general alliance info for all other alliances in league
                    team.GetLeague().Broadcast(team);
                }
                if (team.GetTeamType() != TeamType.AUTO_ALLIANCE && team.ShouldDisband())
                {
                    PlayerAllianceService.Disband(team, true);
                }
                if (reason == LeaveReson.BAN)
                {
                    PacketSendUtility.SendPacket(leavedPlayer, SM_SYSTEM_MESSAGE.STR_FORCE_BAN_ME(banPersonName));
                }
                break;
            case LeaveReson.LEAVE_TIMEOUT:
                if (team.GetTeamType() != TeamType.AUTO_ALLIANCE && team.ShouldDisband())
                {
                    PlayerAllianceService.Disband(team, true);
                }
                break;
            case LeaveReson.DISBAND:
                PacketSendUtility.SendPacket(leavedPlayer, SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_DISPERSED());
                break;
        }

        base.HandleEvent();
    }
}
