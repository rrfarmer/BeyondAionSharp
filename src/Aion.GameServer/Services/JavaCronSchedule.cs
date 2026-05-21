namespace Aion.GameServer.Services;

public sealed class JavaCronSchedule
{
	private static readonly IReadOnlyDictionary<string, DayOfWeek> DayNames = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
	{
		["SUN"] = DayOfWeek.Sunday,
		["MON"] = DayOfWeek.Monday,
		["TUE"] = DayOfWeek.Tuesday,
		["WED"] = DayOfWeek.Wednesday,
		["THU"] = DayOfWeek.Thursday,
		["FRI"] = DayOfWeek.Friday,
		["SAT"] = DayOfWeek.Saturday,
	};

	private JavaCronSchedule(DayOfWeek dayOfWeek, int hour, int minute, int second)
	{
		DayOfWeek = dayOfWeek;
		Hour = hour;
		Minute = minute;
		Second = second;
	}

	public DayOfWeek DayOfWeek { get; }

	public int Hour { get; }

	public int Minute { get; }

	public int Second { get; }

	public static JavaCronSchedule WeeklyOrDefault(
		string? expression,
		DayOfWeek defaultDayOfWeek,
		int defaultHour,
		int defaultMinute = 0,
		int defaultSecond = 0)
	{
		// Java parity: configs/main/HousingConfig CronExpression weekly values such as "0 0 12 ? * SUN".
		return TryParseWeekly(expression, out var schedule)
			? schedule
			: new JavaCronSchedule(defaultDayOfWeek, defaultHour, defaultMinute, defaultSecond);
	}

	public DateTimeOffset GetNextRunAfter(DateTimeOffset date)
	{
		// Java parity: taskmanager/AbstractCronTask.getNextRunAfter delegates to Quartz CronExpression.getTimeAfter.
		var daysUntilRun = ((int)DayOfWeek - (int)date.DayOfWeek + 7) % 7;
		var nextRun = new DateTimeOffset(date.Year, date.Month, date.Day, Hour, Minute, Second, date.Offset).AddDays(daysUntilRun);
		if (nextRun <= date)
			nextRun = nextRun.AddDays(7);
		return nextRun;
	}

	public DateTimeOffset GetPreviousRunBefore(DateTimeOffset date)
	{
		// Java parity: taskmanager/AbstractCronTask.findLastPlannedRun for weekly housing cron schedules.
		var daysSinceRun = ((int)date.DayOfWeek - (int)DayOfWeek + 7) % 7;
		var previousRun = new DateTimeOffset(date.Year, date.Month, date.Day, Hour, Minute, Second, date.Offset).AddDays(-daysSinceRun);
		if (previousRun >= date)
			previousRun = previousRun.AddDays(-7);
		return previousRun;
	}

	public DateTime GetNextRunAfter(DateTime date)
	{
		return GetNextRunAfter(new DateTimeOffset(date)).DateTime;
	}

	private static bool TryParseWeekly(string? expression, out JavaCronSchedule schedule)
	{
		schedule = new JavaCronSchedule(DayOfWeek.Sunday, 12, 0, 0);
		if (string.IsNullOrWhiteSpace(expression))
			return false;

		var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length != 6)
			return false;
		if (parts[3] is not ("?" or "*") || parts[4] != "*")
			return false;
		if (!TryParseNumber(parts[0], 0, 59, out var second)
			|| !TryParseNumber(parts[1], 0, 59, out var minute)
			|| !TryParseNumber(parts[2], 0, 23, out var hour)
			|| !TryParseDayOfWeek(parts[5], out var dayOfWeek))
			return false;

		schedule = new JavaCronSchedule(dayOfWeek, hour, minute, second);
		return true;
	}

	private static bool TryParseNumber(string value, int min, int max, out int result)
	{
		if (!int.TryParse(value, out result))
			return false;
		return result >= min && result <= max;
	}

	private static bool TryParseDayOfWeek(string value, out DayOfWeek dayOfWeek)
	{
		if (DayNames.TryGetValue(value, out dayOfWeek))
			return true;
		if (!int.TryParse(value, out var numericDay))
			return false;
		if (numericDay is < 1 or > 7)
			return false;

		dayOfWeek = (DayOfWeek)(numericDay - 1);
		return true;
	}
}
