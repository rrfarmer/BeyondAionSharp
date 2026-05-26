using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportRequestPlanServiceTests
{
	[Fact]
	public void CreatePlan_DeadPlayerStopsBeforeTeleportComposition()
	{
		var actionPlan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 1,
			locId: 6001,
			kinah: 1_000,
			playerIsDead: true);
		var operationPlan = CreateReadyOperationPlan(playerObjectId: 7001, locId: 6001);

		var plan = BindPointTeleportRequestPlanService.CreatePlan(
			actionPlan,
			playerObjectId: 7001,
			operationPlan);

		Assert.False(plan.IsLive);
		Assert.Equal(BindPointTeleportRequestPlanStatus.NoActionDeadPlayer, plan.Status);
		Assert.Null(plan.OperationPlan);
		Assert.Empty(plan.PacketIntents);
		Assert.Empty(plan.FanoutPlans);
		Assert.Equal([BindPointTeleportRequestPlanStep.ReadClientActionPlan], plan.Steps);
	}

	[Fact]
	public void CreatePlan_UnknownActionNoopsWithoutFanout()
	{
		var actionPlan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 99,
			locId: 0,
			kinah: 0,
			playerIsDead: false);

		var plan = BindPointTeleportRequestPlanService.CreatePlan(
			actionPlan,
			playerObjectId: 7002);

		Assert.Equal(BindPointTeleportRequestPlanStatus.NoActionUnknownAction, plan.Status);
		Assert.Empty(plan.PacketIntents);
		Assert.Empty(plan.FanoutPlans);
		Assert.Contains("switch default", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_ActionOneReadyComposesOperationPacketsAndFanout()
	{
		var actionPlan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 1,
			locId: 6003,
			kinah: 1_500,
			playerIsDead: false);
		var operationPlan = CreateReadyOperationPlan(playerObjectId: 7003, locId: 6003);

		var plan = BindPointTeleportRequestPlanService.CreatePlan(
			actionPlan,
			playerObjectId: 7003,
			operationPlan);

		Assert.Equal(BindPointTeleportRequestPlanStatus.TeleportReady, plan.Status);
		Assert.Same(operationPlan, plan.OperationPlan);
		Assert.Null(plan.ControlPlan);
		Assert.Equal(operationPlan.PacketIntents, plan.PacketIntents);
		Assert.Equal(
			[
				BindPointTeleportRequestPlanStep.ReadClientActionPlan,
				BindPointTeleportRequestPlanStep.ComposeTeleportOperationPlan,
				BindPointTeleportRequestPlanStep.ComposeTeleportStartFanout,
				BindPointTeleportRequestPlanStep.ComposeTeleportCooldownFanout,
			],
			plan.Steps);
		Assert.Collection(
			plan.FanoutPlans,
			start =>
			{
				Assert.Equal(BindPointTeleportFanoutSource.TeleportStartBroadcast, start.Source);
				Assert.True(start.IncludeSourcePlayer);
				Assert.Same(operationPlan.PacketIntents[0], start.Packet);
			},
			cooldown =>
			{
				Assert.Equal(BindPointTeleportFanoutSource.TeleportCooldownBroadcast, cooldown.Source);
				Assert.True(cooldown.IncludeSourcePlayer);
				Assert.Same(operationPlan.PacketIntents[1], cooldown.Packet);
			});
	}

	[Fact]
	public void CreatePlan_ActionOneRequirementsFailureDoesNotCreateFanout()
	{
		var actionPlan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 1,
			locId: 6004,
			kinah: 1_500,
			playerIsDead: false);
		var operationPlan = CreateBlockedOperationPlan(playerObjectId: 7004, locId: 6004);

		var plan = BindPointTeleportRequestPlanService.CreatePlan(
			actionPlan,
			playerObjectId: 7004,
			operationPlan);

		Assert.Equal(BindPointTeleportRequestPlanStatus.TeleportBlocked, plan.Status);
		Assert.Same(operationPlan, plan.OperationPlan);
		Assert.Empty(plan.PacketIntents);
		Assert.Empty(plan.FanoutPlans);
		Assert.Contains("stopped before broadcast", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_ActionOneMissingOperationFactsRecordsGap()
	{
		var actionPlan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 1,
			locId: 6005,
			kinah: 1_500,
			playerIsDead: false);

		var plan = BindPointTeleportRequestPlanService.CreatePlan(
			actionPlan,
			playerObjectId: 7005);

		Assert.Equal(BindPointTeleportRequestPlanStatus.TeleportNeedsOperationFacts, plan.Status);
		Assert.Null(plan.OperationPlan);
		Assert.Empty(plan.FanoutPlans);
		Assert.Contains("requires hotspot, price, and requirement facts", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_ActionTwoActiveSkillUseTaskComposesCancelFanout()
	{
		var actionPlan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 2,
			locId: 6006,
			kinah: 0,
			playerIsDead: false);
		var controlPlan = BindPointTeleportControlPlanService.CreateCancelPlan(
			playerObjectId: 7006,
			locId: 6006,
			hasSkillUseTask: true);

		var plan = BindPointTeleportRequestPlanService.CreatePlan(
			actionPlan,
			playerObjectId: 7006,
			controlPlan: controlPlan);

		Assert.Equal(BindPointTeleportRequestPlanStatus.CancelReady, plan.Status);
		Assert.Null(plan.OperationPlan);
		Assert.Same(controlPlan, plan.ControlPlan);
		var packet = Assert.Single(plan.PacketIntents);
		Assert.Same(controlPlan.Packet, packet);
		var fanout = Assert.Single(plan.FanoutPlans);
		Assert.Equal(BindPointTeleportFanoutSource.CancelBroadcast, fanout.Source);
		Assert.True(fanout.IncludeSourcePlayer);
		Assert.Same(controlPlan.Packet, fanout.Packet);
	}

	[Fact]
	public void CreatePlan_ActionTwoMissingSkillUseTaskDoesNotCreateFanout()
	{
		var actionPlan = BindPointTeleportClientActionPlanService.CreatePlan(
			action: 2,
			locId: 6007,
			kinah: 0,
			playerIsDead: false);
		var controlPlan = BindPointTeleportControlPlanService.CreateCancelPlan(
			playerObjectId: 7007,
			locId: 6007,
			hasSkillUseTask: false);

		var plan = BindPointTeleportRequestPlanService.CreatePlan(
			actionPlan,
			playerObjectId: 7007,
			controlPlan: controlPlan);

		Assert.Equal(BindPointTeleportRequestPlanStatus.CancelNoAction, plan.Status);
		Assert.Same(controlPlan, plan.ControlPlan);
		Assert.Empty(plan.PacketIntents);
		Assert.Empty(plan.FanoutPlans);
	}

	private static BindPointTeleportOperationPlan CreateReadyOperationPlan(int playerObjectId, int locId)
	{
		var pricePlan = CreatePricePlan(finalPrice: 1_500);
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

	private static BindPointTeleportOperationPlan CreateBlockedOperationPlan(int playerObjectId, int locId)
	{
		var pricePlan = CreatePricePlan(finalPrice: 1_500);
		var requirementsPlan = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: locId,
			playerWorldId: 120010000,
			hotspotWorldId: 120010000,
			playerRace: "ELYOS",
			hotspotRace: "ASMODIANS",
			currentKinah: 2_000,
			requiredPrice: pricePlan.FinalPrice);

		return BindPointTeleportOperationPlanService.CreatePlan(
			playerObjectId,
			locId,
			hotspotExists: true,
			pricePlan,
			requirementsPlan);
	}

	private static BindPointTeleportPricePlan CreatePricePlan(long finalPrice)
	{
		return new BindPointTeleportPricePlan(
			HotspotId: 6000,
			BasePrice: 1_000,
			Distance: 500,
			DistanceCost: 500,
			ComputedPrice: 1_500,
			ClientPrice: finalPrice,
			PriceDifference: 0,
			ShouldWarnPriceMismatch: false,
			FinalPrice: finalPrice,
			JavaSource: "test",
			IsLive: false);
	}
}
