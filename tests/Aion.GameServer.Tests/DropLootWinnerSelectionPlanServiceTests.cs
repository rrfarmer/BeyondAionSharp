using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class DropLootWinnerSelectionPlanServiceTests
{
	[Fact]
	public void CreatePlan_HigherLuckSetsNewWinnerWhilePlayersRemain()
	{
		var plan = DropLootWinnerSelectionPlanService.CreatePlan(
			luckyValue: 80, currentHighestValue: 50,
			playerObjectId: 9001, allPlayersFinished: false, currentWinnerObjectId: 7001);

		Assert.Equal(DropLootWinnerSelectionPlanStatus.NewHighestBid, plan.Status);
		Assert.True(plan.HasNewWinner);
		Assert.Equal(9001, plan.NewWinnerObjectId);
		Assert.Null(plan.AllPassedMessage);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_LowerLuckDoesNotChangeWinner()
	{
		var plan = DropLootWinnerSelectionPlanService.CreatePlan(
			luckyValue: 30, currentHighestValue: 50,
			playerObjectId: 9002, allPlayersFinished: false, currentWinnerObjectId: 7001);

		Assert.Equal(DropLootWinnerSelectionPlanStatus.NotHighest, plan.Status);
		Assert.False(plan.HasNewWinner);
		Assert.Equal(7001, plan.NewWinnerObjectId); // previous winner preserved
	}

	[Fact]
	public void CreatePlan_AllFinishedWithNoWinnerSendsAllPassedMessage()
	{
		var plan = DropLootWinnerSelectionPlanService.CreatePlan(
			luckyValue: 0, currentHighestValue: 0,
			playerObjectId: 9001, allPlayersFinished: true, currentWinnerObjectId: null);

		Assert.Equal(DropLootWinnerSelectionPlanStatus.AllPassed, plan.Status);
		Assert.Null(plan.NewWinnerObjectId);
		Assert.NotNull(plan.AllPassedMessage);
		Assert.Equal(1390227, plan.AllPassedMessage!.MessageId); // PayAllGiveup
	}

	[Fact]
	public void CreatePlan_AllFinishedWithWinnerAllowsDistribution()
	{
		var plan = DropLootWinnerSelectionPlanService.CreatePlan(
			luckyValue: 75, currentHighestValue: 50,
			playerObjectId: 9001, allPlayersFinished: true, currentWinnerObjectId: null);

		Assert.Equal(DropLootWinnerSelectionPlanStatus.NewHighestBid, plan.Status);
		Assert.Equal(9001, plan.NewWinnerObjectId);
		Assert.Null(plan.AllPassedMessage);
	}
}
