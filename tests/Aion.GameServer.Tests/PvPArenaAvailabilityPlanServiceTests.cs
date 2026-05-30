using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PvPArenaAvailabilityPlanServiceTests
{
	// --- FFA/Solo availability ---

	[Theory]
	[InlineData(0, 1)]  // weekday midnight
	[InlineData(1, 2)]  // weekday 1am
	[InlineData(12, 3)] // weekday noon
	[InlineData(18, 4)] // weekday 6pm
	[InlineData(23, 5)] // weekday 11pm
	[InlineData(10, 6)] // Saturday 10am
	[InlineData(10, 7)] // Sunday 10am
	public void FfaArena_AvailableAtExpectedHours(int hour, int day)
	{
		var plan = PvPArenaAvailabilityPlanService.CreateAvailabilityPlan(PvPArenaType.FfaOrSolo, hour, day);
		Assert.True(plan.IsAvailable);
		Assert.False(plan.IsLive);
	}

	[Theory]
	[InlineData(9, 1)]  // weekday 9am
	[InlineData(17, 2)] // weekday 5pm
	[InlineData(3, 5)]  // weekday 3am
	public void FfaArena_UnavailableAtOffHours(int hour, int day)
	{
		var plan = PvPArenaAvailabilityPlanService.CreateAvailabilityPlan(PvPArenaType.FfaOrSolo, hour, day);
		Assert.False(plan.IsAvailable);
	}

	// --- Glory arena ---

	[Theory]
	[InlineData(20, 6)] // Saturday 8pm
	[InlineData(21, 7)] // Sunday 9pm
	public void GloryArena_AvailableWeekendEvenings(int hour, int day)
	{
		var plan = PvPArenaAvailabilityPlanService.CreateAvailabilityPlan(PvPArenaType.Glory, hour, day);
		Assert.True(plan.IsAvailable);
	}

	[Theory]
	[InlineData(20, 1)] // weekday - unavailable
	[InlineData(22, 6)] // Saturday 10pm - too late (hour < 22, not <=)
	[InlineData(19, 7)] // Sunday 7pm - too early
	public void GloryArena_UnavailableOutsideWindow(int hour, int day)
	{
		var plan = PvPArenaAvailabilityPlanService.CreateAvailabilityPlan(PvPArenaType.Glory, hour, day);
		Assert.False(plan.IsAvailable);
	}

	// --- Item requirements ---

	[Fact]
	public void ItemRequirement_FfaOrSoloNeedsItem186000135()
	{
		var plan = PvPArenaAvailabilityPlanService.CreateItemRequirementPlan(PvPArenaType.FfaOrSolo);
		Assert.True(plan.HasRequirement);
		Assert.Equal(186000135, plan.RequiredItemId);
		Assert.Equal(1, plan.RequiredCount);
	}

	[Fact]
	public void ItemRequirement_GloryNeedsThreeOf186000185()
	{
		var plan = PvPArenaAvailabilityPlanService.CreateItemRequirementPlan(PvPArenaType.Glory);
		Assert.Equal(186000185, plan.RequiredItemId);
		Assert.Equal(3, plan.RequiredCount);
	}

	[Fact]
	public void ItemRequirement_OtherHasNoRequirement()
	{
		var plan = PvPArenaAvailabilityPlanService.CreateItemRequirementPlan(PvPArenaType.Other);
		Assert.False(plan.HasRequirement);
		Assert.Equal(0, plan.RequiredItemId);
	}
}
