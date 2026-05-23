using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record PlayerGroupEnteredPacketPlan(
	int TeamId,
	int EnteringPlayerObjectId,
	bool SendGroupInfoToEnteringPlayer,
	PlayerGroupInfoPacketPlan? GroupInfoPlan,
	IReadOnlyList<PlayerGroupSystemMessageIntent> SystemMessageIntents,
	PlayerGroupBrandIntent? BrandIntent,
	PlayerGroupAbyssRankUpdateIntent? AbyssRankUpdateIntent)
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

public sealed record PlayerGroupBrandIntent(
	int RecipientObjectId,
	IReadOnlyDictionary<int, int> TargetObjectIdsByBrandId)
{
	public SmShowBrand CreatePacket()
	{
		// Java parity: model/team/TemporaryPlayerTeam.sendBrands sends SM_SHOW_BRAND(current brand map) to the entering player.
		return new SmShowBrand(TargetObjectIdsByBrandId);
	}
}

public sealed record PlayerGroupAbyssRankUpdateIntent(
	int PlayerObjectId,
	int TeamObjectId,
	bool IncludeSelf)
{
	public SmAbyssRankUpdate CreatePacket()
	{
		// Java parity: model/team/group/events/PlayerGroupEnteredEvent broadcasts SM_ABYSS_RANK_UPDATE(1, player), includeSelf=true.
		return SmAbyssRankUpdate.TeamObjectId(PlayerObjectId, TeamObjectId);
	}
}
