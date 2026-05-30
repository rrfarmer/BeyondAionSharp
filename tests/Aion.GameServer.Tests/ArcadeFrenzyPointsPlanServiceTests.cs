using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ArcadeFrenzyPointsPlanServiceTests
{
	[Fact]
	public void CreatePlan_BelowThresholdDoesNotTriggerFrenzy()
	{
		var plan = ArcadeFrenzyPointsPlanService.CreatePlan(currentFrenzyPoints: 90, addedFrenzyPoints: 8);
		// 90 + 8 = 98 < 100

		Assert.Equal(ArcadeFrenzyPointsPlanStatus.FrenzyNotTriggered, plan.Status);
		Assert.False(plan.FrenzyTriggered);
		Assert.Equal(98, plan.NewPoints);
		Assert.Equal(0, plan.FrenzyDurationMilliseconds);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_AtThresholdTriggersFrenzy()
	{
		// 92 + 8 = 100 >= 100 → FRENZY, remainder = 0
		var plan = ArcadeFrenzyPointsPlanService.CreatePlan(currentFrenzyPoints: 92, addedFrenzyPoints: 8);

		Assert.Equal(ArcadeFrenzyPointsPlanStatus.FrenzyTriggered, plan.Status);
		Assert.True(plan.FrenzyTriggered);
		Assert.Equal(0, plan.NewPoints);    // remainder = 100 % 100 = 0
		Assert.Equal(90000L, plan.FrenzyDurationMilliseconds);
	}

	[Fact]
	public void CreatePlan_AboveThresholdKeepsRemainder()
	{
		// 95 + 13 = 108 >= 100 → FRENZY, remainder = 108 % 100 = 8
		var plan = ArcadeFrenzyPointsPlanService.CreatePlan(currentFrenzyPoints: 95, addedFrenzyPoints: 13);

		Assert.Equal(ArcadeFrenzyPointsPlanStatus.FrenzyTriggered, plan.Status);
		Assert.Equal(8, plan.NewPoints);    // 108 % 100 = 8
		Assert.Equal(8, plan.RemainderPoints);
	}

	[Fact]
	public void Constants_MatchJavaValues()
	{
		Assert.Equal(100, ArcadeFrenzyPointsPlanService.FrenzyModeThreshold);
		Assert.Equal(90, ArcadeFrenzyPointsPlanService.FrenzyDurationSeconds);
		Assert.Equal(90000L, ArcadeFrenzyPointsPlanService.FrenzyDurationMilliseconds);
		Assert.Equal(8, ArcadeFrenzyPointsPlanService.FrenzyPointsPerToken);
	}
}
