using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerSellLimitPlanServiceTests
{
	[Fact]
	public void CreatePlan_LimitsDisabledReturnsFullItemCount()
	{
		var plan = PlayerSellLimitPlanService.CreatePlan(
			limitsEnabled: false, dynamicCapEnabled: false,
			itemPrice: 100, itemCount: 5, currentLimit: 1000);

		Assert.Equal(PlayerSellLimitPlanStatus.AllowedUnlimited, plan.Status);
		Assert.Equal(5, plan.UseCount);
		Assert.False(plan.IsLive);
		Assert.Null(plan.LimitReachedMessage);
	}

	[Fact]
	public void CreatePlan_ZeroPriceReturnsFullItemCount()
	{
		var plan = PlayerSellLimitPlanService.CreatePlan(
			limitsEnabled: true, dynamicCapEnabled: false,
			itemPrice: 0, itemCount: 5, currentLimit: 1000);

		Assert.Equal(PlayerSellLimitPlanStatus.AllowedUnlimited, plan.Status);
		Assert.Equal(5, plan.UseCount);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(-100)]
	public void CreatePlan_NegativePriceReturnsZero(long price)
	{
		var plan = PlayerSellLimitPlanService.CreatePlan(
			limitsEnabled: true, dynamicCapEnabled: false,
			itemPrice: price, itemCount: 5, currentLimit: 1000);

		Assert.Equal(PlayerSellLimitPlanStatus.BlockedInvalidInputs, plan.Status);
		Assert.Equal(0, plan.UseCount);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void CreatePlan_ZeroOrNegativeItemCountReturnsZero(long count)
	{
		var plan = PlayerSellLimitPlanService.CreatePlan(
			limitsEnabled: true, dynamicCapEnabled: false,
			itemPrice: 100, itemCount: count, currentLimit: 1000);

		Assert.Equal(PlayerSellLimitPlanStatus.BlockedInvalidInputs, plan.Status);
		Assert.Equal(0, plan.UseCount);
	}

	[Fact]
	public void CreatePlan_ZeroLimitBlocksWithJavaLimitMessage()
	{
		var plan = PlayerSellLimitPlanService.CreatePlan(
			limitsEnabled: true, dynamicCapEnabled: false,
			itemPrice: 100, itemCount: 1, currentLimit: 0);

		Assert.Equal(PlayerSellLimitPlanStatus.BlockedLimitReached, plan.Status);
		Assert.Equal(0, plan.UseCount);
		Assert.True(plan.ShouldSendLimitMessage);
		Assert.NotNull(plan.LimitReachedMessage);
		Assert.Equal(1400938, plan.LimitReachedMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_LimitSmallerThanPriceBlocksWithMessage()
	{
		// possibleCount = max(0, 50 / 100) = 0
		var plan = PlayerSellLimitPlanService.CreatePlan(
			limitsEnabled: true, dynamicCapEnabled: false,
			itemPrice: 100, itemCount: 1, currentLimit: 50);

		Assert.Equal(PlayerSellLimitPlanStatus.BlockedLimitReached, plan.Status);
		Assert.Equal(0, plan.UseCount);
		Assert.True(plan.ShouldSendLimitMessage);
	}

	[Fact]
	public void CreatePlan_DynamicCapAllowsOneExtraItemWhenPossibleBelowRequested()
	{
		// possibleCount = max(0, 50 / 100) = 0, dynamicCap adds 1 → possibleCount = 1
		var plan = PlayerSellLimitPlanService.CreatePlan(
			limitsEnabled: true, dynamicCapEnabled: true,
			itemPrice: 100, itemCount: 1, currentLimit: 50);

		Assert.Equal(PlayerSellLimitPlanStatus.AllowedWithCount, plan.Status);
		Assert.Equal(1, plan.UseCount);
	}

	[Fact]
	public void CreatePlan_NormalLimitAllowsMinOfPossibleAndRequested()
	{
		// possibleCount = 500 / 100 = 5, itemCount = 3 → useCount = 3
		var plan = PlayerSellLimitPlanService.CreatePlan(
			limitsEnabled: true, dynamicCapEnabled: false,
			itemPrice: 100, itemCount: 3, currentLimit: 500);

		Assert.Equal(PlayerSellLimitPlanStatus.AllowedWithCount, plan.Status);
		Assert.Equal(3, plan.UseCount);
		Assert.Equal(200, plan.RemainingLimitAfter); // 500 - 100*3 = 200
	}

	[Fact]
	public void CreatePlan_LimitExactlyCoversOneItemAllowsOne()
	{
		var plan = PlayerSellLimitPlanService.CreatePlan(
			limitsEnabled: true, dynamicCapEnabled: false,
			itemPrice: 100, itemCount: 5, currentLimit: 100);

		Assert.Equal(PlayerSellLimitPlanStatus.AllowedWithCount, plan.Status);
		Assert.Equal(1, plan.UseCount);
		Assert.Equal(0, plan.RemainingLimitAfter);
	}
}
