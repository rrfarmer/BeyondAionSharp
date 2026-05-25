using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestRepeatDateServiceTests
{
	private static readonly TimeZoneInfo ServerTimeZone = TimeZoneInfo.CreateCustomTimeZone(
		"JavaServerTime",
		TimeSpan.FromHours(2),
		"Java Server Time",
		"Java Server Time");

	[Theory]
	[InlineData(8, 59, 25)]
	[InlineData(9, 0, 25)]
	[InlineData(9, 1, 26)]
	public void CalculateNextRepeatTime_AppliesJavaDailyNineAmReset(int hour, int minute, int expectedDay)
	{
		var now = new DateTimeOffset(2026, 5, 25, hour, minute, 0, ServerTimeZone.BaseUtcOffset);

		var nextRepeat = QuestRepeatDateService.CalculateNextRepeatTime(now, ["ALL"], ServerTimeZone);

		Assert.Equal(new DateTimeOffset(2026, 5, expectedDay, 9, 0, 0, ServerTimeZone.BaseUtcOffset), nextRepeat);
	}

	[Fact]
	public void CalculateNextRepeatTime_TreatsAllAsDailyEvenWhenWeekdaysArePresent()
	{
		var now = new DateTimeOffset(2026, 5, 25, 10, 0, 0, ServerTimeZone.BaseUtcOffset);

		var nextRepeat = QuestRepeatDateService.CalculateNextRepeatTime(now, ["ALL", "WED"], ServerTimeZone);

		Assert.Equal(new DateTimeOffset(2026, 5, 26, 9, 0, 0, ServerTimeZone.BaseUtcOffset), nextRepeat);
	}

	[Theory]
	[InlineData(2026, 5, 25, 8, 0, 2026, 5, 25)] // Monday before reset: same configured weekday.
	[InlineData(2026, 5, 25, 10, 0, 2026, 5, 27)] // Monday after reset: candidate Tuesday, then Wednesday.
	[InlineData(2026, 5, 27, 10, 0, 2026, 6, 1)] // Wednesday after reset: candidate Thursday, then wraps to Monday.
	[InlineData(2026, 5, 31, 8, 0, 2026, 6, 1)] // Sunday before reset: wraps to Monday.
	public void CalculateNextRepeatTime_AppliesJavaWeeklySelectionFromResetCandidate(
		int year,
		int month,
		int day,
		int hour,
		int minute,
		int expectedYear,
		int expectedMonth,
		int expectedDay)
	{
		var now = new DateTimeOffset(year, month, day, hour, minute, 0, ServerTimeZone.BaseUtcOffset);

		var nextRepeat = QuestRepeatDateService.CalculateNextRepeatTime(now, ["MON", "WED"], ServerTimeZone);

		Assert.Equal(new DateTimeOffset(expectedYear, expectedMonth, expectedDay, 9, 0, 0, ServerTimeZone.BaseUtcOffset), nextRepeat);
	}

	[Fact]
	public void CalculateNextRepeatTime_RequiresWeekdayForWeeklyCycles()
	{
		var now = new DateTimeOffset(2026, 5, 25, 10, 0, 0, ServerTimeZone.BaseUtcOffset);

		var exception = Assert.Throws<ArgumentException>(() =>
			QuestRepeatDateService.CalculateNextRepeatTime(now, [], ServerTimeZone));

		Assert.Contains("Weekly repeat cycle requires", exception.Message);
	}
}
