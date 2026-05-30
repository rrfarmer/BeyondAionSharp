namespace Aion.GameServer.Services;

// Java parity: utils/time/gametime/GameTime.Month enum — all 12 months have 31 days.
public static class GameTimeCalendar
{
	public const int DaysPerMonth = 31;
	public const int MonthsPerYear = 12;
	public const int DaysPerYear = DaysPerMonth * MonthsPerYear; // 372
}

public enum GameDayTime
{
	Morning,
	Afternoon,
	Evening,
	Night,
}

public sealed record GameTimeDayCalculationPlan(
	int GameTimeMinutes,
	int Year,
	int Month,
	int Day,
	int Hour,
	int Minute,
	GameDayTime DayTime,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class GameTimeDayCalculationService
{
	// Java parity: utils/time/gametime/GameTime constants.
	public const int MinutesInHour = 60;
	public const int MinutesInDay = MinutesInHour * 24;
	public const int MinutesInYear = GameTimeCalendar.DaysPerYear * MinutesInDay; // 372 * 1440 = 535680

	public static GameTimeDayCalculationPlan CreatePlan(int gameTimeMinutes)
	{
		// Java parity: utils/time/gametime/GameTime.getHour, getMinute, calculateDayTime.
		var year = GetYear(gameTimeMinutes);
		var month = GetMonth(gameTimeMinutes);
		var day = GetDay(gameTimeMinutes);
		var hour = GetHour(gameTimeMinutes);
		var minute = GetMinute(gameTimeMinutes);
		var dayTime = CalculateDayTime(hour);

		return new GameTimeDayCalculationPlan(
			gameTimeMinutes,
			year, month, day,
			hour, minute,
			dayTime,
			$"GameTime -> year={year} month={month} day={day} hour={hour} minute={minute} dayTime={dayTime}"
		);
	}

	public static int GetYear(int gameTimeMinutes)
	{
		// Java parity: gameTime / MINUTES_IN_YEAR.
		return gameTimeMinutes / MinutesInYear;
	}

	public static int GetMonth(int gameTimeMinutes)
	{
		// Java parity: GameTime.getMonth iterates 12 months (each 31 days) subtracting from minutesOfThisYear.
		var minutesOfThisYear = gameTimeMinutes % MinutesInYear;
		for (var month = 1; month <= GameTimeCalendar.MonthsPerYear; month++)
		{
			minutesOfThisYear -= GameTimeCalendar.DaysPerMonth * MinutesInDay;
			if (minutesOfThisYear < 0)
				return month;
		}
		return GameTimeCalendar.MonthsPerYear;
	}

	public static int GetDay(int gameTimeMinutes)
	{
		// Java parity: GameTime.getDay iterates 12 months accumulating days.
		var minutesInYear = gameTimeMinutes % MinutesInYear;
		for (var month = 0; month < GameTimeCalendar.MonthsPerYear; month++)
		{
			var minutesInMonth = GameTimeCalendar.DaysPerMonth * MinutesInDay;
			if (minutesInYear > minutesInMonth)
			{
				minutesInYear -= minutesInMonth;
			}
			else
			{
				if (minutesInYear < minutesInMonth)
					return 1 + minutesInYear / MinutesInDay;
				return 1; // exactly at month boundary = day 1 of next month
			}
		}
		return 1;
	}

	public static int GetHour(int gameTimeMinutes)
	{
		// Java parity: (gameTime % MINUTES_IN_DAY) / MINUTES_IN_HOUR.
		return (gameTimeMinutes % MinutesInDay) / MinutesInHour;
	}

	public static int GetMinute(int gameTimeMinutes)
	{
		// Java parity: gameTime % MINUTES_IN_HOUR.
		return gameTimeMinutes % MinutesInHour;
	}

	public static GameDayTime CalculateDayTime(int hour)
	{
		// Java parity: utils/time/gametime/GameTime.calculateDayTime.
		// hour > 21 || hour < 4 → NIGHT
		// hour > 16 → EVENING
		// hour > 8 → AFTERNOON
		// else → MORNING (includes 0-8)
		if (hour > 21 || hour < 4)
			return GameDayTime.Night;
		if (hour > 16)
			return GameDayTime.Evening;
		if (hour > 8)
			return GameDayTime.Afternoon;
		return GameDayTime.Morning;
	}
}
