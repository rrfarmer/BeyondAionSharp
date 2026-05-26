using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportPricePlanServiceTests
{
	[Fact]
	public void CreatePlan_UsesJavaDistanceCostAndIgnoresOneKinahClientDrift()
	{
		var plan = BindPointTeleportPricePlanService.CreatePlan(
			hotspotId: 4001,
			playerX: 0,
			playerY: 0,
			playerZ: 0,
			hotspotX: 300,
			hotspotY: 400,
			hotspotZ: 0,
			hotspotBasePrice: 1_000,
			priceSentByGameClient: 1_499);

		Assert.False(plan.IsLive);
		Assert.Equal(4001, plan.HotspotId);
		Assert.Equal(500, plan.Distance);
		Assert.Equal(500, plan.DistanceCost);
		Assert.Equal(1_500, plan.ComputedPrice);
		Assert.Equal(1, plan.PriceDifference);
		Assert.False(plan.ShouldWarnPriceMismatch);
		Assert.Equal(1_500, plan.FinalPrice);
		Assert.Contains("BindPointTeleportService.calculateTeleportationPrice", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_UsesHigherClientPriceAndFlagsWarning()
	{
		var plan = BindPointTeleportPricePlanService.CreatePlan(
			hotspotId: 4002,
			playerX: 0,
			playerY: 0,
			playerZ: 0,
			hotspotX: 300,
			hotspotY: 400,
			hotspotZ: 0,
			hotspotBasePrice: 1_000,
			priceSentByGameClient: 2_000);

		Assert.Equal(1_500, plan.ComputedPrice);
		Assert.Equal(500, plan.PriceDifference);
		Assert.True(plan.ShouldWarnPriceMismatch);
		Assert.Equal(2_000, plan.FinalPrice);
	}

	[Fact]
	public void CreatePlan_UsesJavaLongTruncationForDiagonalDistance()
	{
		var plan = BindPointTeleportPricePlanService.CreatePlan(
			hotspotId: 4003,
			playerX: 0,
			playerY: 0,
			playerZ: 0,
			hotspotX: 1_000,
			hotspotY: 1_000,
			hotspotZ: 1_000,
			hotspotBasePrice: 100,
			priceSentByGameClient: 273);

		Assert.Equal(1_732.0508075688772, plan.Distance, precision: 12);
		Assert.Equal(173, plan.DistanceCost);
		Assert.Equal(273, plan.ComputedPrice);
		Assert.Equal(0, plan.PriceDifference);
		Assert.False(plan.ShouldWarnPriceMismatch);
		Assert.Equal(273, plan.FinalPrice);
	}

	[Fact]
	public void CreatePlan_AppliesJavaOneKinahMinimumBeforeClientReconciliation()
	{
		var plan = BindPointTeleportPricePlanService.CreatePlan(
			hotspotId: 4004,
			playerX: 10,
			playerY: 10,
			playerZ: 10,
			hotspotX: 10,
			hotspotY: 10,
			hotspotZ: 10,
			hotspotBasePrice: 0,
			priceSentByGameClient: 0);

		Assert.Equal(0, plan.Distance);
		Assert.Equal(0, plan.DistanceCost);
		Assert.Equal(1, plan.ComputedPrice);
		Assert.Equal(1, plan.PriceDifference);
		Assert.False(plan.ShouldWarnPriceMismatch);
		Assert.Equal(1, plan.FinalPrice);
	}
}
