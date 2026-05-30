using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AtreianPassportAttendPlanServiceTests
{
	// --- getAttendDay ---

	[Fact]
	public void CreateAttendDayPlan_BeforeResetHourUsesYesterday()
	{
		// 8:00 AM on May 10 → minusHours(9) = 23:00 on May 9 → date = May 9
		var time = new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero);
		var plan = AtreianPassportAttendPlanService.CreateAttendDayPlan(time);

		Assert.Equal(new DateOnly(2026, 5, 9), plan.AttendDay);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateAttendDayPlan_AfterResetHourUsesToday()
	{
		// 10:00 AM on May 10 → minusHours(9) = 1:00 on May 10 → date = May 10
		var time = new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero);
		var plan = AtreianPassportAttendPlanService.CreateAttendDayPlan(time);

		Assert.Equal(new DateOnly(2026, 5, 10), plan.AttendDay);
	}

	[Fact]
	public void AttendResetHourConstantIs9()
	{
		Assert.Equal(9, AtreianPassportAttendPlanService.AttendResetHour);
	}

	// --- checkOnlineDate ---

	[Fact]
	public void CreateCheckOnlineDatePlan_NullLastStampAlwaysRewards()
	{
		var plan = AtreianPassportAttendPlanService.CreateCheckOnlineDatePlan(
			lastStamp: null, now: DateTimeOffset.UtcNow);

		Assert.True(plan.ShouldReward);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateCheckOnlineDatePlan_DifferentDayRewards()
	{
		// Last stamp on May 9, current time on May 10 (both after 9am)
		var last = new DateTimeOffset(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);
		var now = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

		var plan = AtreianPassportAttendPlanService.CreateCheckOnlineDatePlan(last, now);

		Assert.True(plan.ShouldReward);
	}

	[Fact]
	public void CreateCheckOnlineDatePlan_SameDayDoesNotReward()
	{
		// Both on May 10 (after 9am) → same attend day = no reward
		var last = new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero);
		var now = new DateTimeOffset(2026, 5, 10, 15, 0, 0, TimeSpan.Zero);

		var plan = AtreianPassportAttendPlanService.CreateCheckOnlineDatePlan(last, now);

		Assert.False(plan.ShouldReward);
	}

	// --- getAccountAgeInMonths ---

	[Fact]
	public void CreateAccountAgeInMonthsPlan_SameMonthIsZero()
	{
		var plan = AtreianPassportAttendPlanService.CreateAccountAgeInMonthsPlan(
			new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 15));

		Assert.Equal(0, plan.Months);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreateAccountAgeInMonthsPlan_TwelveMonthsIsOneYear()
	{
		var plan = AtreianPassportAttendPlanService.CreateAccountAgeInMonthsPlan(
			new DateOnly(2025, 5, 1), new DateOnly(2026, 5, 1));

		Assert.Equal(12, plan.Months);
	}

	[Fact]
	public void CreateAccountAgeInMonthsPlan_DayOfMonthAdjustment()
	{
		// Creation May 15, now May 10 → day 10 < day 15 → subtract 1 → 11 months
		var plan = AtreianPassportAttendPlanService.CreateAccountAgeInMonthsPlan(
			new DateOnly(2025, 6, 15), new DateOnly(2026, 6, 10));

		Assert.Equal(11, plan.Months);
	}

	[Fact]
	public void CreateAccountAgeInMonthsPlan_NegativeClampedToZero()
	{
		// Creation in future relative to now
		var plan = AtreianPassportAttendPlanService.CreateAccountAgeInMonthsPlan(
			new DateOnly(2026, 6, 1), new DateOnly(2026, 5, 1));

		Assert.Equal(0, plan.Months);
	}
}
