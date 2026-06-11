using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>
/// Java parity: model/team/group/events/PlayerGroupEnteredEvent (ATracer) : PlayerEnteredEvent&lt;PlayerGroup&gt;. Adds player to group
/// and broadcasts join/enter member-info + system messages. team/player are protected base fields. PlayerGroupService/SM_* red-tolerated.
/// </summary>
public class PlayerGroupEnteredEvent : PlayerEnteredEvent<PlayerGroup>
{
    public PlayerGroupEnteredEvent(PlayerGroup group, Player player)
        : base(group, player)
    {
    }

    public override void HandleEvent()
    {
        PlayerGroupService.AddPlayerToGroup(team, player);
        PacketSendUtility.SendPacket(player, new SM_GROUP_INFO(team));
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_PARTY_ENTERED_PARTY());
        PacketSendUtility.SendPacket(player, new SM_GROUP_MEMBER_INFO(team, player, GroupEvent.JOIN));
        team.SendBrands(player);
        team.ForEach(member =>
        {
            if (!member.Equals(player))
            {
                // TODO probably here JOIN event
                PacketSendUtility.SendPacket(member, new SM_GROUP_MEMBER_INFO(team, player, GroupEvent.ENTER));
                PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_PARTY_HE_ENTERED_PARTY(player.GetName()));
                PacketSendUtility.SendPacket(player, new SM_GROUP_MEMBER_INFO(team, member, GroupEvent.ENTER));
            }
        });
        PacketSendUtility.BroadcastPacket(player, new SM_ABYSS_RANK_UPDATE(1, player), true);
        base.HandleEvent();
    }
}
