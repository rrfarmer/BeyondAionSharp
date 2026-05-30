using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class VeteranRewardEligibilityPlanServiceTests
{
	[Fact]
	public void CreatePlan_AllGuardsPassIsEligible()
	{
		var plan = VeteranRewardEligibilityPlanService.CreatePlan(
			playerLevel: 65, charAgeMonths: 3, accountAgeMonths: 6, receivedMonths: 2);

		Assert.Equal(VeteranRewardEligibilityPlanStatus.Eligible, plan.Status);
		Assert.True(plan.IsEligible);
		Assert.Equal(4, plan.PendingMonths); // 6 - 2 = 4
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_NotLevel65Skips()
	{
		var plan = VeteranRewardEligibilityPlanService.CreatePlan(64, 3, 6, 0);
		Assert.Equal(VeteranRewardEligibilityPlanStatus.SkippedNotLevel65, plan.Status);
	}

	[Fact]
	public void CreatePlan_CharTooYoungSkips()
	{
		var plan = VeteranRewardEligibilityPlanService.CreatePlan(65, charAgeMonths: 0, 6, 0);
		Assert.Equal(VeteranRewardEligibilityPlanStatus.SkippedCharTooYoung, plan.Status);
	}

	[Fact]
	public void CreatePlan_AccountTooYoungSkips()
	{
		var plan = VeteranRewardEligibilityPlanService.CreatePlan(65, 3, accountAgeMonths: 0, 0);
		Assert.Equal(VeteranRewardEligibilityPlanStatus.SkippedAccountTooYoung, plan.Status);
	}

	[Fact]
	public void CreatePlan_DaoErrorSkips()
	{
		var plan = VeteranRewardEligibilityPlanService.CreatePlan(65, 3, 6, receivedMonths: -1);
		Assert.Equal(VeteranRewardEligibilityPlanStatus.SkippedDaoError, plan.Status);
	}

	[Fact]
	public void CreatePlan_AlreadyReceivedAllSkips()
	{
		var plan = VeteranRewardEligibilityPlanService.CreatePlan(65, 3, 6, receivedMonths: 6);
		Assert.Equal(VeteranRewardEligibilityPlanStatus.SkippedAlreadyReceivedAll, plan.Status);
	}

	[Fact]
	public void Constants_MatchJavaValues()
	{
		Assert.Equal(4, VeteranRewardEligibilityPlanService.RandomItemsPerMonth);
		Assert.Equal(60, VeteranRewardEligibilityPlanService.MaxFixedRewardMonths);
	}
}
