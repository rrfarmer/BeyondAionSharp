namespace Aion.GameServer.Services;

public sealed record AtreianPassportAttendDayPlan(
	DateTimeOffset ServerTime,
	DateOnly AttendDay,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record AtreianPassportCheckOnlineDatePlan(
	bool ShouldReward,
	DateOnly LastAttendDay,
	DateOnly CurrentAttendDay,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record AccountAgeInMonthsPlan(
	int Months,
	DateOnly CreationDate,
	DateOnly NowDate,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class AtreianPassportAttendPlanService
{
	// Java parity: AtreianPassportService.ATTEND_RESET_HOUR = 9.
	public const int AttendResetHour = 9;

	public static AtreianPassportAttendDayPlan CreateAttendDayPlan(DateTimeOffset serverTime)
	{
		// Java parity: AtreianPassportService.getAttendDay(LocalDateTime).
		// return serverTime.minusHours(ATTEND_RESET_HOUR).toLocalDate()
		var attendDate = DateOnly.FromDateTime(serverTime.AddHours(-AttendResetHour).DateTime);
		return new AtreianPassportAttendDayPlan(
			serverTime, attendDate,
			$"AtreianPassportService.getAttendDay -> serverTime={serverTime:O}.minusHours({AttendResetHour}) -> {attendDate}"
		);
	}

	public static AtreianPassportCheckOnlineDatePlan CreateCheckOnlineDatePlan(
		DateTimeOffset? lastStamp,
		DateTimeOffset now)
	{
		// Java parity: AtreianPassportService.checkOnlineDate.
		// null lastStamp → always reward; else check if attend days differ.
		if (lastStamp == null)
		{
			var currentDay = DateOnly.FromDateTime(now.AddHours(-AttendResetHour).DateTime);
			return new AtreianPassportCheckOnlineDatePlan(
				ShouldReward: true,
				LastAttendDay: DateOnly.MinValue,
				CurrentAttendDay: currentDay,
				"AtreianPassportService.checkOnlineDate -> lastStamp == null -> return true (first stamp)"
			);
		}

		var lastAttendDay = DateOnly.FromDateTime(lastStamp.Value.AddHours(-AttendResetHour).DateTime);
		var currentAttendDay = DateOnly.FromDateTime(now.AddHours(-AttendResetHour).DateTime);
		var isDifferentDay = lastAttendDay != currentAttendDay;

		return new AtreianPassportCheckOnlineDatePlan(
			ShouldReward: isDifferentDay,
			lastAttendDay,
			currentAttendDay,
			isDifferentDay
				? $"AtreianPassportService.checkOnlineDate -> lastDay={lastAttendDay} != currentDay={currentAttendDay} -> true (reward)"
				: $"AtreianPassportService.checkOnlineDate -> lastDay={lastAttendDay} == currentDay={currentAttendDay} -> false (already stamped)"
		);
	}

	public static AccountAgeInMonthsPlan CreateAccountAgeInMonthsPlan(DateOnly creationDate, DateOnly nowDate)
	{
		// Java parity: AtreianPassportService.getAccountAgeInMonths.
		var months = (nowDate.Year - creationDate.Year) * 12 + (nowDate.Month - creationDate.Month);
		if (nowDate.Day < creationDate.Day)
			months--;
		var result = Math.Max(0, months);

		return new AccountAgeInMonthsPlan(
			result, creationDate, nowDate,
			$"AtreianPassportService.getAccountAgeInMonths -> creation={creationDate} now={nowDate} -> months={result}"
		);
	}
}
