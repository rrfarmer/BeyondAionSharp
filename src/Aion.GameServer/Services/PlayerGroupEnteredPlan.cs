using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record PlayerGroupEnteredPacketPlan(
	int TeamId,
	int EnteringPlayerObjectId,
	bool SendGroupInfoToEnteringPlayer,
	PlayerGroupInfoPacketPlan? GroupInfoPlan,
	IReadOnlyList<PlayerGroupSystemMessageIntent> SystemMessageIntents)
{
	public SmGroupInfo? CreateGroupInfoPacket()
	{
		// Java parity: model/team/group/events/PlayerGroupEnteredEvent sends SM_GROUP_INFO to the entering player.
		return SendGroupInfoToEnteringPlayer && GroupInfoPlan != null
			? new SmGroupInfo(GroupInfoPlan)
			: null;
	}
}

public sealed record PlayerGroupSystemMessageIntent(
	int RecipientObjectId,
	SmSystemMessage Message);
