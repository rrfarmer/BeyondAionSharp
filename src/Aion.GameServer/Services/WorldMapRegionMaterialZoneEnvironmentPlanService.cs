namespace Aion.GameServer.Services;

public static class WorldMapRegionMaterialZoneEnvironmentPlanService
{
	public static WorldMapRegionMaterialZoneEnvironmentPlan CreatePlan(
		WorldMapRegionMaterialZoneEnvironmentContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: GameTime rejects negative minutes and calculates
		// DayTime from hour; WeatherService.findWeatherEntry returns the first non-null
		// weather entry from WEATHER zones, otherwise WeatherEntry.NONE.
		if (context.GameTimeMinutes < 0)
		{
			return new WorldMapRegionMaterialZoneEnvironmentPlan(
				WorldMapRegionMaterialZoneEnvironmentStatus.BlockedNegativeGameTime,
				DayTime: null,
				WeatherName: null,
				WeatherIsBefore: false,
				SunnyConditionMatches: true,
				JavaSource: "GameTime constructor rejects negative time");
		}

		var weatherEntry = context.WeatherZones
			.Where(zone => zone.IsWeatherZone)
			.Select(zone => zone.Entry)
			.FirstOrDefault(entry => entry is not null);

		var weatherName = weatherEntry?.WeatherName;
		var weatherIsBefore = weatherEntry?.IsBefore ?? false;
		var isRain = weatherName is not null && weatherName.StartsWith("RAIN", StringComparison.Ordinal);
		return new WorldMapRegionMaterialZoneEnvironmentPlan(
			WorldMapRegionMaterialZoneEnvironmentStatus.Ready,
			CalculateDayTime(context.GameTimeMinutes),
			weatherName,
			weatherIsBefore,
			SunnyConditionMatches: !isRain || weatherIsBefore,
			JavaSource: "GameTime.getDayTime and WeatherService.findWeatherEntry non-live boundary plan");
	}

	private static WorldMapRegionMaterialZoneDayTime CalculateDayTime(int gameTimeMinutes)
	{
		var hour = (gameTimeMinutes % 1440) / 60;
		if (hour > 21 || hour < 4)
			return WorldMapRegionMaterialZoneDayTime.Night;
		if (hour > 16)
			return WorldMapRegionMaterialZoneDayTime.Evening;
		if (hour > 8)
			return WorldMapRegionMaterialZoneDayTime.Afternoon;
		return WorldMapRegionMaterialZoneDayTime.Morning;
	}
}

public sealed record WorldMapRegionMaterialZoneEnvironmentContext(
	int GameTimeMinutes,
	IReadOnlyList<WorldMapRegionMaterialZoneWeatherZoneSnapshot> WeatherZones);

public sealed record WorldMapRegionMaterialZoneWeatherZoneSnapshot(
	bool IsWeatherZone,
	int WeatherZoneId,
	WorldMapRegionMaterialZoneWeatherEntrySnapshot? Entry);

public sealed record WorldMapRegionMaterialZoneWeatherEntrySnapshot(
	string? WeatherName,
	bool IsBefore,
	bool IsAfter);

public sealed record WorldMapRegionMaterialZoneEnvironmentPlan(
	WorldMapRegionMaterialZoneEnvironmentStatus Status,
	WorldMapRegionMaterialZoneDayTime? DayTime,
	string? WeatherName,
	bool WeatherIsBefore,
	bool SunnyConditionMatches,
	string JavaSource);

public enum WorldMapRegionMaterialZoneEnvironmentStatus
{
	Ready,
	BlockedNegativeGameTime,
}
