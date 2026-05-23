using Aion.GameServer.Services;

namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerAllianceDescriptor(
	int AllianceId,
	int LeaderObjectId,
	PlayerAllianceTeamType TeamType,
	int MaxMemberCount,
	int MaxGroupMemberCount,
	PlayerGroupLootRules LootRules,
	IReadOnlyList<int> AllianceGroupIds)
{
	public const int JavaMaxMemberCount = 24;
	public const int JavaMaxGroupMemberCount = 6;
	public static readonly IReadOnlyList<int> JavaAllianceGroupIds = [1000, 1001, 1002, 1003];

	public bool IsFull(int memberCount)
	{
		// Java parity: model/team/GeneralTeam.isFull compares size() with PlayerAlliance.getMaxMemberCount().
		return memberCount >= MaxMemberCount;
	}

	public static PlayerAllianceDescriptor FromLeader(
		int allianceId,
		Player leader,
		PlayerAllianceTeamType teamType = PlayerAllianceTeamType.Alliance,
		PlayerGroupLootRules? lootRules = null)
	{
		// Java parity: model/team/alliance/PlayerAlliance constructor stores TeamType, leader, loot rules, and groups 1000..1003.
		return new PlayerAllianceDescriptor(
			allianceId,
			leader.ObjectId,
			teamType,
			JavaMaxMemberCount,
			JavaMaxGroupMemberCount,
			lootRules ?? PlayerGroupLootRules.Default(),
			JavaAllianceGroupIds);
	}
}
