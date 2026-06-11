using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>
/// Java parity: model/team/group/events/PlayerDisconnectedEvent (ATracer) : ITeamEvent. Disbands if no online members remain,
/// otherwise transfers leadership and broadcasts offline/disconnected member info. PlayerGroupService/SM_SYSTEM_MESSAGE.STR_* red-tolerated.
/// </summary>
public class PlayerDisconnectedEvent : ITeamEvent
{
    private readonly PlayerGroup group;
    private readonly Player player;

    public PlayerDisconnectedEvent(PlayerGroup group, Player player)
    {
        this.group = group;
        this.player = player;
    }

    /// <summary>Player should be in group before disconnection</summary>
    public bool CheckCondition()
    {
        return group.HasMember(player.GetObjectId());
    }

    public void HandleEvent()
    {
        if (group.GetOnlineMembers().Count == 0)
        {
            PlayerGroupService.Disband(group);
        }
        else
        {
            if (player.Equals(group.GetLeader().GetObject()))
            {
                group.OnEvent(new ChangeGroupLeaderEvent(group));
            }
            group.ForEach(member =>
            {
                if (!member.Equals(player))
                {
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_PARTY_HE_BECOME_OFFLINE(player.GetName()));
                    PacketSendUtility.SendPacket(member, new SM_GROUP_MEMBER_INFO(group, player, GroupEvent.DISCONNECTED));
                    // disconnect other group members on logout? check
                    PacketSendUtility.SendPacket(player, new SM_GROUP_MEMBER_INFO(group, member, GroupEvent.DISCONNECTED));
                }
            });
        }
    }
}
