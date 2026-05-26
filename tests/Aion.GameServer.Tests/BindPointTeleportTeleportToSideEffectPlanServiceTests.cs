using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportTeleportToSideEffectPlanServiceTests
{
	[Fact]
	public void CreatePlan_BlockedFinalMovementProducesNoTeleportSideEffects()
	{
		var movementPlan = CreateMovementPlan(
			currentWorldId: 120010000,
			currentInstanceId: 42,
			targetWorldId: 120010000,
			playerIsAboutToDie: true);

		var plan = BindPointTeleportTeleportToSideEffectPlanService.CreatePlan(movementPlan);

		Assert.False(plan.IsLive);
		Assert.Equal(BindPointTeleportTeleportToSideEffectPlanStatus.BlockedFinalMovement, plan.Status);
		Assert.Empty(plan.Steps);
		Assert.Equal([BindPointTeleportTeleportToSideEffectGap.FinalMovementBlocked], plan.Gaps);
		Assert.False(plan.ShouldAbortPlayerActions);
		Assert.False(plan.ShouldBroadcastDelete);
		Assert.False(plan.ShouldRunSpawnTaskImmediately);
		Assert.False(plan.ShouldMovePlayer);
		Assert.False(plan.ShouldSendChannelInfo);
		Assert.Contains("blocked before TeleportService.teleportTo", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_SameInstanceUsesJavaSpawnOnSameMapPacketOrderWithoutTeleportLoc()
	{
		var movementPlan = CreateMovementPlan(
			currentWorldId: 120010000,
			currentInstanceId: 42,
			targetWorldId: 120010000);

		var plan = BindPointTeleportTeleportToSideEffectPlanService.CreatePlan(movementPlan);

		Assert.Equal(BindPointTeleportTeleportToSideEffectPlanStatus.ReadySameInstance, plan.Status);
		Assert.True(plan.ShouldAbortPlayerActions);
		Assert.True(plan.ShouldBroadcastDelete);
		Assert.False(plan.ShouldSendTeleportLoc);
		Assert.True(plan.ShouldRunSpawnTaskImmediately);
		Assert.True(plan.ShouldMovePlayer);
		Assert.True(plan.ShouldMovePet);
		Assert.True(plan.ShouldUseSameInstanceSpawnPath);
		Assert.True(plan.ShouldSendChannelInfo);
		Assert.True(plan.ShouldSendPlayerInfo);
		Assert.True(plan.ShouldSendStatsInfo);
		Assert.True(plan.ShouldSendMotion);
		Assert.False(plan.ShouldSendPlayerSpawn);
		Assert.True(plan.ShouldUpdateZone);
		Assert.Equal(
			[
				BindPointTeleportTeleportToSideEffectPlanStep.EnterTeleportToOverload,
				BindPointTeleportTeleportToSideEffectPlanStep.UsePlayerHeadingAndNoneAnimation,
				BindPointTeleportTeleportToSideEffectPlanStep.ResolveTargetInstance,
				BindPointTeleportTeleportToSideEffectPlanStep.AbortPlayerActions,
				BindPointTeleportTeleportToSideEffectPlanStep.DespawnPlayer,
				BindPointTeleportTeleportToSideEffectPlanStep.CreateSpawnTask,
				BindPointTeleportTeleportToSideEffectPlanStep.RunSpawnTaskImmediately,
				BindPointTeleportTeleportToSideEffectPlanStep.CheckAlreadySpawned,
				BindPointTeleportTeleportToSideEffectPlanStep.SnapshotCurrentWorldAndInstance,
				BindPointTeleportTeleportToSideEffectPlanStep.SetPlayerPosition,
				BindPointTeleportTeleportToSideEffectPlanStep.SetPetPosition,
				BindPointTeleportTeleportToSideEffectPlanStep.SetPortAnimation,
				BindPointTeleportTeleportToSideEffectPlanStep.SendChannelInfo,
				BindPointTeleportTeleportToSideEffectPlanStep.SendPlayerInfo,
				BindPointTeleportTeleportToSideEffectPlanStep.SendStatsInfo,
				BindPointTeleportTeleportToSideEffectPlanStep.SendMotion,
				BindPointTeleportTeleportToSideEffectPlanStep.SpawnPlayerAndPet,
				BindPointTeleportTeleportToSideEffectPlanStep.StartProtection,
				BindPointTeleportTeleportToSideEffectPlanStep.UpdateEffectIcons,
				BindPointTeleportTeleportToSideEffectPlanStep.UpdateZone,
				BindPointTeleportTeleportToSideEffectPlanStep.ResetPortAnimationNone,
			],
			plan.Steps);
		Assert.Contains(BindPointTeleportTeleportToSideEffectGap.PrivateStoreClosure, plan.Gaps);
		Assert.Contains(BindPointTeleportTeleportToSideEffectGap.ZoneUpdate, plan.Gaps);
	}

	[Fact]
	public void CreatePlan_MapChangeUsesChannelInfoPlayerSpawnAndInstanceMessageBranch()
	{
		var movementPlan = CreateMovementPlan(
			currentWorldId: 120010000,
			currentInstanceId: 42,
			targetWorldId: 300030000);

		var plan = BindPointTeleportTeleportToSideEffectPlanService.CreatePlan(
			movementPlan,
			destinationIsInstance: true,
			destinationIsPersonalWorld: false);

		Assert.Equal(BindPointTeleportTeleportToSideEffectPlanStatus.ReadyMapOrInstanceChange, plan.Status);
		Assert.True(plan.ShouldBroadcastDelete);
		Assert.False(plan.ShouldSendTeleportLoc);
		Assert.True(plan.ShouldRunSpawnTaskImmediately);
		Assert.False(plan.ShouldUseSameInstanceSpawnPath);
		Assert.True(plan.ShouldSendChannelInfo);
		Assert.False(plan.ShouldSendPlayerInfo);
		Assert.False(plan.ShouldSendStatsInfo);
		Assert.False(plan.ShouldSendMotion);
		Assert.True(plan.ShouldSendPlayerSpawn);
		Assert.True(plan.ShouldSendInstanceOpenedMessage);
		Assert.True(plan.ShouldRefreshLegionMemberInfo);
		Assert.Equal(1, movementPlan.TargetInstanceId);
		Assert.Equal(
			[
				BindPointTeleportTeleportToSideEffectPlanStep.EnterTeleportToOverload,
				BindPointTeleportTeleportToSideEffectPlanStep.UsePlayerHeadingAndNoneAnimation,
				BindPointTeleportTeleportToSideEffectPlanStep.ResolveTargetInstance,
				BindPointTeleportTeleportToSideEffectPlanStep.AbortPlayerActions,
				BindPointTeleportTeleportToSideEffectPlanStep.DespawnPlayer,
				BindPointTeleportTeleportToSideEffectPlanStep.CreateSpawnTask,
				BindPointTeleportTeleportToSideEffectPlanStep.RunSpawnTaskImmediately,
				BindPointTeleportTeleportToSideEffectPlanStep.CheckAlreadySpawned,
				BindPointTeleportTeleportToSideEffectPlanStep.SnapshotCurrentWorldAndInstance,
				BindPointTeleportTeleportToSideEffectPlanStep.LeaveMapAndInstanceIfChanged,
				BindPointTeleportTeleportToSideEffectPlanStep.SetPlayerPosition,
				BindPointTeleportTeleportToSideEffectPlanStep.SetPetPosition,
				BindPointTeleportTeleportToSideEffectPlanStep.SetPortAnimation,
				BindPointTeleportTeleportToSideEffectPlanStep.SendChannelInfo,
				BindPointTeleportTeleportToSideEffectPlanStep.SendPlayerSpawn,
				BindPointTeleportTeleportToSideEffectPlanStep.SendInstanceOpenedMessage,
				BindPointTeleportTeleportToSideEffectPlanStep.UpdateLegionMemberInfoIfWorldChanged,
			],
			plan.Steps);
		Assert.Contains(BindPointTeleportTeleportToSideEffectGap.LeaveMapCallback, plan.Gaps);
		Assert.Contains(BindPointTeleportTeleportToSideEffectGap.LeaveInstanceCallback, plan.Gaps);
		Assert.Contains(BindPointTeleportTeleportToSideEffectGap.LegionRefresh, plan.Gaps);
	}

	[Fact]
	public void CreatePlan_RecordsJavaRaceFallbacksForDeadAfterGateAndDuel()
	{
		var movementPlan = CreateMovementPlan(
			currentWorldId: 120010000,
			currentInstanceId: 42,
			targetWorldId: 120010000);

		var plan = BindPointTeleportTeleportToSideEffectPlanService.CreatePlan(
			movementPlan,
			playerBecameDeadAfterFinalGate: true,
			playerIsDueling: true);

		Assert.Contains(BindPointTeleportTeleportToSideEffectPlanStep.ReviveDeadAfterGateIfNeeded, plan.Steps);
		Assert.Contains(BindPointTeleportTeleportToSideEffectPlanStep.LoseDuelIfNeeded, plan.Steps);
		Assert.Contains(BindPointTeleportTeleportToSideEffectGap.DeadAfterGateReviveFallback, plan.Gaps);
		Assert.Contains(BindPointTeleportTeleportToSideEffectGap.DuelLoss, plan.Gaps);
	}

	private static BindPointTeleportFinalMovementPlan CreateMovementPlan(
		int currentWorldId,
		int currentInstanceId,
		int targetWorldId,
		bool playerIsDead = false,
		bool playerIsAboutToDie = false)
	{
		var destination = new BindPointTeleportDestinationFact(
			WorldId: targetWorldId,
			X: 100.25f,
			Y: 200.5f,
			Z: 300.75f,
			Heading: 60,
			CurrentWorldId: currentWorldId,
			CurrentInstanceId: currentInstanceId);

		return BindPointTeleportFinalMovementPlanService.CreatePlan(
			destination,
			playerIsDead,
			playerIsAboutToDie);
	}
}
