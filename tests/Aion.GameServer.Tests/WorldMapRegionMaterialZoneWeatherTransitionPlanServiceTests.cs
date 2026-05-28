using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionMaterialZoneWeatherTransitionPlanServiceTests
{
	[Fact]
	public void CreatePlan_UsesJavaBeforeAndAfterWeatherChainBeforeRandomSelection()
	{
		var before = Weather(zoneId: 1, "RAIN", rank: 1, isBefore: true, isAfter: false);
		var active = Weather(zoneId: 1, "RAIN", rank: 1, isBefore: false, isAfter: false);
		var after = Weather(zoneId: 1, "RAIN", rank: 1, isBefore: false, isAfter: true);

		var fromBefore = WorldMapRegionMaterialZoneWeatherTransitionPlanService.CreatePlan(CreateContext(
			oldEntry: before,
			zoneData: [before, active, after]));
		var fromActive = WorldMapRegionMaterialZoneWeatherTransitionPlanService.CreatePlan(CreateContext(
			oldEntry: active,
			zoneData: [before, active, after]));

		Assert.Equal(WorldMapRegionMaterialZoneWeatherTransitionStatus.ChainedWeather, fromBefore.Status);
		Assert.Equal(active, fromBefore.SelectedEntry);
		Assert.Equal(after, fromActive.SelectedEntry);
		Assert.Contains("getWeatherAfter", fromBefore.JavaSource);
	}

	[Fact]
	public void CreatePlan_ReturnsConstantWeatherForRankMinusOne()
	{
		var constant = Weather(zoneId: 1, "CONSTANT_FOG", rank: -1, isBefore: false, isAfter: false);

		var plan = WorldMapRegionMaterialZoneWeatherTransitionPlanService.CreatePlan(CreateContext(
			oldEntry: null,
			zoneData: [constant]));

		Assert.Equal(WorldMapRegionMaterialZoneWeatherTransitionStatus.ConstantWeather, plan.Status);
		Assert.Equal(constant, plan.SelectedEntry);
	}

	[Fact]
	public void CreatePlan_FiltersSnowOutsideWinterAndFallsBackToLowerRank()
	{
		var summerSnow = Weather(zoneId: 1, "SNOW", rank: 2, isBefore: false, isAfter: false);
		var lowerRankRain = Weather(zoneId: 1, "RAIN", rank: 1, isBefore: true, isAfter: false);

		var plan = WorldMapRegionMaterialZoneWeatherTransitionPlanService.CreatePlan(CreateContext(
			gameMonth: 6,
			rankChance: 6,
			chance: 0,
			zoneData: [summerSnow, lowerRankRain]));

		Assert.Equal(WorldMapRegionMaterialZoneWeatherTransitionStatus.RandomWeather, plan.Status);
		Assert.False(plan.CanSnow);
		Assert.Equal(lowerRankRain, plan.SelectedEntry);
	}

	[Fact]
	public void CreatePlan_PrefersBeforeWeatherForSelectedActiveWeather()
	{
		var before = Weather(zoneId: 1, "RAIN", rank: 1, isBefore: true, isAfter: false);
		var active = Weather(zoneId: 1, "RAIN", rank: 1, isBefore: false, isAfter: false);

		var plan = WorldMapRegionMaterialZoneWeatherTransitionPlanService.CreatePlan(CreateContext(
			rankChance: 1,
			selectedPossibleWeatherIndex: 1,
			chance: 0,
			zoneData: [before, active]));

		Assert.Equal(before, plan.SelectedEntry);
	}

	[Fact]
	public void CreatePlan_UsesJavaIntegerAfternoonCorrectionWhenChanceClearsWeather()
	{
		var rankZeroWeather = Weather(zoneId: 1, "RAIN", rank: 0, isBefore: false, isAfter: false);

		var plan = WorldMapRegionMaterialZoneWeatherTransitionPlanService.CreatePlan(CreateContext(
			gameMonth: 6,
			dayTime: WorldMapRegionMaterialZoneDayTime.Afternoon,
			rankChance: 0,
			chance: 16,
			zoneData: [rankZeroWeather]));

		Assert.Equal(WorldMapRegionMaterialZoneWeatherTransitionStatus.NoneWeather, plan.Status);
		Assert.Equal(2, plan.DayTimeCorrection);
		Assert.Null(plan.SelectedEntry);
	}

	private static WorldMapRegionMaterialZoneWeatherTransitionContext CreateContext(
		int zoneId = 1,
		int gameMonth = 6,
		WorldMapRegionMaterialZoneDayTime dayTime = WorldMapRegionMaterialZoneDayTime.Morning,
		int rankChance = 0,
		int selectedPossibleWeatherIndex = 0,
		float chance = 0,
		WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot? oldEntry = null,
		IReadOnlyList<WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot>? zoneData = null)
	{
		return new WorldMapRegionMaterialZoneWeatherTransitionContext(
			zoneId,
			gameMonth,
			dayTime,
			rankChance,
			selectedPossibleWeatherIndex,
			chance,
			oldEntry,
			zoneData ?? []);
	}

	private static WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot Weather(
		int zoneId,
		string? weatherName,
		int rank,
		bool isBefore,
		bool isAfter)
	{
		return new WorldMapRegionMaterialZoneWeatherTransitionEntrySnapshot(
			zoneId,
			weatherName,
			rank,
			isBefore,
			isAfter);
	}
}
