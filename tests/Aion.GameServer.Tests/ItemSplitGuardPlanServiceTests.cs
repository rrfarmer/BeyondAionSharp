using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemSplitGuardPlanServiceTests
{
	[Fact]
	public void CreatePlan_ValidSplitAmountAndNotTradingAllows()
	{
		var plan = ItemSplitGuardPlanService.CreatePlan(splitAmount: 1, isTrading: false);

		Assert.Equal(ItemSplitGuardPlanStatus.CanSplit, plan.Status);
		Assert.True(plan.CanSplit);
		Assert.False(plan.IsLive);
		Assert.Null(plan.DenialMessage);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void CreatePlan_ZeroOrNegativeSplitAmountBlocksSilently(long amount)
	{
		var plan = ItemSplitGuardPlanService.CreatePlan(amount, isTrading: false);

		Assert.Equal(ItemSplitGuardPlanStatus.BlockedZeroOrNegativeSplitAmount, plan.Status);
		Assert.False(plan.CanSplit);
		Assert.Null(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_TradingBlocksWithMessage()
	{
		var plan = ItemSplitGuardPlanService.CreatePlan(splitAmount: 5, isTrading: true);

		Assert.Equal(ItemSplitGuardPlanStatus.BlockedIsTrading, plan.Status);
		Assert.False(plan.CanSplit);
		Assert.NotNull(plan.DenialMessage);
		Assert.Equal(1300713, plan.DenialMessage!.MessageId);
	}

	[Fact]
	public void CreatePlan_ZeroAmountBlocksBeforeTradingCheck()
	{
		// Java: splitAmount <= 0 check is first
		var plan = ItemSplitGuardPlanService.CreatePlan(splitAmount: 0, isTrading: true);

		Assert.Equal(ItemSplitGuardPlanStatus.BlockedZeroOrNegativeSplitAmount, plan.Status);
		Assert.Null(plan.DenialMessage); // silent, not trading message
	}
}
