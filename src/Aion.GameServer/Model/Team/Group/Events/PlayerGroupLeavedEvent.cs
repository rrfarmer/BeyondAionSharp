using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>
/// Java parity: model/team/group/events/PlayerGroupLeavedEvent (ATracer) : PlayerLeavedEvent&lt;PlayerGroupMember, PlayerGroup&gt;
/// (two-param base maps 1:1). Removes the member, ends mentoring, broadcasts leave info + reason message, then disbands or transfers
/// leadership. switch-arrow on LeaveReson -> C# switch statements; inherited nested enum referenced by simple name. TeamType.AUTO_GROUP
/// SCREAMING. SM_*/PlayerGroupService red-tolerated.
/// </summary>
public class PlayerGroupLeavedEvent : PlayerLeavedEvent<PlayerGroupMember, PlayerGroup>
{
    public PlayerGroupLeavedEvent(PlayerGroup alliance, Player player)
        : base(alliance, player)
    {
    }

    public PlayerGroupLeavedEvent(PlayerGroup team, Player player, LeaveReson reason, string banPersonName)
        : base(team, player, reason, banPersonName)
    {
    }

    public PlayerGroupLeavedEvent(PlayerGroup alliance, Player player, LeaveReson reason)
        : base(alliance, player, reason)
    {
    }

    public override void HandleEvent()
    {
        team.RemoveMember(leavedPlayer.GetObjectId());

        if (leavedPlayer.IsMentor())
        {
            team.OnEvent(new PlayerGroupStopMentoringEvent(team, leavedPlayer));
        }

        team.ForEach(member =>
        {
            PacketSendUtility.SendPacket(member, new SM_GROUP_MEMBER_INFO(team, leavedPlayer, GroupEvent.LEAVE));

            switch (reason)
            {
                case LeaveReson.LEAVE:
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_PARTY_HE_LEAVE_PARTY(leavedPlayer.GetName()));
                    break;
                case LeaveReson.LEAVE_TIMEOUT:
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_PARTY_HE_BECOME_OFFLINE_TIMEOUT(leavedPlayer.GetName()));
                    break;
                case LeaveReson.BAN:
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_PARTY_HE_IS_BANISHED(leavedPlayer.GetName()));
                    break;
                case LeaveReson.DISBAND:
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_PARTY_IS_DISPERSED());
                    break;
            }
        });

        switch (reason)
        {
            case LeaveReson.BAN:
            case LeaveReson.LEAVE:
                if (team.GetTeamType() != TeamType.AUTO_GROUP && team.ShouldDisband())
                {
                    PlayerGroupService.Disband(team);
                }
                else
                {
                    if (leavedPlayer.Equals(team.GetLeader().GetObject()))
                    {
                        team.OnEvent(new ChangeGroupLeaderEvent(team));
                    }
                }
                if (reason == LeaveReson.BAN)
                {
                    PacketSendUtility.SendPacket(leavedPlayer, SM_SYSTEM_MESSAGE.STR_PARTY_YOU_ARE_BANISHED());
                }
                break;
            case LeaveReson.LEAVE_TIMEOUT:
                if (team.GetTeamType() != TeamType.AUTO_GROUP && team.ShouldDisband())
                {
                    PlayerGroupService.Disband(team);
                }
                break;
            case LeaveReson.DISBAND:
                PacketSendUtility.SendPacket(leavedPlayer, SM_SYSTEM_MESSAGE.STR_PARTY_IS_DISPERSED());
                break;
        }

        base.HandleEvent();
    }
}
