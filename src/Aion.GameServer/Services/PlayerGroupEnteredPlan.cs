using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record PlayerGroupEnteredPacketPlan(
	int TeamId,
	int EnteringPlayerObjectId,
	bool SendGroupInfoToEnteringPlayer,
	PlayerGroupInfoPacketPlan? GroupInfoPlan)
{
	public SmGroupInfo? CreateGroupInfoPacket()
	{
		// Java parity: model/team/group/events/PlayerGroupEnteredEvent sends SM_GROUP_INFO to the entering player.
		return SendGroupInfoToEnteringPlayer && GroupInfoPlan != null
			? new SmGroupInfo(GroupInfoPlan)
			: null;
	}
}
