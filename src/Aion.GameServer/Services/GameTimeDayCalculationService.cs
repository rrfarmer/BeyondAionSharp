namespace Aion.GameServer.Services;

public enum GameDayTime
{
	Morning,
	Afternoon,
	Evening,
	Night,
}

public sealed record GameTimeDayCalculationPlan(
	int GameTimeMinutes,
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

	public static GameTimeDayCalculationPlan CreatePlan(int gameTimeMinutes)
	{
		// Java parity: utils/time/gametime/GameTime.getHour, getMinute, calculateDayTime.
		var hour = GetHour(gameTimeMinutes);
		var minute = GetMinute(gameTimeMinutes);
		var dayTime = CalculateDayTime(hour);

		return new GameTimeDayCalculationPlan(
			gameTimeMinutes,
			hour,
			minute,
			dayTime,
			$"GameTime.calculateDayTime -> hour={hour} -> {dayTime}"
		);
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
