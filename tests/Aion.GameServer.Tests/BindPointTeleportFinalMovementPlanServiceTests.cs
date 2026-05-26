using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportFinalMovementPlanServiceTests
{
	[Fact]
	public void CreatePlan_AliveAndNotAboutToDieCreatesTeleportIntent()
	{
		var destination = CreateDestination(currentWorldId: 120010000, currentInstanceId: 42, targetWorldId: 120010000);

		var plan = BindPointTeleportFinalMovementPlanService.CreatePlan(
			destination,
			playerIsDead: false,
			playerIsAboutToDie: false);

		Assert.False(plan.IsLive);
		Assert.Equal(BindPointTeleportFinalMovementPlanStatus.TeleportReady, plan.Status);
		Assert.True(plan.ShouldTeleport);
		Assert.Equal(120010000, plan.TargetWorldId);
		Assert.Equal(42, plan.TargetInstanceId);
		Assert.Equal(100.25f, plan.TargetX);
		Assert.Equal(200.5f, plan.TargetY);
		Assert.Equal(300.75f, plan.TargetZ);
		Assert.Equal(60, plan.TargetHeading);
		Assert.Equal("TeleportAnimation.NONE", plan.TeleportAnimation);
		Assert.Equal(
			[
				BindPointTeleportFinalMovementPlanStep.CheckAboutToDie,
				BindPointTeleportFinalMovementPlanStep.CheckDead,
				BindPointTeleportFinalMovementPlanStep.CreateTeleportIntent,
			],
			plan.Steps);
	}

	[Fact]
	public void CreatePlan_AboutToDieBlocksBeforeDeadCheckLikeJavaShortCircuit()
	{
		var destination = CreateDestination(currentWorldId: 120010000, currentInstanceId: 42, targetWorldId: 120010000);

		var plan = BindPointTeleportFinalMovementPlanService.CreatePlan(
			destination,
			playerIsDead: false,
			playerIsAboutToDie: true);

		Assert.Equal(BindPointTeleportFinalMovementPlanStatus.BlockedAboutToDie, plan.Status);
		Assert.False(plan.ShouldTeleport);
		Assert.Null(plan.TargetInstanceId);
		Assert.Equal([BindPointTeleportFinalMovementPlanStep.CheckAboutToDie], plan.Steps);
		Assert.Contains("isAboutToDie", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_DeadPlayerBlocksAfterAboutToDieCheck()
	{
		var destination = CreateDestination(currentWorldId: 120010000, currentInstanceId: 42, targetWorldId: 120010000);

		var plan = BindPointTeleportFinalMovementPlanService.CreatePlan(
			destination,
			playerIsDead: true,
			playerIsAboutToDie: false);

		Assert.Equal(BindPointTeleportFinalMovementPlanStatus.BlockedDead, plan.Status);
		Assert.False(plan.ShouldTeleport);
		Assert.Null(plan.TargetInstanceId);
		Assert.Equal(
			[
				BindPointTeleportFinalMovementPlanStep.CheckAboutToDie,
				BindPointTeleportFinalMovementPlanStep.CheckDead,
			],
			plan.Steps);
		Assert.Contains("isDead", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_CrossWorldTeleportUsesJavaDefaultInstanceOne()
	{
		var destination = CreateDestination(currentWorldId: 120010000, currentInstanceId: 42, targetWorldId: 220010000);

		var plan = BindPointTeleportFinalMovementPlanService.CreatePlan(
			destination,
			playerIsDead: false,
			playerIsAboutToDie: false);

		Assert.Equal(BindPointTeleportFinalMovementPlanStatus.TeleportReady, plan.Status);
		Assert.True(plan.ShouldTeleport);
		Assert.Equal(220010000, plan.TargetWorldId);
		Assert.Equal(1, plan.TargetInstanceId);
		Assert.Contains("instance 1 when crossing worlds", plan.JavaSource, StringComparison.Ordinal);
	}

	private static BindPointTeleportDestinationFact CreateDestination(
		int currentWorldId,
		int currentInstanceId,
		int targetWorldId)
	{
		return new BindPointTeleportDestinationFact(
			WorldId: targetWorldId,
			X: 100.25f,
			Y: 200.5f,
			Z: 300.75f,
			Heading: 60,
			CurrentWorldId: currentWorldId,
			CurrentInstanceId: currentInstanceId);
	}
}
