using Aion.GameServer.Services;
using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportOperationPlanServiceTests
{
	[Fact]
	public void CreatePlan_InvalidHotspotStopsBeforePriceAndRequirements()
	{
		var plan = BindPointTeleportOperationPlanService.CreatePlan(
			playerObjectId: 7001,
			locId: 6001,
			hotspotExists: false,
			pricePlan: null,
			requirementsPlan: null);

		Assert.False(plan.IsLive);
		Assert.False(plan.CanSchedule);
		Assert.Equal(BindPointTeleportOperationPlanStatus.InvalidHotspot, plan.Status);
		Assert.Null(plan.RequiredPrice);
		Assert.Equal("STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE", plan.SystemMessage);
		Assert.Equal("Tried to use invalid hotspot teleport to locId 6001", plan.AuditMessage);
		Assert.Empty(plan.PacketIntents);
		Assert.Equal(
			[BindPointTeleportOperationStep.AuditInvalidHotspot, BindPointTeleportOperationStep.SendNoRoute],
			plan.Steps);
	}

	[Fact]
	public void CreatePlan_RequirementFailureStopsBeforeBroadcastAndScheduling()
	{
		var pricePlan = CreatePricePlan(finalPrice: 1_500, shouldWarn: true);
		var requirementsPlan = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: 6002,
			playerWorldId: 120010000,
			hotspotWorldId: 120010000,
			playerRace: "ELYOS",
			hotspotRace: "ASMODIANS",
			currentKinah: 10_000,
			requiredPrice: pricePlan.FinalPrice);

		var plan = BindPointTeleportOperationPlanService.CreatePlan(
			playerObjectId: 7002,
			locId: 6002,
			hotspotExists: true,
			pricePlan,
			requirementsPlan);

		Assert.False(plan.CanSchedule);
		Assert.Equal(BindPointTeleportOperationPlanStatus.RequirementsFailed, plan.Status);
		Assert.Equal(1_500, plan.RequiredPrice);
		Assert.Equal(BindPointTeleportRequirementStatus.InvalidRace, plan.RequirementStatus);
		Assert.True(plan.ShouldWarnPriceMismatch);
		Assert.Null(plan.SystemMessage);
		Assert.Equal("tried to use hotspot teleport 6002 for invalid race ELYOS, expected ASMODIANS", plan.AuditMessage);
		Assert.Empty(plan.Steps);
		Assert.Empty(plan.PacketIntents);
	}

	[Fact]
	public void CreatePlan_ReadyPlanRecordsJavaSuccessOperationOrder()
	{
		var pricePlan = CreatePricePlan(finalPrice: 1_500, shouldWarn: false);
		var requirementsPlan = BindPointTeleportRequirementsPlanService.CreatePlan(
			hotspotId: 6003,
			playerWorldId: 120010000,
			hotspotWorldId: 120010000,
			playerRace: "ELYOS",
			hotspotRace: "ELYOS",
			currentKinah: 2_000,
			requiredPrice: pricePlan.FinalPrice);

		var plan = BindPointTeleportOperationPlanService.CreatePlan(
			playerObjectId: 7003,
			locId: 6003,
			hotspotExists: true,
			pricePlan,
			requirementsPlan);

		Assert.True(plan.CanSchedule);
		Assert.Equal(BindPointTeleportOperationPlanStatus.ReadyToSchedule, plan.Status);
		Assert.Equal(1_500, plan.RequiredPrice);
		Assert.Equal(BindPointTeleportRequirementStatus.Ready, plan.RequirementStatus);
		Assert.False(plan.ShouldWarnPriceMismatch);
		Assert.Null(plan.SystemMessage);
		Assert.Null(plan.AuditMessage);
		Assert.Equal(
			[
				BindPointTeleportOperationStep.BroadcastStart,
				BindPointTeleportOperationStep.ScheduleSkillUseTask,
				BindPointTeleportOperationStep.TryDecreaseKinahFly,
				BindPointTeleportOperationStep.SendNotEnoughFeeIfScheduledKinahDecreaseFails,
				BindPointTeleportOperationStep.AddCooldown,
				BindPointTeleportOperationStep.BroadcastCooldown,
				BindPointTeleportOperationStep.ScheduleFinalTeleport,
			],
			plan.Steps);
		Assert.Collection(
			plan.PacketIntents,
			packet =>
			{
				using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
				Assert.Equal(1, (int)reader.ReadC());
				Assert.Equal(7003, reader.ReadD());
				Assert.Equal(6003, reader.ReadD());
				Assert.Equal(0, reader.Remaining);
			},
			packet =>
			{
				using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
				Assert.Equal(3, (int)reader.ReadC());
				Assert.Equal(7003, reader.ReadD());
				Assert.Equal(6003, reader.ReadD());
				Assert.Equal(600, reader.ReadD());
				Assert.Equal(0, reader.Remaining);
			});
		Assert.Contains("schedule 10000ms skill task", plan.JavaSource, StringComparison.Ordinal);
		Assert.Contains("schedule 1000ms teleport", plan.JavaSource, StringComparison.Ordinal);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static BindPointTeleportPricePlan CreatePricePlan(long finalPrice, bool shouldWarn)
	{
		return new BindPointTeleportPricePlan(
			HotspotId: 6000,
			BasePrice: 1_000,
			Distance: 500,
			DistanceCost: 500,
			ComputedPrice: 1_500,
			ClientPrice: finalPrice,
			PriceDifference: shouldWarn ? 500 : 0,
			ShouldWarnPriceMismatch: shouldWarn,
			FinalPrice: finalPrice,
			JavaSource: "test",
			IsLive: false);
	}
}
