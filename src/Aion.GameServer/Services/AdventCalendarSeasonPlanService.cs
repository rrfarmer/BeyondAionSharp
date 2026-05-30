namespace Aion.GameServer.Services;

public sealed record AdventCalendarSeasonPlan(
	DateOnly Date,
	bool IsAdventSeason,
	int DayOfMonth,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class AdventCalendarSeasonPlanService
{
	// Java parity: services/reward/AdventService.isAdventSeason.
	// December 1-24 (day <= 24) is advent season.
	public const int AdventEndDay = 24;

	public static AdventCalendarSeasonPlan CreatePlan(DateOnly date)
	{
		// Java parity: services/reward/AdventService.isAdventSeason(LocalDate).
		// return date.getMonth() == Month.DECEMBER && date.getDayOfMonth() <= AdventEndDay;
		var isAdventSeason = date.Month == 12 && date.Day <= AdventEndDay;

		return new AdventCalendarSeasonPlan(
			date, isAdventSeason, date.Day,
			isAdventSeason
				? $"AdventService.isAdventSeason -> {date} is December && day {date.Day} <= 24 -> true"
				: $"AdventService.isAdventSeason -> {date} not in advent season -> false"
		);
	}

	public static bool IsAdventSeason(DateOnly date)
	{
		return date.Month == 12 && date.Day <= AdventEndDay;
	}
}
