using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record PlayerGroupInfoBroadcastIntent(
	int RecipientObjectId,
	PlayerGroupInfoPacketPlan GroupInfoPlan)
{
	public SmGroupInfo CreateGroupInfoPacket()
	{
		// Java parity: model/team/group/events/ChangeGroupLootRulesEvent broadcasts SM_GROUP_INFO to group members.
		return new SmGroupInfo(GroupInfoPlan);
	}
}

public sealed record PlayerGroupLootRulesChangedPacketPlan(
	int TeamId,
	IReadOnlyList<PlayerGroupInfoBroadcastIntent> GroupInfoBroadcasts);
