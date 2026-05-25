using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerLevelChangeUpgradePlanServiceTests
{
	[Fact]
	public void CreatePlan_StagesJavaUpgradePlayerOrderWithTeamAndLegionDependencies()
	{
		var player = CreatePlayer(
			lifeStats: new PlayerLifeStats(CurrentHp: 75, CurrentMp: 40, CurrentFp: 5),
			teamMembership: PlayerTeamMembership.Alliance,
			legionId: 77);

		var plan = PlayerLevelChangeUpgradePlanService.CreatePlan(
			player,
			new PlayerLevelChangeUpgradeStats(MaxHp: 300, MaxMp: 220, MaxFp: 110));

		Assert.Equal(PlayerLevelChangeUpgradePlanStatus.Planned, plan.Status);
		Assert.True(plan.Applied);
		Assert.Equal(player.ObjectId, plan.ObjectId);
		Assert.Equal(new PlayerLifeStats(75, 40, 5), plan.PreviousLifeStats);
		Assert.Equal(new PlayerLifeStats(300, 220, 110), plan.PlannedLifeStats);
		Assert.Equal(PlayerTeamMembership.Alliance, plan.TeamMembership);
		Assert.Equal(77, plan.LegionId);
		Assert.Equal(
		[
			PlayerLevelChangeUpgradeAction.LifeStatsSynchronize,
			PlayerLevelChangeUpgradeAction.VisualStatsUpdate,
			PlayerLevelChangeUpgradeAction.TeamStatUpdate,
			PlayerLevelChangeUpgradeAction.LegionMemberUpdate,
		], plan.Descriptors.Select(descriptor => descriptor.Action));
		Assert.All(plan.Descriptors, descriptor => Assert.False(descriptor.IsLive));
		Assert.All(plan.Descriptors, descriptor => Assert.Equal(PlayerLevelChangeUpgradeDescriptorStatus.Planned, descriptor.Status));
		Assert.Contains(
			plan.Descriptors,
			descriptor => descriptor.Action == PlayerLevelChangeUpgradeAction.TeamStatUpdate
				&& descriptor.Notes!.Contains("SM_ALLIANCE_MEMBER_INFO", StringComparison.Ordinal));
		Assert.Contains(
			plan.Descriptors,
			descriptor => descriptor.Action == PlayerLevelChangeUpgradeAction.LegionMemberUpdate
				&& descriptor.Notes!.Contains("SM_LEGION_UPDATE_MEMBER", StringComparison.Ordinal));
	}

	[Fact]
	public void CreatePlan_RecordsMissingMaxStatsDeadAndNoTeamLegionBranches()
	{
		var aliveNoInputs = CreatePlayer(
			lifeStats: new PlayerLifeStats(CurrentHp: 75, CurrentMp: 40, CurrentFp: 5),
			teamMembership: PlayerTeamMembership.None,
			legionId: 0);
		var dead = CreatePlayer(
			lifeStats: new PlayerLifeStats(CurrentHp: 0, CurrentMp: 40, CurrentFp: 5),
			teamMembership: PlayerTeamMembership.Group,
			legionId: 0);

		var missingMax = PlayerLevelChangeUpgradePlanService.CreatePlan(aliveNoInputs);
		var deadPlan = PlayerLevelChangeUpgradePlanService.CreatePlan(dead, new PlayerLevelChangeUpgradeStats(300, 220, 110));
		var missingPlayer = PlayerLevelChangeUpgradePlanService.CreatePlan(null);

		Assert.Null(missingMax.PlannedLifeStats);
		Assert.Collection(
			missingMax.Descriptors,
			descriptor => Assert.Equal(PlayerLevelChangeUpgradeDescriptorStatus.NeedsMaxStats, descriptor.Status),
			descriptor => Assert.Equal(PlayerLevelChangeUpgradeDescriptorStatus.Planned, descriptor.Status),
			descriptor => Assert.Equal(PlayerLevelChangeUpgradeDescriptorStatus.SkippedNoTeam, descriptor.Status),
			descriptor => Assert.Equal(PlayerLevelChangeUpgradeDescriptorStatus.SkippedNoLegion, descriptor.Status));
		Assert.Equal(new PlayerLifeStats(0, 40, 5), deadPlan.PlannedLifeStats);
		Assert.Equal(PlayerLevelChangeUpgradeDescriptorStatus.SkippedDead, deadPlan.Descriptors[0].Status);
		Assert.Equal(PlayerLevelChangeUpgradeDescriptorStatus.Planned, deadPlan.Descriptors[2].Status);
		Assert.Contains("SM_GROUP_MEMBER_INFO", deadPlan.Descriptors[2].Notes, StringComparison.Ordinal);
		Assert.Equal(PlayerLevelChangeUpgradePlanStatus.MissingPlayer, missingPlayer.Status);
		Assert.Empty(missingPlayer.Descriptors);
	}

	private static Player CreatePlayer(
		PlayerLifeStats? lifeStats,
		PlayerTeamMembership teamMembership,
		int legionId)
	{
		return new Player
		{
			ObjectId = 4501,
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 10,
			Exp = 9_000,
			IsOnline = true,
			LifeStats = lifeStats,
			TeamMembership = teamMembership,
			LegionId = legionId,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			AbyssRank = PlayerAbyssRank.Default(),
		};
	}
}
