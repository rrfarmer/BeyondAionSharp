using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class XpLossPlanServiceTests
{
	[Fact]
	public void CreatePlan_LevelBelow6NoLoss()
	{
		var plan = XpLossPlanService.CreatePlan(playerLevel: 5, expNeed: 100_000);

		Assert.Equal(0L, plan.ExpLoss);
		Assert.False(plan.IsLive);
	}

	[Theory]
	[InlineData(6,  10_000L, 100L)]   // level 6 → LEVEL_6 (param=1.0) → round(10000/100*1.0)=100
	[InlineData(10, 10_000L, 100L)]   // level 10 → LEVEL_30 (param=1.0) → 100
	[InlineData(30, 10_000L, 100L)]   // level 30 → LEVEL_30 (param=1.0) → 100
	[InlineData(31, 10_000L, 35L)]    // level 31 → LEVEL_40 (param=0.35) → round(10000/100*0.35)=35
	[InlineData(40, 10_000L, 35L)]    // level 40 → LEVEL_40 (param=0.35) → 35
	[InlineData(41, 10_000L, 25L)]    // level 41 → LEVEL_50 (param=0.25) → 25
	[InlineData(65, 10_000L, 25L)]    // level 65 → LEVEL_65 (param=0.25) → 25
	public void CreatePlan_CorrectBracketAndLoss(int level, long expNeed, long expectedLoss)
	{
		var plan = XpLossPlanService.CreatePlan(level, expNeed);

		Assert.Equal(expectedLoss, plan.ExpLoss);
	}

	[Fact]
	public void CreatePlan_RoundsExpLossCorrectly()
	{
		// expNeed=1001, param=0.35 → 1001/100*0.35 = 3.5035 → round = 4
		var plan = XpLossPlanService.CreatePlan(35, expNeed: 1001);

		Assert.Equal(4L, plan.ExpLoss);
	}
}
