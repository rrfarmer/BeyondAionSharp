namespace Aion.GameServer.Services;

public sealed record WeatherRankSelectionPlan(
	int RankChance,
	int SelectedRank,
	bool CanSnow,
	int Month,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class WeatherRankSelectionPlanService
{
	// Java parity: services/WeatherService.getRandomWeather rank selection formula.
	// rank 2 occurs 4/7 times (rankChance 3-6), rank 1 occurs 2/7 (rankChance 1-2), rank 0 occurs 1/7 (rankChance 0).
	public const int MaxRankChance = 6;

	// Java parity: canSnow = time.getMonth() <= 3 || time.getMonth() >= 11
	// Months 1-3 (Jan-Mar) and 11-12 (Nov-Dec) = snow season.
	public static bool CanSnow(int month)
	{
		return month <= 3 || month >= 11;
	}

	public static int SelectRank(int rankChance)
	{
		// Java parity: WeatherService.getRandomWeather rank selection.
		if (rankChance == 0)
			return 0;
		if (rankChance <= 2)
			return 1;
		return 2;
	}

	public static WeatherRankSelectionPlan CreatePlan(int rankChance, int month)
	{
		var rank = SelectRank(rankChance);
		var canSnow = CanSnow(month);

		return new WeatherRankSelectionPlan(
			rankChance, rank, canSnow, month,
			$"WeatherService.getRandomWeather -> rankChance={rankChance} -> rank={rank}; month={month} canSnow={canSnow}"
		);
	}
}
