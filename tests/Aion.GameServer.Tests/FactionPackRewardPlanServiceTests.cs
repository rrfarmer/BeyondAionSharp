using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FactionPackRewardPlanServiceTests
{
	private static DateTimeOffset ElyosAccount => new DateTimeOffset(2020, 9, 15, 12, 0, 0, TimeSpan.Zero);
	private static DateTimeOffset AsmodianAccount => new DateTimeOffset(2022, 7, 1, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public void CreatePlan_ElyosWithinWindowIsEligible()
	{
		var plan = FactionPackRewardPlanService.CreatePlan(true, 65, 0, "ELYOS", ElyosAccount, false);

		Assert.Equal(FactionPackEligibilityPlanStatus.Eligible, plan.Status);
		Assert.True(plan.IsEligible);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_AsmodianWithinWindowIsEligible()
	{
		var plan = FactionPackRewardPlanService.CreatePlan(true, 65, 0, "ASMODIANS", AsmodianAccount, false);

		Assert.Equal(FactionPackEligibilityPlanStatus.Eligible, plan.Status);
		Assert.True(plan.IsEligible);
	}

	[Fact]
	public void CreatePlan_NotLevel65Skips()
	{
		var plan = FactionPackRewardPlanService.CreatePlan(true, 60, 0, "ELYOS", ElyosAccount, false);
		Assert.Equal(FactionPackEligibilityPlanStatus.SkippedNoRewardsOrNotLevel65, plan.Status);
	}

	[Fact]
	public void CreatePlan_ElyosBeforeWindowSkips()
	{
		var tooEarly = new DateTimeOffset(2020, 9, 10, 0, 0, 0, TimeSpan.Zero);
		var plan = FactionPackRewardPlanService.CreatePlan(true, 65, 0, "ELYOS", tooEarly, false);
		Assert.Equal(FactionPackEligibilityPlanStatus.SkippedAccountCreatedBeforeWindow, plan.Status);
	}

	[Fact]
	public void CreatePlan_ElyosAfterWindowSkips()
	{
		var tooLate = new DateTimeOffset(2020, 9, 30, 0, 0, 0, TimeSpan.Zero);
		var plan = FactionPackRewardPlanService.CreatePlan(true, 65, 0, "ELYOS", tooLate, false);
		Assert.Equal(FactionPackEligibilityPlanStatus.SkippedAccountCreatedAfterWindow, plan.Status);
	}

	[Fact]
	public void CreatePlan_AlreadyReceivedSkips()
	{
		var plan = FactionPackRewardPlanService.CreatePlan(true, 65, 0, "ELYOS", ElyosAccount, alreadyReceived: true);
		Assert.Equal(FactionPackEligibilityPlanStatus.SkippedAlreadyReceived, plan.Status);
	}

	[Fact]
	public void WindowConstants_MatchJavaValues()
	{
		Assert.Equal(2020, FactionPackRewardPlanService.ElyosMinCreationTime.Year);
		Assert.Equal(9, FactionPackRewardPlanService.ElyosMinCreationTime.Month);
		Assert.Equal(2022, FactionPackRewardPlanService.AsmodianMinCreationTime.Year);
		Assert.Equal(6, FactionPackRewardPlanService.AsmodianMinCreationTime.Month);
	}
}
