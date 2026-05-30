using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class XpRewardPlanServiceTests
{
	[Theory]
	[InlineData(-11, 0)]
	[InlineData(-12, 0)]  // below min → 0
	[InlineData(-10, 1)]
	[InlineData(-9, 10)]
	[InlineData(-8, 20)]
	[InlineData(-7, 30)]
	[InlineData(-6, 40)]
	[InlineData(-5, 50)]
	[InlineData(-4, 60)]
	[InlineData(-3, 90)]
	[InlineData(-2, 100)]
	[InlineData(-1, 100)]
	[InlineData(0, 100)]
	[InlineData(1, 105)]
	[InlineData(2, 110)]
	[InlineData(3, 115)]
	[InlineData(4, 120)]
	[InlineData(5, 120)]   // above max → 120
	public void XpRewardFrom_AllTableEntries(int levelDiff, int expectedPercent)
	{
		Assert.Equal(expectedPercent, XpRewardPlanService.XpRewardFrom(levelDiff));
	}

	[Fact]
	public void CreatePlan_ComputesLevelDiffAndPercent()
	{
		var plan = XpRewardPlanService.CreatePlan(targetLevel: 30, playerLevel: 30);

		Assert.Equal(0, plan.LevelDifference);
		Assert.Equal(100, plan.XpRewardPercent);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_HighLevelTargetGivesBonus()
	{
		var plan = XpRewardPlanService.CreatePlan(targetLevel: 35, playerLevel: 32);

		Assert.Equal(3, plan.LevelDifference);
		Assert.Equal(115, plan.XpRewardPercent);
	}
}
