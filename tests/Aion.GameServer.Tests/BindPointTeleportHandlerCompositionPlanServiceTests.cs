using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportHandlerCompositionPlanServiceTests
{
	[Fact]
	public void CreatePlan_DeadPlayerStopsBeforeOperationAndCallbackComposition()
	{
		var operationPlan = CreateReadyOperationPlan(playerObjectId: 7001, locId: 6001);
		var callbackPlan = CreateCallbackPlan();

		var plan = BindPointTeleportHandlerCompositionPlanService.CreatePlan(
			action: 1,
			locId: 6001,
			kinah: 1_500,
			playerObjectId: 7001,
			playerIsDead: true,
			operationPlan,
			scheduledCallbackPlan: callbackPlan);

		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Equal(BindPointTeleportHandlerCompositionPlanStatus.NoActionDeadPlayer, plan.Status);
		Assert.Equal(BindPointTeleportClientActionPlanStatus.NoActionDeadPlayer, plan.ActionPlan.Status);
		Assert.Equal(BindPointTeleportRequestPlanStatus.NoActionDeadPlayer, plan.RequestPlan.Status);
		Assert.Null(plan.ScheduledCallbackPlan);
		Assert.Empty(plan.PacketIntents);
		Assert.Empty(plan.FanoutPlans);
		Assert.Equal(
			[
				BindPointTeleportHandlerCompositionPlanStep.ReadParsedClientPacketValues,
				BindPointTeleportHandlerCompositionPlanStep.CreateClientActionPlan,
				BindPointTeleportHandlerCompositionPlanStep.ComposeRequestPlan,
			],
			plan.Steps);
	}

	[Fact]
	public void CreatePlan_ActionOneReadyCarriesRequestFanoutAndCallbackMetadata()
	{
		var operationPlan = CreateReadyOperationPlan(playerObjectId: 7002, locId: 6002);
		var callbackPlan = CreateCallbackPlan();

		var plan = BindPointTeleportHandlerCompositionPlanService.CreatePlan(
			action: 1,
			locId: 6002,
			kinah: 1_500,
			playerObjectId: 7002,
			playerIsDead: false,
			operationPlan,
			scheduledCallbackPlan: callbackPlan);

		Assert.Equal(BindPointTeleportHandlerCompositionPlanStatus.TeleportReadyWithCallbackMetadata, plan.Status);
		Assert.Equal(BindPointTeleportClientActionPlanStatus.TeleportRequested, plan.ActionPlan.Status);
		Assert.Equal(BindPointTeleportRequestPlanStatus.TeleportReady, plan.RequestPlan.Status);
		Assert.Same(operationPlan, plan.RequestPlan.OperationPlan);
		Assert.Same(callbackPlan, plan.ScheduledCallbackPlan);
		Assert.Equal(operationPlan.PacketIntents, plan.PacketIntents);
		Assert.Equal(2, plan.FanoutPlans.Count);
		Assert.Equal(
			[
				BindPointTeleportHandlerCompositionPlanStep.ReadParsedClientPacketValues,
				BindPointTeleportHandlerCompositionPlanStep.CreateClientActionPlan,
				BindPointTeleportHandlerCompositionPlanStep.ComposeRequestPlan,
				BindPointTeleportHandlerCompositionPlanStep.AttachScheduledCallbackMetadata,
			],
			plan.Steps);
	}

	[Fact]
	public void CreatePlan_ActionOneMissingOperationFactsDoesNotAttachCallback()
	{
		var callbackPlan = CreateCallbackPlan();

		var plan = BindPointTeleportHandlerCompositionPlanService.CreatePlan(
			action: 1,
			locId: 6003,
			kinah: 1_500,
			playerObjectId: 7003,
			playerIsDead: false,
			scheduledCallbackPlan: callbackPlan);

		Assert.Equal(BindPointTeleportHandlerCompositionPlanStatus.TeleportNeedsOperationFacts, plan.Status);
		Assert.Equal(BindPointTeleportRequestPlanStatus.TeleportNeedsOperationFacts, plan.RequestPlan.Status);
		Assert.Null(plan.ScheduledCallbackPlan);
		Assert.Empty(plan.PacketIntents);
		Assert.Empty(plan.FanoutPlans);
		Assert.DoesNotContain(BindPointTeleportHandlerCompositionPlanStep.AttachScheduledCallbackMetadata, plan.Steps);
	}

	[Fact]
	public void CreatePlan_ActionTwoActiveTaskComposesCancelOnly()
	{
		var controlPlan = BindPointTeleportControlPlanService.CreateCancelPlan(
			playerObjectId: 7004,
			locId: 6004,
			hasSkillUseTask: true);

		var plan = BindPointTeleportHandlerCompositionPlanService.CreatePlan(
			action: 2,
			locId: 6004,
			kinah: 0,
			playerObjectId: 7004,
			playerIsDead: false,
			controlPlan: controlPlan,
			scheduledCallbackPlan: CreateCallbackPlan());

		Assert.Equal(BindPointTeleportHandlerCompositionPlanStatus.CancelReady, plan.Status);
		Assert.Equal(BindPointTeleportClientActionPlanStatus.CancelRequested, plan.ActionPlan.Status);
		Assert.Equal(BindPointTeleportRequestPlanStatus.CancelReady, plan.RequestPlan.Status);
		Assert.Same(controlPlan, plan.RequestPlan.ControlPlan);
		Assert.Null(plan.ScheduledCallbackPlan);
		Assert.Single(plan.PacketIntents);
		Assert.Single(plan.FanoutPlans);
	}

	[Fact]
	public void CreatePlan_UnknownActionNoopsWithoutFacts()
	{
		var plan = BindPointTeleportHandlerCompositionPlanService.CreatePlan(
			action: 99,
			locId: 0,
			kinah: 0,
			playerObjectId: 7005,
			playerIsDead: false);

		Assert.Equal(BindPointTeleportHandlerCompositionPlanStatus.NoActionUnknownAction, plan.Status);
		Assert.Equal(BindPointTeleportClientActionPlanStatus.NoActionUnknownAction, plan.ActionPlan.Status);
		Assert.Equal(BindPointTeleportRequestPlanStatus.NoActionUnknownAction, plan.RequestPlan.Status);
		Assert.Empty(plan.PacketIntents);
		Assert.Empty(plan.FanoutPlans);
		Assert.Contains("non-live handler composition bridge", plan.JavaSource, StringComparison.Ordinal);
	}

	private static BindPointTeleportOperationPlan CreateReadyOperationPlan(int playerObjectId, int locId)
	{
		var pricePlan = new BindPointTeleportPricePlan(
			HotspotId: locId,
			BasePrice: 1_000,
			Distance: 500,
			DistanceCost: 500,
			ComputedPrice: 1_500,
			ClientPrice: 1_500,
			PriceDifference: 0,
			ShouldWarnPriceMismatch: false,
			FinalPrice: 1_500,
			JavaSource: "test",
			IsLive: false);
		var requirementsPlan = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: locId,
			playerWorldId: 120010000,
			hotspotWorldId: 120010000,
			playerRace: "ELYOS",
			hotspotRace: "ELYOS",
			currentKinah: 2_000,
			requiredPrice: pricePlan.FinalPrice);

		return BindPointTeleportOperationPlanService.CreatePlan(
			playerObjectId,
			locId,
			hotspotExists: true,
			pricePlan,
			requirementsPlan);
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan()
	{
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_500,
			currentKinah: 2_000);
		var cooldownPlan = BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
			playerObjectId: 7002,
			locId: 6002,
			currentTimeMillis: 1_000);
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			sourcePlayerObjectId: 7002,
			SmBindPointTeleport.Cooldown(7002, 6002, 600));
		var destination = new BindPointTeleportDestinationFact(
			WorldId: 120010000,
			X: 100,
			Y: 200,
			Z: 300,
			Heading: 60,
			CurrentWorldId: 120010000,
			CurrentInstanceId: 1);
		var movementPlan = BindPointTeleportFinalMovementPlanService.CreatePlan(
			destination,
			playerIsDead: false,
			playerIsAboutToDie: false);
		var sideEffectPlan = BindPointTeleportTeleportToSideEffectPlanService.CreatePlan(movementPlan);

		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan,
			sideEffectPlan);
	}
}
