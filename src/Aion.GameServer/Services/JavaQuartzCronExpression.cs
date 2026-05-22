namespace Aion.GameServer.Services;

public sealed class JavaQuartzCronExpression
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

	private JavaQuartzCronExpression(
		string expression,
		IReadOnlyList<int> seconds,
		IReadOnlyList<int> minutes,
		IReadOnlyList<int> hours,
		IReadOnlySet<DayOfWeek> daysOfWeek)
	{
		Expression = expression;
		Seconds = seconds;
		Minutes = minutes;
		Hours = hours;
		DaysOfWeek = daysOfWeek;
	}

	public string Expression { get; }

	public IReadOnlyList<int> Seconds { get; }

	public IReadOnlyList<int> Minutes { get; }

	public IReadOnlyList<int> Hours { get; }

	public IReadOnlySet<DayOfWeek> DaysOfWeek { get; }

	public static bool TryParse(string? expression, out JavaQuartzCronExpression cronExpression)
	{
		cronExpression = new JavaQuartzCronExpression(
			string.Empty,
			[0],
			[0],
			[0],
			new HashSet<DayOfWeek>());
		if (string.IsNullOrWhiteSpace(expression))
			return false;

		var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (parts.Length != 6)
			return false;
		if (parts[3] is not ("?" or "*") || parts[4] != "*")
			return false;
		if (!TryParseIntList(parts[0], 0, 59, out var seconds)
			|| !TryParseIntList(parts[1], 0, 59, out var minutes)
			|| !TryParseIntList(parts[2], 0, 23, out var hours)
			|| !TryParseDaysOfWeek(parts[5], out var daysOfWeek))
			return false;

		cronExpression = new JavaQuartzCronExpression(expression, seconds, minutes, hours, daysOfWeek);
		return true;
	}

	public DateTimeOffset GetNextRunAfter(DateTimeOffset date)
	{
		// Java parity: services/cron/CronService delegates Quartz CronExpression.getTimeAfter, which is strictly after the input time.
		for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
		{
			var day = date.Date.AddDays(dayOffset);
			if (!DaysOfWeek.Contains(day.DayOfWeek))
				continue;

			foreach (var hour in Hours)
			foreach (var minute in Minutes)
			foreach (var second in Seconds)
			{
				var candidate = new DateTimeOffset(
					day.Year,
					day.Month,
					day.Day,
					hour,
					minute,
					second,
					date.Offset);
				if (candidate > date)
					return candidate;
			}
		}

		throw new InvalidOperationException($"Cron expression {Expression} did not produce a next run.");
	}

	private static bool TryParseIntList(string value, int min, int max, out IReadOnlyList<int> values)
	{
		values = Array.Empty<int>();
		if (value == "*")
		{
			values = Enumerable.Range(min, max - min + 1).ToArray();
			return true;
		}

		var parsed = new SortedSet<int>();
		foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (!int.TryParse(part, out var number) || number < min || number > max)
				return false;
			parsed.Add(number);
		}

		if (parsed.Count == 0)
			return false;

		values = parsed.ToArray();
		return true;
	}

	private static bool TryParseDaysOfWeek(string value, out IReadOnlySet<DayOfWeek> daysOfWeek)
	{
		daysOfWeek = new HashSet<DayOfWeek>();
		if (value == "*")
		{
			daysOfWeek = Enum.GetValues<DayOfWeek>().ToHashSet();
			return true;
		}

		var parsed = new HashSet<DayOfWeek>();
		foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (DayNames.TryGetValue(part, out var namedDay))
			{
				parsed.Add(namedDay);
				continue;
			}

			if (!int.TryParse(part, out var numericDay) || numericDay is < 1 or > 7)
				return false;
			parsed.Add((DayOfWeek)(numericDay - 1));
		}

		if (parsed.Count == 0)
			return false;

		daysOfWeek = parsed;
		return true;
	}
}
