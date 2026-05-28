namespace Aion.GameServer.Services;

public static class WorldMapRegionMaterialZoneWeatherTransitionPlanService
{
	public static WorldMapRegionMaterialZoneWeatherTransitionPlan CreatePlan(
		WorldMapRegionMaterialZoneWeatherTransitionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: WeatherService.nextWeather first asks
		// WeatherTable.getWeatherAfter(oldEntry); if there is no chained before/after
		// transition, WeatherService.getRandomWeather selects candidates by weighted rank,
		// filters snow outside winter months, prefers "before" entries, and may clear
		// weather to WeatherEntry.NONE based on Rnd.chance().
		var chained = GetWeatherAfter(context.OldEntry, context.ZoneData);
		if (chained is not null)
		{
			return new WorldMapRegionMaterialZoneWeatherTransitionPlan(
				WorldMapRegionMaterialZoneWeatherTransitionStatus.ChainedWeather,
				chained,
				InitialRank: null,
				CanSnow: CanSnow(context.GameMonth),
				DayTimeCorrection: 1,
				JavaSource: "WeatherTable.getWeatherAfter chained transition");
		}

		var initialRank = SelectInitialRank(context.RankChance);
		var canSnow = CanSnow(context.GameMonth);
		var possibleWeathers = new List<WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot>();
		for (var rank = initialRank; rank >= 0; rank--)
		{
			foreach (var entry in context.ZoneData.Where(entry => entry.ZoneId == context.ZoneId))
			{
				if (entry.Rank == -1)
				{
					return new WorldMapRegionMaterialZoneWeatherTransitionPlan(
						WorldMapRegionMaterialZoneWeatherTransitionStatus.ConstantWeather,
						entry,
						initialRank,
						canSnow,
						DayTimeCorrection: 1,
						JavaSource: "WeatherService.getRandomWeather constant rank -1 weather");
				}

				if (entry.Rank == rank && CheckSnowCondition(canSnow, entry))
					possibleWeathers.Add(entry);
			}

			if (possibleWeathers.Count > 0)
				break;
		}

		if (possibleWeathers.Count == 0)
		{
			return new WorldMapRegionMaterialZoneWeatherTransitionPlan(
				WorldMapRegionMaterialZoneWeatherTransitionStatus.NoneWeather,
				SelectedEntry: null,
				initialRank,
				canSnow,
				DayTimeCorrection: 1,
				JavaSource: "WeatherService.getRandomWeather no possible weather");
		}

		var selected = possibleWeathers[Math.Clamp(context.SelectedPossibleWeatherIndex, 0, possibleWeathers.Count - 1)];
		if (!selected.IsBefore)
		{
			selected = context.ZoneData.FirstOrDefault(entry =>
				entry.ZoneId == context.ZoneId
				&& string.Equals(entry.WeatherName, selected.WeatherName, StringComparison.Ordinal)
				&& entry.IsBefore) ?? selected;
		}

		var dayTimeCorrection = context.DayTime == WorldMapRegionMaterialZoneDayTime.Afternoon && !canSnow
			? 2
			: 1;
		var threshold = selected.Rank switch
		{
			0 => 33 / dayTimeCorrection,
			1 => 50 / dayTimeCorrection,
			2 => 66 / dayTimeCorrection,
			_ => int.MaxValue,
		};

		if (context.Chance >= threshold)
		{
			return new WorldMapRegionMaterialZoneWeatherTransitionPlan(
				WorldMapRegionMaterialZoneWeatherTransitionStatus.NoneWeather,
				SelectedEntry: null,
				initialRank,
				canSnow,
				dayTimeCorrection,
				JavaSource: "WeatherService.getRandomWeather chance cleared selected weather");
		}

		return new WorldMapRegionMaterialZoneWeatherTransitionPlan(
			WorldMapRegionMaterialZoneWeatherTransitionStatus.RandomWeather,
			selected,
			initialRank,
			canSnow,
			dayTimeCorrection,
			JavaSource: "WeatherService.getRandomWeather deterministic random-input plan");
	}

	private static WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot? GetWeatherAfter(
		WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot? entry,
		IReadOnlyList<WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot> zoneData)
	{
		if (entry is null || entry.WeatherName is null || entry.IsAfter)
			return null;

		foreach (var candidate in zoneData)
		{
			if (candidate.ZoneId != entry.ZoneId)
				continue;
			if (!string.Equals(entry.WeatherName, candidate.WeatherName, StringComparison.Ordinal))
				continue;
			if (entry.IsBefore && !candidate.IsBefore && !candidate.IsAfter)
				return candidate;
			if (!entry.IsBefore && !entry.IsAfter && candidate.IsAfter)
				return candidate;
		}

		return null;
	}

	private static int SelectInitialRank(int rankChance)
	{
		if (rankChance == 0)
			return 0;
		return rankChance <= 2 ? 1 : 2;
	}

	private static bool CanSnow(int gameMonth)
	{
		return gameMonth <= 3 || gameMonth >= 11;
	}

	private static bool CheckSnowCondition(
		bool canSnow,
		WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot entry)
	{
		if (canSnow || entry.WeatherName is null)
			return true;
		return entry.WeatherName is not ("SNOW" or "SNOW_BEACH");
	}
}

public sealed record WorldMapRegionMaterialZoneWeatherTransitionContext(
	int ZoneId,
	int GameMonth,
	WorldMapRegionMaterialZoneDayTime DayTime,
	int RankChance,
	int SelectedPossibleWeatherIndex,
	float Chance,
	WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot? OldEntry,
	IReadOnlyList<WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot> ZoneData);

public sealed record WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot(
	int ZoneId,
	string? WeatherName,
	int Rank,
	bool IsBefore,
	bool IsAfter);

public sealed record WorldMapRegionMaterialZoneWeatherTransitionPlan(
	WorldMapRegionMaterialZoneWeatherTransitionStatus Status,
	WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot? SelectedEntry,
	int? InitialRank,
	bool CanSnow,
	int DayTimeCorrection,
	string JavaSource);

public enum WorldMapRegionMaterialZoneWeatherTransitionStatus
{
	ChainedWeather,
	ConstantWeather,
	RandomWeather,
	NoneWeather,
}
