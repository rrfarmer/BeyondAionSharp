using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>
/// Java parity: model/team/group/events/PlayerGroupStopMentoringEvent (ATracer) : PlayerStopMentoringEvent&lt;PlayerGroup&gt;. Sends a
/// MOVEMENT group-member-info update on mentor end. team/player are protected base fields. SM_GROUP_MEMBER_INFO red-tolerated.
/// </summary>
public class PlayerGroupStopMentoringEvent : PlayerStopMentoringEvent<PlayerGroup>
{
    public PlayerGroupStopMentoringEvent(PlayerGroup group, Player player)
        : base(group, player)
    {
    }

    protected override void SendGroupPacketOnMentorEnd(Player member)
    {
        PacketSendUtility.SendPacket(member, new SM_GROUP_MEMBER_INFO(team, player, GroupEvent.MOVEMENT));
    }
}
