using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionMaterialZoneEnvironmentPlanServiceTests
{
	[Theory]
	[InlineData(0, WorldMapRegionMaterialZoneDayTime.Night)]
	[InlineData(240, WorldMapRegionMaterialZoneDayTime.Morning)]
	[InlineData(540, WorldMapRegionMaterialZoneDayTime.Afternoon)]
	[InlineData(1020, WorldMapRegionMaterialZoneDayTime.Evening)]
	[InlineData(1320, WorldMapRegionMaterialZoneDayTime.Night)]
	public void CreatePlan_MapsGameMinutesToJavaDayTimeThresholds(
		int gameTimeMinutes,
		WorldMapRegionMaterialZoneDayTime expectedDayTime)
	{
		var plan = WorldMapRegionMaterialZoneEnvironmentPlanService.CreatePlan(new WorldMapRegionMaterialZoneEnvironmentContext(
			gameTimeMinutes,
			WeatherZones: []));

		Assert.Equal(WorldMapRegionMaterialZoneEnvironmentStatus.Ready, plan.Status);
		Assert.Equal(expectedDayTime, plan.DayTime);
		Assert.Contains("GameTime.getDayTime", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_BlocksNegativeGameTimeLikeJavaConstructor()
	{
		var plan = WorldMapRegionMaterialZoneEnvironmentPlanService.CreatePlan(new WorldMapRegionMaterialZoneEnvironmentContext(
			GameTimeMinutes: -1,
			WeatherZones: []));

		Assert.Equal(WorldMapRegionMaterialZoneEnvironmentStatus.BlockedNegativeGameTime, plan.Status);
		Assert.Null(plan.DayTime);
		Assert.Contains("rejects negative", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_UsesFirstNonNullWeatherEntryFromWeatherZones()
	{
		var plan = WorldMapRegionMaterialZoneEnvironmentPlanService.CreatePlan(new WorldMapRegionMaterialZoneEnvironmentContext(
			GameTimeMinutes: 600,
			WeatherZones:
			[
				new(IsWeatherZone: false, WeatherZoneId: 1, Entry: Weather("RAIN_IGNORED", isBefore: false)),
				new(IsWeatherZone: true, WeatherZoneId: 2, Entry: null),
				new(IsWeatherZone: true, WeatherZoneId: 3, Entry: Weather("RAIN_HEAVY", isBefore: true)),
				new(IsWeatherZone: true, WeatherZoneId: 4, Entry: Weather("SNOW", isBefore: false)),
			]));

		Assert.Equal("RAIN_HEAVY", plan.WeatherName);
		Assert.True(plan.WeatherIsBefore);
		Assert.True(plan.SunnyConditionMatches);
	}

	[Theory]
	[InlineData(null, false, true)]
	[InlineData("SUNNY", false, true)]
	[InlineData("RAIN_HEAVY", true, true)]
	[InlineData("RAIN_HEAVY", false, false)]
	[InlineData("rain_heavy", false, true)]
	public void CreatePlan_ModelsJavaSunnyConditionRainPrefixCaseSensitivity(
		string? weatherName,
		bool isBefore,
		bool expectedSunnyCondition)
	{
		var plan = WorldMapRegionMaterialZoneEnvironmentPlanService.CreatePlan(new WorldMapRegionMaterialZoneEnvironmentContext(
			GameTimeMinutes: 600,
			WeatherZones: [new(IsWeatherZone: true, WeatherZoneId: 1, Entry: Weather(weatherName, isBefore))]));

		Assert.Equal(expectedSunnyCondition, plan.SunnyConditionMatches);
	}

	private static WorldMapRegionMaterialZoneWeatherEntrySnapshot Weather(
		string? weatherName,
		bool isBefore)
	{
		return new WorldMapRegionMaterialZoneWeatherEntrySnapshot(
			weatherName,
			isBefore,
			IsAfter: false);
	}
}
