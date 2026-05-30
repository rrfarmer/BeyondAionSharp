using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class GameTimeDayCalculationServiceTests
{
	// --- getHour ---

	[Fact]
	public void GetHour_MidnightIsZero()
	{
		Assert.Equal(0, GameTimeDayCalculationService.GetHour(0));
	}

	[Fact]
	public void GetHour_OneHourIs1()
	{
		Assert.Equal(1, GameTimeDayCalculationService.GetHour(60));
	}

	[Fact]
	public void GetHour_WrapsByDay()
	{
		// gameTime % 1440 / 60 → hour 0 at start of day 2
		Assert.Equal(0, GameTimeDayCalculationService.GetHour(1440));
	}

	[Fact]
	public void GetHour_Hour23IsCorrect()
	{
		// 23*60 = 1380, 1380 % 1440 / 60 = 23
		Assert.Equal(23, GameTimeDayCalculationService.GetHour(23 * 60));
	}

	// --- getMinute ---

	[Fact]
	public void GetMinute_ZeroMinutesIsZero()
	{
		Assert.Equal(0, GameTimeDayCalculationService.GetMinute(0));
	}

	[Fact]
	public void GetMinute_30MinutesIs30()
	{
		Assert.Equal(30, GameTimeDayCalculationService.GetMinute(90)); // 90 % 60 = 30
	}

	// --- calculateDayTime boundaries ---

	[Theory]
	[InlineData(22, GameDayTime.Night)]  // 22 > 21
	[InlineData(23, GameDayTime.Night)]
	[InlineData(0, GameDayTime.Night)]   // 0 < 4
	[InlineData(3, GameDayTime.Night)]   // 3 < 4
	public void CalculateDayTime_NightBoundaries(int hour, GameDayTime expected)
	{
		Assert.Equal(expected, GameTimeDayCalculationService.CalculateDayTime(hour));
	}

	[Theory]
	[InlineData(17, GameDayTime.Evening)]  // 17 > 16
	[InlineData(20, GameDayTime.Evening)]
	[InlineData(21, GameDayTime.Evening)]  // 21 not > 21 but > 16
	public void CalculateDayTime_EveningBoundaries(int hour, GameDayTime expected)
	{
		Assert.Equal(expected, GameTimeDayCalculationService.CalculateDayTime(hour));
	}

	[Theory]
	[InlineData(9, GameDayTime.Afternoon)]   // 9 > 8
	[InlineData(12, GameDayTime.Afternoon)]
	[InlineData(16, GameDayTime.Afternoon)]  // 16 not > 16
	public void CalculateDayTime_AfternoonBoundaries(int hour, GameDayTime expected)
	{
		Assert.Equal(expected, GameTimeDayCalculationService.CalculateDayTime(hour));
	}

	[Theory]
	[InlineData(4, GameDayTime.Morning)]  // 4 not < 4, not > 16, not > 8
	[InlineData(8, GameDayTime.Morning)]  // 8 not > 8
	public void CalculateDayTime_MorningBoundaries(int hour, GameDayTime expected)
	{
		Assert.Equal(expected, GameTimeDayCalculationService.CalculateDayTime(hour));
	}

	// --- full plan ---

	[Fact]
	public void CreatePlan_ComputesFullDateTimeFromGameTimeMinutes()
	{
		// 9*60 + 30 = 570 minutes = year 0, month 1, day 1, hour 9, minute 30 → AFTERNOON
		var plan = GameTimeDayCalculationService.CreatePlan(570);

		Assert.Equal(0, plan.Year);
		Assert.Equal(1, plan.Month);
		Assert.Equal(1, plan.Day);
		Assert.Equal(9, plan.Hour);
		Assert.Equal(30, plan.Minute);
		Assert.Equal(GameDayTime.Afternoon, plan.DayTime);
		Assert.False(plan.IsLive);
	}

	// --- getYear ---

	[Fact]
	public void GetYear_ZeroMinutesIsYear0()
	{
		Assert.Equal(0, GameTimeDayCalculationService.GetYear(0));
	}

	[Fact]
	public void GetYear_OnceAroundIsYear1()
	{
		// MINUTES_IN_YEAR = 372 * 1440 = 535680
		Assert.Equal(1, GameTimeDayCalculationService.GetYear(GameTimeDayCalculationService.MinutesInYear));
	}

	// --- getMonth ---

	[Fact]
	public void GetMonth_StartOfYearIsMonth1()
	{
		Assert.Equal(1, GameTimeDayCalculationService.GetMonth(0));
	}

	[Fact]
	public void GetMonth_AfterFirstMonthIsMonth2()
	{
		// First month = 31 days = 31 * 1440 = 44640 minutes
		Assert.Equal(2, GameTimeDayCalculationService.GetMonth(31 * GameTimeDayCalculationService.MinutesInDay));
	}

	[Fact]
	public void GetMonth_LastMonthIsMonth12()
	{
		// 11 months into year = month 12
		Assert.Equal(12, GameTimeDayCalculationService.GetMonth(11 * 31 * GameTimeDayCalculationService.MinutesInDay));
	}

	// --- getDay ---

	[Fact]
	public void GetDay_StartOfMonthIsDay1()
	{
		Assert.Equal(1, GameTimeDayCalculationService.GetDay(0));
	}

	[Fact]
	public void GetDay_OneFullDayIntoMonthIsDay2()
	{
		Assert.Equal(2, GameTimeDayCalculationService.GetDay(GameTimeDayCalculationService.MinutesInDay));
	}

	[Fact]
	public void GetDay_CalendarConstantsAreCorrect()
	{
		Assert.Equal(31, GameTimeCalendar.DaysPerMonth);
		Assert.Equal(12, GameTimeCalendar.MonthsPerYear);
		Assert.Equal(372, GameTimeCalendar.DaysPerYear);
		Assert.Equal(535680, GameTimeDayCalculationService.MinutesInYear);
	}
}
