using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportScheduledCallbackPlanServiceTests
{
	[Fact]
	public void CreatePlan_KinahFailureStopsBeforeCooldownFanoutAndMovement()
	{
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_500,
			currentKinah: 1_000);
		var cooldownPlan = CreateCooldownPlan();
		var fanoutPlan = CreateCooldownFanoutPlan();
		var movementPlan = CreateMovementPlan(playerIsDead: false, playerIsAboutToDie: false);

		var plan = BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan);

		Assert.False(plan.IsLive);
		Assert.Equal(BindPointTeleportScheduledCallbackPlanStatus.StoppedNotEnoughKinah, plan.Status);
		Assert.True(plan.ShouldSendNotEnoughFee);
		Assert.False(plan.ShouldStoreCooldown);
		Assert.False(plan.ShouldBroadcastCooldown);
		Assert.False(plan.ShouldScheduleFinalTeleport);
		Assert.False(plan.ShouldTeleport);
		Assert.False(plan.ShouldPlanTeleportSideEffects);
		Assert.Null(plan.CooldownPlan);
		Assert.Null(plan.CooldownFanoutPlan);
		Assert.Null(plan.FinalMovementPlan);
		Assert.Null(plan.TeleportSideEffectPlan);
		Assert.Equal(
			[
				BindPointTeleportScheduledCallbackPlanStep.TryDecreaseKinahFly,
				BindPointTeleportScheduledCallbackPlanStep.SendNotEnoughFeeAndReturn,
			],
			plan.Steps);
	}

	[Fact]
	public void CreatePlan_KinahSuccessComposesCooldownFanoutAndMovementInJavaOrder()
	{
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_500,
			currentKinah: 2_000);
		var cooldownPlan = CreateCooldownPlan();
		var fanoutPlan = CreateCooldownFanoutPlan();
		var movementPlan = CreateMovementPlan(playerIsDead: false, playerIsAboutToDie: false);

		var plan = BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan);

		Assert.Equal(BindPointTeleportScheduledCallbackPlanStatus.ReadyWithMovement, plan.Status);
		Assert.False(plan.ShouldSendNotEnoughFee);
		Assert.True(plan.ShouldStoreCooldown);
		Assert.True(plan.ShouldBroadcastCooldown);
		Assert.True(plan.ShouldScheduleFinalTeleport);
		Assert.True(plan.ShouldTeleport);
		Assert.False(plan.ShouldPlanTeleportSideEffects);
		Assert.Same(cooldownPlan, plan.CooldownPlan);
		Assert.Same(fanoutPlan, plan.CooldownFanoutPlan);
		Assert.Same(movementPlan, plan.FinalMovementPlan);
		Assert.Null(plan.TeleportSideEffectPlan);
		Assert.Equal(
			[
				BindPointTeleportScheduledCallbackPlanStep.TryDecreaseKinahFly,
				BindPointTeleportScheduledCallbackPlanStep.AddCooldown,
				BindPointTeleportScheduledCallbackPlanStep.BroadcastCooldown,
				BindPointTeleportScheduledCallbackPlanStep.ScheduleFinalTeleport,
				BindPointTeleportScheduledCallbackPlanStep.CheckFinalMovementGate,
				BindPointTeleportScheduledCallbackPlanStep.CreateFinalMovementIntent,
			],
			plan.Steps);
	}

	[Fact]
	public void CreatePlan_FinalMovementBlockedStillStoresCooldownAndBroadcastsLikeJava()
	{
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_500,
			currentKinah: 2_000);
		var cooldownPlan = CreateCooldownPlan();
		var fanoutPlan = CreateCooldownFanoutPlan();
		var movementPlan = CreateMovementPlan(playerIsDead: false, playerIsAboutToDie: true);

		var plan = BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan);

		Assert.Equal(BindPointTeleportScheduledCallbackPlanStatus.ReadyWithoutMovement, plan.Status);
		Assert.True(plan.ShouldStoreCooldown);
		Assert.True(plan.ShouldBroadcastCooldown);
		Assert.True(plan.ShouldScheduleFinalTeleport);
		Assert.False(plan.ShouldTeleport);
		Assert.False(plan.ShouldPlanTeleportSideEffects);
		Assert.Same(movementPlan, plan.FinalMovementPlan);
		Assert.Null(plan.TeleportSideEffectPlan);
		Assert.DoesNotContain(BindPointTeleportScheduledCallbackPlanStep.CreateFinalMovementIntent, plan.Steps);
		Assert.DoesNotContain(BindPointTeleportScheduledCallbackPlanStep.CreateTeleportSideEffectIntent, plan.Steps);
		Assert.Contains("schedule 1000ms final teleport gate", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_KinahSuccessCanCarryTeleportSideEffectMetadataAfterFinalMovementIntent()
	{
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_500,
			currentKinah: 2_000);
		var cooldownPlan = CreateCooldownPlan();
		var fanoutPlan = CreateCooldownFanoutPlan();
		var movementPlan = CreateMovementPlan(playerIsDead: false, playerIsAboutToDie: false);
		var sideEffectPlan = BindPointTeleportTeleportToSideEffectPlanService.CreatePlan(movementPlan);

		var plan = BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan,
			sideEffectPlan);

		Assert.Equal(BindPointTeleportScheduledCallbackPlanStatus.ReadyWithMovement, plan.Status);
		Assert.True(plan.ShouldTeleport);
		Assert.True(plan.ShouldPlanTeleportSideEffects);
		Assert.Same(sideEffectPlan, plan.TeleportSideEffectPlan);
		Assert.Equal(
			[
				BindPointTeleportScheduledCallbackPlanStep.TryDecreaseKinahFly,
				BindPointTeleportScheduledCallbackPlanStep.AddCooldown,
				BindPointTeleportScheduledCallbackPlanStep.BroadcastCooldown,
				BindPointTeleportScheduledCallbackPlanStep.ScheduleFinalTeleport,
				BindPointTeleportScheduledCallbackPlanStep.CheckFinalMovementGate,
				BindPointTeleportScheduledCallbackPlanStep.CreateFinalMovementIntent,
				BindPointTeleportScheduledCallbackPlanStep.CreateTeleportSideEffectIntent,
			],
			plan.Steps);
	}

	private static BindPointTeleportCooldownPlan CreateCooldownPlan()
	{
		return BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
			playerObjectId: 7001,
			locId: 6001,
			currentTimeMillis: 1_000);
	}

	private static BindPointTeleportFanoutPlan CreateCooldownFanoutPlan()
	{
		return BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			sourcePlayerObjectId: 7001,
			SmBindPointTeleport.Cooldown(playerObjectId: 7001, locId: 6001, cooldownSeconds: 600));
	}

	private static BindPointTeleportFinalMovementPlan CreateMovementPlan(bool playerIsDead, bool playerIsAboutToDie)
	{
		var destination = new BindPointTeleportDestinationFact(
			WorldId: 120010000,
			X: 100,
			Y: 200,
			Z: 300,
			Heading: 60,
			CurrentWorldId: 120010000,
			CurrentInstanceId: 1);

		return BindPointTeleportFinalMovementPlanService.CreatePlan(
			destination,
			playerIsDead,
			playerIsAboutToDie);
	}
}
