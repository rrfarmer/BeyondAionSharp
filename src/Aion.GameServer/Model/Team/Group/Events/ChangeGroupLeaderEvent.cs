using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>
/// Java parity: model/team/group/events/ChangeGroupLeaderEvent (ATracer) : ChangeLeaderEvent&lt;PlayerGroup&gt;. Concrete PlayerGroup
/// satisfies the base `where T : class` bound, so team.ChangeLeader/GetMember/ForEach resolve via the concrete type. SM_SYSTEM_MESSAGE.STR_*
/// catalog red-tolerated.
/// </summary>
public class ChangeGroupLeaderEvent : ChangeLeaderEvent<PlayerGroup>
{
    public ChangeGroupLeaderEvent(PlayerGroup team, Player eventPlayer)
        : base(team, eventPlayer)
    {
    }

    public ChangeGroupLeaderEvent(PlayerGroup team)
        : base(team, null)
    {
    }

    public override void HandleEvent()
    {
        if (eventPlayer == null)
        {
            ChangeLeaderToNextAvailablePlayer();
        }
        else
        {
            ChangeLeaderTo(eventPlayer);
        }
    }

    protected override void ChangeLeaderTo(Player player)
    {
        team.ChangeLeader(team.GetMember(player.GetObjectId()));
        team.ForEach(member =>
        {
            PacketSendUtility.SendPacket(member, new SM_GROUP_INFO(team));
            if (!player.Equals(member))
            {
                PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_PARTY_HE_IS_NEW_LEADER(player.GetName()));
            }
            else
            {
                PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_PARTY_YOU_BECOME_NEW_LEADER());
            }
        });
    }
}
