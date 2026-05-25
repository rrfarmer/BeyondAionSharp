using Aion.GameServer.Configuration;

namespace Aion.GameServer.Services;

public static class QuestRepeatDateService
{
	private const int ResetHour = 9;

	public static DateTimeOffset CalculateNextRepeatTime(
		DateTimeOffset now,
		IReadOnlyList<string> repeatCycle,
		GameServerOptions options)
	{
		return CalculateNextRepeatTime(now, repeatCycle, options.Core.GetTimeZone());
	}

	public static DateTimeOffset CalculateNextRepeatTime(
		DateTimeOffset now,
		IReadOnlyList<string> repeatCycle,
		TimeZoneInfo serverTimeZone)
	{
		// Java parity breadcrumb: QuestService.calculateRepeatDate uses ServerTime.now()
		// and the next server-time 09:00 reset before applying weekly repeat-cycle days.
		var serverNow = TimeZoneInfo.ConvertTime(now, serverTimeZone);
		var repeatDate = AtServerReset(serverNow.Date, serverTimeZone);
		if (serverNow > repeatDate)
			repeatDate = AtServerReset(serverNow.Date.AddDays(1), serverTimeZone);

		if (repeatCycle.Contains("ALL", StringComparer.Ordinal))
			return repeatDate;

		var repeatDays = repeatCycle
			.Select(ToJavaDayOfWeek)
			.Where(day => day > 0)
			.Order()
			.ToArray();
		if (repeatDays.Length == 0)
			throw new ArgumentException("Weekly repeat cycle requires at least one weekday token.", nameof(repeatCycle));

		var baseDay = ToJavaDayOfWeek(repeatDate.DayOfWeek);
		var nextRepeatDay = repeatDays.FirstOrDefault(day => day >= baseDay);
		var daysToAdd = nextRepeatDay == 0
			? 7 - baseDay + repeatDays[0]
			: nextRepeatDay - baseDay;
		return AtServerReset(repeatDate.Date.AddDays(daysToAdd), serverTimeZone);
	}

	private static DateTimeOffset AtServerReset(DateTime date, TimeZoneInfo serverTimeZone)
	{
		var localReset = new DateTime(date.Year, date.Month, date.Day, ResetHour, 0, 0, DateTimeKind.Unspecified);
		return new DateTimeOffset(localReset, serverTimeZone.GetUtcOffset(localReset));
	}

	private static int ToJavaDayOfWeek(DayOfWeek day)
	{
		return day == DayOfWeek.Sunday ? 7 : (int) day;
	}

	private static int ToJavaDayOfWeek(string token)
	{
		return token switch
		{
			"MON" => 1,
			"TUE" => 2,
			"WED" => 3,
			"THU" => 4,
			"FRI" => 5,
			"SAT" => 6,
			"SUN" => 7,
			_ => 0,
		};
	}
}
