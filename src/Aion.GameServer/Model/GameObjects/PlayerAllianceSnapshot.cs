using Aion.GameServer.Services;

namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerAllianceSnapshot(
	int AllianceId,
	int LeaderObjectId,
	IReadOnlyList<int> MemberObjectIds,
	IReadOnlyDictionary<int, IReadOnlyList<int>> MemberObjectIdsByGroupId,
	IReadOnlyList<int> ViceCaptainObjectIds,
	PlayerAllianceTeamType TeamType,
	PlayerGroupLootRules LootRules)
{
	public int AllianceGroupSize => MemberObjectIds.Count;

	public PlayerAllianceInfoPacketPlan CreateInfoPacketPlan(
		int activePlayerMapId,
		int messageId = 0,
		string message = "",
		int leagueId = 0,
		IReadOnlyList<PlayerAllianceInfoLeagueRow>? leagueRows = null)
	{
		// Java parity: network/aion/serverpackets/SM_ALLIANCE_INFO is built from PlayerAlliance snapshot data.
		return PlayerAllianceInfoPacketPlan.FromSnapshot(
			AllianceId,
			LeaderObjectId,
			AllianceGroupSize,
			activePlayerMapId,
			ViceCaptainObjectIds,
			LootRules,
			TeamType,
			messageId,
			message,
			leagueId,
			leagueRows: leagueRows);
	}
}
