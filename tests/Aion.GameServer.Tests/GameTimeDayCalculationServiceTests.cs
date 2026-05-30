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
	public void CreatePlan_ComputesHourMinuteDayTimeFromGameTimeMinutes()
	{
		// 9*60 + 30 = 570 minutes = hour 9, minute 30 → AFTERNOON
		var plan = GameTimeDayCalculationService.CreatePlan(570);

		Assert.Equal(9, plan.Hour);
		Assert.Equal(30, plan.Minute);
		Assert.Equal(GameDayTime.Afternoon, plan.DayTime);
		Assert.False(plan.IsLive);
	}
}
