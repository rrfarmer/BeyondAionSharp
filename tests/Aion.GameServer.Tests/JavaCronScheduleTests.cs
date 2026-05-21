using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class JavaCronScheduleTests
{
	[Fact]
	public void WeeklyOrDefault_ParsesNamedQuartzDay()
	{
		var schedule = JavaCronSchedule.WeeklyOrDefault("15 30 18 ? * TUE", DayOfWeek.Monday, 0);

		var nextRun = schedule.GetNextRunAfter(new DateTimeOffset(2026, 5, 19, 18, 0, 0, TimeSpan.Zero));

		Assert.Equal(DayOfWeek.Tuesday, schedule.DayOfWeek);
		Assert.Equal(18, schedule.Hour);
		Assert.Equal(30, schedule.Minute);
		Assert.Equal(15, schedule.Second);
		Assert.Equal(new DateTimeOffset(2026, 5, 19, 18, 30, 15, TimeSpan.Zero), nextRun);
	}

	[Fact]
	public void WeeklyOrDefault_ParsesQuartzNumericDay()
	{
		var schedule = JavaCronSchedule.WeeklyOrDefault("0 45 9 ? * 6", DayOfWeek.Monday, 0);

		var nextRun = schedule.GetNextRunAfter(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));

		Assert.Equal(DayOfWeek.Friday, schedule.DayOfWeek);
		Assert.Equal(new DateTimeOffset(2026, 5, 22, 9, 45, 0, TimeSpan.Zero), nextRun);
	}

	[Fact]
	public void GetNextRunAfter_IsStrictlyAfterInputTime()
	{
		var schedule = JavaCronSchedule.WeeklyOrDefault("0 0 12 ? * SUN", DayOfWeek.Monday, 0);

		var nextRun = schedule.GetNextRunAfter(new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero));

		Assert.Equal(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero), nextRun);
	}

	[Fact]
	public void WeeklyOrDefault_FallsBackForUnsupportedExpression()
	{
		var schedule = JavaCronSchedule.WeeklyOrDefault("bad cron", DayOfWeek.Wednesday, 3, 15, 30);

		var nextRun = schedule.GetNextRunAfter(new DateTimeOffset(2026, 5, 21, 0, 0, 0, TimeSpan.Zero));

		Assert.Equal(DayOfWeek.Wednesday, schedule.DayOfWeek);
		Assert.Equal(new DateTimeOffset(2026, 5, 27, 3, 15, 30, TimeSpan.Zero), nextRun);
	}
}
