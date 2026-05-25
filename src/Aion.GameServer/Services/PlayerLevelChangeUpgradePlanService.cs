using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerLevelChangeUpgradePlanService
{
	public static PlayerLevelChangeUpgradePlan CreatePlan(
		Player? player,
		PlayerLevelChangeUpgradeStats? maxStats = null)
	{
		if (player == null)
			return PlayerLevelChangeUpgradePlan.MissingPlayer();

		var descriptors = new List<PlayerLevelChangeUpgradeDescriptor>();
		var plannedLifeStats = CreateLifeStatsDescriptor(player, maxStats, descriptors);

		descriptors.Add(new PlayerLevelChangeUpgradeDescriptor(
			PlayerLevelChangeUpgradeAction.VisualStatsUpdate,
			PlayerLevelChangeUpgradeDescriptorStatus.Planned,
			"PlayerController.upgradePlayer -> PlayerGameStats.updateStatsVisually",
			Notes: "Future live execution must send SM_STATS_INFO after HP/MP/FP synchronization."));

		var teamStatus = player.TeamMembership switch
		{
			PlayerTeamMembership.Group => PlayerLevelChangeUpgradeDescriptorStatus.Planned,
			PlayerTeamMembership.Alliance => PlayerLevelChangeUpgradeDescriptorStatus.Planned,
			_ => PlayerLevelChangeUpgradeDescriptorStatus.SkippedNoTeam,
		};
		descriptors.Add(new PlayerLevelChangeUpgradeDescriptor(
			PlayerLevelChangeUpgradeAction.TeamStatUpdate,
			teamStatus,
			"PlayerController.upgradePlayer -> TeamStatUpdater.add",
			Notes: player.TeamMembership switch
			{
				PlayerTeamMembership.Group => "Java queues SM_GROUP_MEMBER_INFO through TeamStatUpdater with GroupEvent.MOVEMENT.",
				PlayerTeamMembership.Alliance => "Java queues SM_ALLIANCE_MEMBER_INFO through TeamStatUpdater with PlayerAllianceEvent.MOVEMENT.",
				_ => "Java skips TeamStatUpdater when player.isInTeam() is false.",
			}));

		var legionStatus = player.LegionId != 0
			? PlayerLevelChangeUpgradeDescriptorStatus.Planned
			: PlayerLevelChangeUpgradeDescriptorStatus.SkippedNoLegion;
		descriptors.Add(new PlayerLevelChangeUpgradeDescriptor(
			PlayerLevelChangeUpgradeAction.LegionMemberUpdate,
			legionStatus,
			"PlayerController.upgradePlayer -> LegionService.updateMemberInfo",
			Notes: player.LegionId != 0
				? "Java refreshes LegionMember player data and broadcasts SM_LEGION_UPDATE_MEMBER to the legion."
				: "Java skips LegionService.updateMemberInfo when player.isLegionMember() is false."));

		return new PlayerLevelChangeUpgradePlan(
			PlayerLevelChangeUpgradePlanStatus.Planned,
			player.ObjectId,
			player.LifeStats,
			plannedLifeStats,
			maxStats,
			player.TeamMembership,
			player.LegionId,
			descriptors);
	}

	private static PlayerLifeStats? CreateLifeStatsDescriptor(
		Player player,
		PlayerLevelChangeUpgradeStats? maxStats,
		List<PlayerLevelChangeUpgradeDescriptor> descriptors)
	{
		// Java parity breadcrumb: PlayerController.upgradePlayer -> PlayerLifeStats.synchronizeWithMaxStats.
		if (player.LifeStats == null)
		{
			descriptors.Add(new PlayerLevelChangeUpgradeDescriptor(
				PlayerLevelChangeUpgradeAction.LifeStatsSynchronize,
				PlayerLevelChangeUpgradeDescriptorStatus.BlockedMissingLifeStats,
				"PlayerController.upgradePlayer -> PlayerLifeStats.synchronizeWithMaxStats",
				Notes: "C# player has no loaded LifeStats; future live execution cannot synchronize HP/MP/FP."));
			return null;
		}

		if (player.LifeStats.CurrentHp <= 0)
		{
			descriptors.Add(new PlayerLevelChangeUpgradeDescriptor(
				PlayerLevelChangeUpgradeAction.LifeStatsSynchronize,
				PlayerLevelChangeUpgradeDescriptorStatus.SkippedDead,
				"PlayerLifeStats.synchronizeWithMaxStats",
				Notes: "Java returns without changing dead players."));
			return player.LifeStats;
		}

		if (maxStats == null)
		{
			descriptors.Add(new PlayerLevelChangeUpgradeDescriptor(
				PlayerLevelChangeUpgradeAction.LifeStatsSynchronize,
				PlayerLevelChangeUpgradeDescriptorStatus.NeedsMaxStats,
				"CreatureLifeStats.synchronizeWithMaxStats; PlayerLifeStats.synchronizeWithMaxStats",
				Notes: "Java sets current HP/MP/FP to current max stats; C# max stat calculation is not supplied to this non-live plan."));
			return null;
		}

		var planned = new PlayerLifeStats(maxStats.MaxHp, maxStats.MaxMp, maxStats.MaxFp);
		descriptors.Add(new PlayerLevelChangeUpgradeDescriptor(
			PlayerLevelChangeUpgradeAction.LifeStatsSynchronize,
			PlayerLevelChangeUpgradeDescriptorStatus.Planned,
			"CreatureLifeStats.synchronizeWithMaxStats; PlayerLifeStats.synchronizeWithMaxStats",
			Notes: "Would set current HP and MP to max values and current FP to max FP, then future live code must send HP/MP/FP packets if spawned."));
		return planned;
	}
}

public sealed record PlayerLevelChangeUpgradeStats(int MaxHp, int MaxMp, int MaxFp);

public sealed record PlayerLevelChangeUpgradePlan(
	PlayerLevelChangeUpgradePlanStatus Status,
	int ObjectId,
	PlayerLifeStats? PreviousLifeStats,
	PlayerLifeStats? PlannedLifeStats,
	PlayerLevelChangeUpgradeStats? MaxStats,
	PlayerTeamMembership TeamMembership,
	int LegionId,
	IReadOnlyList<PlayerLevelChangeUpgradeDescriptor> Descriptors)
{
	public bool Applied => Status == PlayerLevelChangeUpgradePlanStatus.Planned;

	public static PlayerLevelChangeUpgradePlan MissingPlayer()
	{
		return new PlayerLevelChangeUpgradePlan(
			PlayerLevelChangeUpgradePlanStatus.MissingPlayer,
			ObjectId: 0,
			PreviousLifeStats: null,
			PlannedLifeStats: null,
			MaxStats: null,
			PlayerTeamMembership.None,
			LegionId: 0,
			Array.Empty<PlayerLevelChangeUpgradeDescriptor>());
	}
}

public sealed record PlayerLevelChangeUpgradeDescriptor(
	PlayerLevelChangeUpgradeAction Action,
	PlayerLevelChangeUpgradeDescriptorStatus Status,
	string JavaSource,
	bool IsLive = false,
	string? Notes = null);

public enum PlayerLevelChangeUpgradePlanStatus
{
	Planned,
	MissingPlayer,
}

public enum PlayerLevelChangeUpgradeAction
{
	LifeStatsSynchronize,
	VisualStatsUpdate,
	TeamStatUpdate,
	LegionMemberUpdate,
}

public enum PlayerLevelChangeUpgradeDescriptorStatus
{
	Planned,
	NeedsMaxStats,
	BlockedMissingLifeStats,
	SkippedDead,
	SkippedNoTeam,
	SkippedNoLegion,
}
