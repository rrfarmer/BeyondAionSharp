using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class TemporarySpawnScheduleTests
{
	[Fact]
	public void IsInSpawnTime_MatchesOvernightHourWindow()
	{
		var schedule = TemporarySpawnSchedule.FromAttributes(null, "21.*.*", "4.*.*");

		Assert.True(schedule.IsInSpawnTime(GameMinutes(hour: 21), DayOfWeek.Friday));
		Assert.True(schedule.IsInSpawnTime(GameMinutes(hour: 2), DayOfWeek.Friday));
		Assert.False(schedule.IsInSpawnTime(GameMinutes(hour: 4), DayOfWeek.Friday));
		Assert.False(schedule.IsInSpawnTime(GameMinutes(hour: 20), DayOfWeek.Friday));
	}

	[Fact]
	public void IsInSpawnTime_MatchesMonthAndDayWindow()
	{
		var schedule = TemporarySpawnSchedule.FromAttributes(null, "0.15.09", "0.18.09");

		Assert.False(schedule.IsInSpawnTime(GameMinutes(month: 9, day: 14), DayOfWeek.Friday));
		Assert.True(schedule.IsInSpawnTime(GameMinutes(month: 9, day: 15), DayOfWeek.Friday));
		Assert.True(schedule.IsInSpawnTime(GameMinutes(month: 9, day: 18), DayOfWeek.Friday));
		Assert.False(schedule.IsInSpawnTime(GameMinutes(month: 9, day: 19), DayOfWeek.Friday));
	}

	[Fact]
	public void CanSpawn_MatchesEveryNthHourExpression()
	{
		var schedule = TemporarySpawnSchedule.FromAttributes(null, "/3.*.*", "/3.*.*");

		Assert.True(schedule.CanSpawn(GameMinutes(hour: 6), DayOfWeek.Friday));
		Assert.True(schedule.CanDespawn(GameMinutes(hour: 6), DayOfWeek.Friday));
		Assert.False(schedule.CanSpawn(GameMinutes(hour: 5), DayOfWeek.Friday));
		Assert.False(schedule.CanDespawn(GameMinutes(hour: 5), DayOfWeek.Friday));
	}

	[Fact]
	public void IsInSpawnTime_HonorsServerWeekdayMask()
	{
		var schedule = TemporarySpawnSchedule.FromAttributes("Monday Wednesday", "*.*.*", "*.*.*");

		Assert.True(schedule.IsInSpawnTime(0, DayOfWeek.Monday));
		Assert.True(schedule.IsInSpawnTime(0, DayOfWeek.Wednesday));
		Assert.False(schedule.IsInSpawnTime(0, DayOfWeek.Tuesday));
	}

	private static int GameMinutes(int month = 1, int day = 1, int hour = 0)
	{
		return (((month - 1) * 31) + (day - 1)) * 24 * 60 + hour * 60;
	}
}
