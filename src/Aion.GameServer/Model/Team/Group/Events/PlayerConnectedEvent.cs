using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>
/// Java parity: model/team/group/events/PlayerConnectedEvent (ATracer) : AlwaysTrueTeamEvent. Re-adds a reconnecting member, restores
/// leadership if needed, rebroadcasts group/member info. slf4j {} -> string concat. PlayerGroupMember/SM_* red-tolerated.
/// </summary>
public class PlayerConnectedEvent : AlwaysTrueTeamEvent
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerConnectedEvent));
    private readonly PlayerGroup group;
    private readonly Player player;

    public PlayerConnectedEvent(PlayerGroup group, Player player)
    {
        this.group = group;
        this.player = player;
    }

    public override void HandleEvent()
    {
        group.RemoveMember(player.GetObjectId());
        group.AddMember(new PlayerGroupMember(player));
        if (player.Equals(group.GetLeader().GetObject()))
        {
            log.LogWarning("[TEAM] leader (" + player + ") reconnected, but should have lost leadership on disconnect");
            group.ChangeLeader(new PlayerGroupMember(player));
        }
        PacketSendUtility.SendPacket(player, new SM_GROUP_INFO(group));
        PacketSendUtility.SendPacket(player, new SM_GROUP_MEMBER_INFO(group, player, GroupEvent.JOIN));
        group.ForEach(member =>
        {
            if (!player.Equals(member))
            {
                PacketSendUtility.SendPacket(member, new SM_GROUP_MEMBER_INFO(group, player, GroupEvent.ENTER));
                PacketSendUtility.SendPacket(player, new SM_GROUP_MEMBER_INFO(group, member, GroupEvent.ENTER));
            }
        });
        // change leader to player logging in first if all players are disconnected
        if (group.GetLeader() != null && !group.HasMember(group.GetLeader().GetObjectId()))
            group.OnEvent(new ChangeGroupLeaderEvent(group, player));
    }
}
