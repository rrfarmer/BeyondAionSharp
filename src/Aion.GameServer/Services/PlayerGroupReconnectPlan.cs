using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record PlayerGroupReconnectResult(
	bool Reconnected,
	PlayerGroupReconnectPacketPlan? PacketPlan)
{
	public static PlayerGroupReconnectResult NotFound()
	{
		return new PlayerGroupReconnectResult(false, null);
	}
}

public sealed record PlayerGroupReconnectPacketPlan(
	int TeamId,
	int ReconnectingPlayerObjectId,
	bool SendGroupInfoToReconnectingPlayer,
	IReadOnlyList<PlayerGroupMemberInfoIntent> MemberInfoIntents,
	PlayerGroupInfoPacketPlan? GroupInfoPlan)
{
	public SmGroupInfo? CreateGroupInfoPacket()
	{
		// Java parity: model/team/group/events/PlayerConnectedEvent sends SM_GROUP_INFO to the reconnecting player.
		return SendGroupInfoToReconnectingPlayer && GroupInfoPlan != null
			? new SmGroupInfo(GroupInfoPlan)
			: null;
	}
}

public sealed record PlayerGroupMemberInfoIntent(
	int RecipientObjectId,
	int SubjectObjectId,
	PlayerGroupEvent Event);
