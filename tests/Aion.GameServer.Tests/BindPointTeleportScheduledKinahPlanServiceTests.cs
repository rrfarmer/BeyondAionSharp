using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportScheduledKinahPlanServiceTests
{
	[Fact]
	public void CreatePlan_EnoughKinahRecordsDecKinahFlyIntent()
	{
		var plan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_500,
			currentKinah: 2_000);

		Assert.False(plan.IsLive);
		Assert.Equal(BindPointTeleportScheduledKinahPlanStatus.DecrementReady, plan.Status);
		Assert.True(plan.ShouldDecreaseKinah);
		Assert.False(plan.ShouldSendNotEnoughFee);
		Assert.True(plan.ShouldContinueScheduledTeleport);
		Assert.Equal(500, plan.RemainingKinah);
		Assert.Equal("ItemPacketService.ItemUpdateType.DEC_KINAH_FLY", plan.ItemUpdateTypeName);
		Assert.Equal(0x4B, plan.ItemUpdateTypeMask);
		Assert.Null(plan.SystemMessage);
		Assert.Equal(
			[
				BindPointTeleportScheduledKinahPlanStep.TryDecreaseKinahFly,
				BindPointTeleportScheduledKinahPlanStep.ContinueCooldownAndTeleportFlow,
			],
			plan.Steps);
	}

	[Fact]
	public void CreatePlan_ExactKinahStillContinuesLikeTryDecreaseKinahSuccess()
	{
		var plan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_500,
			currentKinah: 1_500);

		Assert.Equal(BindPointTeleportScheduledKinahPlanStatus.DecrementReady, plan.Status);
		Assert.True(plan.ShouldDecreaseKinah);
		Assert.True(plan.ShouldContinueScheduledTeleport);
		Assert.Equal(0, plan.RemainingKinah);
	}

	[Fact]
	public void CreatePlan_NotEnoughKinahSendsFeeMessageAndStops()
	{
		var plan = BindPointTeleportScheduledKinahPlanService.CreatePlan(
			requiredPrice: 1_500,
			currentKinah: 1_499);

		Assert.Equal(BindPointTeleportScheduledKinahPlanStatus.NotEnoughKinah, plan.Status);
		Assert.False(plan.ShouldDecreaseKinah);
		Assert.True(plan.ShouldSendNotEnoughFee);
		Assert.False(plan.ShouldContinueScheduledTeleport);
		Assert.Null(plan.RemainingKinah);
		Assert.Equal("STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE", plan.SystemMessage);
		Assert.Equal(
			[
				BindPointTeleportScheduledKinahPlanStep.TryDecreaseKinahFly,
				BindPointTeleportScheduledKinahPlanStep.SendNotEnoughFee,
			],
			plan.Steps);
		Assert.Contains("return", plan.JavaSource, StringComparison.Ordinal);
	}
}
