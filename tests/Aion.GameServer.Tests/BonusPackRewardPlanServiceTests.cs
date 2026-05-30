using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BonusPackRewardPlanServiceTests
{
	private static BonusPackRewardPlan CanReward() =>
		BonusPackRewardPlanService.CreatePlan(true, 5, 65, 0, false);

	[Fact]
	public void CreatePlan_AllGuardsPassAllowsReward()
	{
		var plan = CanReward();
		Assert.Equal(BonusPackRewardPlanStatus.CanReward, plan.Status);
		Assert.True(plan.CanReward);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_NoRewardsSkips()
	{
		var plan = BonusPackRewardPlanService.CreatePlan(false, 0, 65, 0, false);
		Assert.Equal(BonusPackRewardPlanStatus.SkippedNoRewards, plan.Status);
		Assert.False(plan.CanReward);
	}

	[Fact]
	public void CreatePlan_WrongLevelSkips()
	{
		var plan = BonusPackRewardPlanService.CreatePlan(true, 5, 64, 0, false);
		Assert.Equal(BonusPackRewardPlanStatus.SkippedNotLevel65, plan.Status);
	}

	[Fact]
	public void CreatePlan_MailboxWouldOverflowSkips()
	{
		// 96 + 5 = 101 > 100
		var plan = BonusPackRewardPlanService.CreatePlan(true, 5, 65, 96, false);
		Assert.Equal(BonusPackRewardPlanStatus.SkippedMailboxWouldOverflow, plan.Status);
	}

	[Fact]
	public void CreatePlan_ExactlyAtLimitDoesNotOverflow()
	{
		// 95 + 5 = 100 is NOT > 100
		var plan = BonusPackRewardPlanService.CreatePlan(true, 5, 65, 95, false);
		Assert.Equal(BonusPackRewardPlanStatus.CanReward, plan.Status);
	}

	[Fact]
	public void CreatePlan_AlreadyReceivedSkips()
	{
		var plan = BonusPackRewardPlanService.CreatePlan(true, 5, 65, 0, alreadyReceived: true);
		Assert.Equal(BonusPackRewardPlanStatus.SkippedAlreadyReceived, plan.Status);
	}

	[Fact]
	public void Constants_MatchJavaValues()
	{
		Assert.Equal(65, BonusPackRewardPlanService.RequiredLevel);
		Assert.Equal(100, BonusPackRewardPlanService.MailboxLetterLimit);
		Assert.Equal("Beyond Aion", BonusPackRewardPlanService.MailSender);
	}
}
