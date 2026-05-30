using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AdventCalendarSeasonPlanServiceTests
{
	[Theory]
	[InlineData(12, 1)]
	[InlineData(12, 15)]
	[InlineData(12, 24)]
	public void CreatePlan_DecemberDay1To24IsAdventSeason(int month, int day)
	{
		var plan = AdventCalendarSeasonPlanService.CreatePlan(new DateOnly(2026, month, day));

		Assert.True(plan.IsAdventSeason);
		Assert.False(plan.IsLive);
	}

	[Theory]
	[InlineData(12, 25)]  // Christmas Day = not advent (December 25+)
	[InlineData(12, 31)]
	[InlineData(11, 30)]  // November = not advent
	[InlineData(1, 1)]    // January = not advent
	public void CreatePlan_OutsideAdventSeasonIsFalse(int month, int day)
	{
		var plan = AdventCalendarSeasonPlanService.CreatePlan(new DateOnly(2026, month, day));

		Assert.False(plan.IsAdventSeason);
	}

	[Fact]
	public void AdventEndDayConstantIs24()
	{
		Assert.Equal(24, AdventCalendarSeasonPlanService.AdventEndDay);
	}

	[Fact]
	public void IsAdventSeason_StaticMethodWorks()
	{
		Assert.True(AdventCalendarSeasonPlanService.IsAdventSeason(new DateOnly(2026, 12, 1)));
		Assert.False(AdventCalendarSeasonPlanService.IsAdventSeason(new DateOnly(2026, 12, 25)));
	}
}
