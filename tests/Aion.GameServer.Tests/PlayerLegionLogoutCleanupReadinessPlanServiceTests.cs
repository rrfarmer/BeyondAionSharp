using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerLegionLogoutCleanupReadinessPlanServiceTests
{
	[Fact]
	public void CreatePlan_NoLegionPlayer_SkipsJavaLegionCleanup()
	{
		var player = new Player { ObjectId = 1001, LegionId = 0 };

		var plan = PlayerLegionLogoutCleanupReadinessPlanService.CreatePlan(
			player,
			new PlayerLegionLogoutCleanupPrerequisites());

		Assert.Equal(PlayerLegionLogoutCleanupReadinessStatus.SkippedNoLegion, plan.Status);
		Assert.Equal(1001, plan.PlayerObjectId);
		Assert.Equal(0, plan.LegionId);
		Assert.False(plan.WouldRunWarehouseUpdate);
		Assert.False(plan.WouldRunMemberCleanup);
		Assert.False(plan.ReadyForLiveLogoutWiring);
		Assert.False(plan.IsLive);
		Assert.Empty(plan.MissingCriteria);
		Assert.Contains("LegionWhUpdate returns", plan.JavaSource, StringComparison.Ordinal);
		Assert.Contains("player.isLegionMember()", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_LegionMemberWithNoPrerequisites_EnumeratesAllBlockers()
	{
		var player = new Player { ObjectId = 1002, LegionId = 77 };

		var plan = PlayerLegionLogoutCleanupReadinessPlanService.CreatePlan(
			player,
			new PlayerLegionLogoutCleanupPrerequisites());

		Assert.Equal(PlayerLegionLogoutCleanupReadinessStatus.NotReady, plan.Status);
		Assert.Equal(1002, plan.PlayerObjectId);
		Assert.Equal(77, plan.LegionId);
		Assert.True(plan.WouldRunWarehouseUpdate);
		Assert.True(plan.WouldRunMemberCleanup);
		Assert.False(plan.ReadyForLiveLogoutWiring);
		Assert.False(plan.IsLive);
		Assert.Equal(
			[
				PlayerLegionLogoutCleanupReadinessCriterion.LegionWarehouseRuntimeAvailable,
				PlayerLegionLogoutCleanupReadinessCriterion.LegionWarehouseInUseStateAvailable,
				PlayerLegionLogoutCleanupReadinessCriterion.LegionWarehouseItemPersistenceAvailable,
				PlayerLegionLogoutCleanupReadinessCriterion.ItemStonePersistenceAvailable,
				PlayerLegionLogoutCleanupReadinessCriterion.LegionMemberRuntimeAvailable,
				PlayerLegionLogoutCleanupReadinessCriterion.LegionRepositoryAvailable,
				PlayerLegionLogoutCleanupReadinessCriterion.LegionMemberRepositoryAvailable,
				PlayerLegionLogoutCleanupReadinessCriterion.LegionMemberInfoFanoutAvailable,
				PlayerLegionLogoutCleanupReadinessCriterion.LegionBonusFanoutAvailable,
				PlayerLegionLogoutCleanupReadinessCriterion.LogoutHookAvailable,
			],
			plan.MissingCriteria);
		Assert.Contains("LegionService.LegionWhUpdate", plan.JavaSource, StringComparison.Ordinal);
		Assert.Contains("LegionService.onLogout", plan.JavaSource, StringComparison.Ordinal);
		Assert.Contains("missing one or more", plan.CSharpEvidence, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_PartialPrerequisites_RemoveOnlySatisfiedBlockers()
	{
		var player = new Player { ObjectId = 1003, LegionId = 88 };

		var plan = PlayerLegionLogoutCleanupReadinessPlanService.CreatePlan(
			player,
			new PlayerLegionLogoutCleanupPrerequisites(
				LegionWarehouseRuntimeAvailable: true,
				LegionWarehouseInUseStateAvailable: true,
				LegionMemberRuntimeAvailable: true,
				LogoutHookAvailable: true));

		Assert.Equal(PlayerLegionLogoutCleanupReadinessStatus.NotReady, plan.Status);
		Assert.DoesNotContain(PlayerLegionLogoutCleanupReadinessCriterion.LegionWarehouseRuntimeAvailable, plan.MissingCriteria);
		Assert.DoesNotContain(PlayerLegionLogoutCleanupReadinessCriterion.LegionWarehouseInUseStateAvailable, plan.MissingCriteria);
		Assert.DoesNotContain(PlayerLegionLogoutCleanupReadinessCriterion.LegionMemberRuntimeAvailable, plan.MissingCriteria);
		Assert.DoesNotContain(PlayerLegionLogoutCleanupReadinessCriterion.LogoutHookAvailable, plan.MissingCriteria);
		Assert.Contains(PlayerLegionLogoutCleanupReadinessCriterion.LegionWarehouseItemPersistenceAvailable, plan.MissingCriteria);
		Assert.Contains(PlayerLegionLogoutCleanupReadinessCriterion.ItemStonePersistenceAvailable, plan.MissingCriteria);
		Assert.Contains(PlayerLegionLogoutCleanupReadinessCriterion.LegionRepositoryAvailable, plan.MissingCriteria);
		Assert.Contains(PlayerLegionLogoutCleanupReadinessCriterion.LegionMemberRepositoryAvailable, plan.MissingCriteria);
		Assert.Contains(PlayerLegionLogoutCleanupReadinessCriterion.LegionMemberInfoFanoutAvailable, plan.MissingCriteria);
		Assert.Contains(PlayerLegionLogoutCleanupReadinessCriterion.LegionBonusFanoutAvailable, plan.MissingCriteria);
		Assert.False(plan.ReadyForLiveLogoutWiring);
	}

	[Fact]
	public void CreatePlan_AllPrerequisitesReady_MarksReadyButStaysNonLive()
	{
		var player = new Player { ObjectId = 1004, LegionId = 99 };

		var plan = PlayerLegionLogoutCleanupReadinessPlanService.CreatePlan(
			player,
			new PlayerLegionLogoutCleanupPrerequisites(
				LegionWarehouseRuntimeAvailable: true,
				LegionWarehouseInUseStateAvailable: true,
				LegionWarehouseItemPersistenceAvailable: true,
				ItemStonePersistenceAvailable: true,
				LegionMemberRuntimeAvailable: true,
				LegionRepositoryAvailable: true,
				LegionMemberRepositoryAvailable: true,
				LegionMemberInfoFanoutAvailable: true,
				LegionBonusFanoutAvailable: true,
				LogoutHookAvailable: true));

		Assert.Equal(PlayerLegionLogoutCleanupReadinessStatus.ReadyForLiveLogoutWiring, plan.Status);
		Assert.Empty(plan.MissingCriteria);
		Assert.True(plan.WouldRunWarehouseUpdate);
		Assert.True(plan.WouldRunMemberCleanup);
		Assert.True(plan.ReadyForLiveLogoutWiring);
		Assert.False(plan.IsLive);
		Assert.Contains("does not execute live persistence", plan.CSharpEvidence, StringComparison.Ordinal);
	}
}
