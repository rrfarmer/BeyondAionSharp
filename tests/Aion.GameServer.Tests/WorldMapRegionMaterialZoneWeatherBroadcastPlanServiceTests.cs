using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionMaterialZoneWeatherBroadcastPlanServiceTests
{
	[Fact]
	public void CreateCheckWeathersTimePlan_ClampsJavaRandomDelayAndBroadcastsPerMap()
	{
		var plan = WorldMapRegionMaterialZoneWeatherBroadcastPlanService.CreateCheckWeathersTimePlan(
			new WorldMapRegionMaterialZoneWeatherCheckContext(
				ScheduledDelayMilliseconds: 1,
				WorldMapIds: [210010000, 220010000]));

		Assert.Equal(WorldMapRegionMaterialZoneWeatherBroadcastPlanService.MinimumWeatherChangeDelayMilliseconds, plan.ScheduledDelayMilliseconds);
		Assert.Equal([210010000, 220010000], plan.Broadcasts.Select(entry => entry.MapId));
		Assert.All(plan.Broadcasts, entry =>
		{
			Assert.True(entry.ShouldBroadcast);
			Assert.Equal(WorldMapRegionMaterialZoneWeatherBroadcastPlanService.BroadcastFilter, entry.PlayerFilter);
			Assert.Contains("SM_WEATHER", entry.SideEffect);
		});
		Assert.Contains("checkWeathersTime", plan.JavaSource);
	}

	[Theory]
	[InlineData(true, WorldMapRegionMaterialZoneWeatherLoadStatus.PacketSent, true)]
	[InlineData(false, WorldMapRegionMaterialZoneWeatherLoadStatus.NoWeatherEntries, false)]
	public void CreateLoadWeatherPlan_SendsPacketOnlyWhenWorldHasWeatherEntries(
		bool hasWeatherEntries,
		WorldMapRegionMaterialZoneWeatherLoadStatus expectedStatus,
		bool expectedSend)
	{
		var plan = WorldMapRegionMaterialZoneWeatherBroadcastPlanService.CreateLoadWeatherPlan(
			new WorldMapRegionMaterialZoneWeatherLoadContext(
				PlayerWorldId: 210010000,
				WorldHasWeatherEntries: hasWeatherEntries));

		Assert.Equal(expectedStatus, plan.Status);
		Assert.Equal(expectedSend, plan.ShouldSendPacket);
		Assert.Contains("loadWeather", plan.JavaSource);
	}

	[Fact]
	public void CreateChangeWeatherPlan_ReturnsFalseWhenWorldHasNoWeatherEntries()
	{
		var plan = WorldMapRegionMaterialZoneWeatherBroadcastPlanService.CreateChangeWeatherPlan(
			new WorldMapRegionMaterialZoneWeatherChangeContext(
				WorldHasWeatherEntries: false,
				ZoneCount: 2,
				WeatherCode: 7,
				ZoneData: []));

		Assert.Equal(WorldMapRegionMaterialZoneWeatherChangeStatus.NoWeatherEntries, plan.Status);
		Assert.False(plan.ShouldBroadcast);
		Assert.Empty(plan.Entries);
	}

	[Theory]
	[InlineData(-1, WorldMapRegionMaterialZoneWeatherChangeEntryStatus.NaturalTransitionRequested)]
	[InlineData(0, WorldMapRegionMaterialZoneWeatherChangeEntryStatus.NoneWeather)]
	public void CreateChangeWeatherPlan_ModelsNaturalAndNoneWeatherCodes(
		int weatherCode,
		WorldMapRegionMaterialZoneWeatherChangeEntryStatus expectedEntryStatus)
	{
		var plan = WorldMapRegionMaterialZoneWeatherBroadcastPlanService.CreateChangeWeatherPlan(
			new WorldMapRegionMaterialZoneWeatherChangeContext(
				WorldHasWeatherEntries: true,
				ZoneCount: 2,
				WeatherCode: weatherCode,
				ZoneData: []));

		Assert.Equal(WorldMapRegionMaterialZoneWeatherChangeStatus.WeatherChanged, plan.Status);
		Assert.True(plan.ShouldBroadcast);
		Assert.Equal([expectedEntryStatus, expectedEntryStatus], plan.Entries.Select(entry => entry.Status));
		Assert.All(plan.Entries, entry => Assert.Null(entry.SelectedEntry));
	}

	[Fact]
	public void CreateChangeWeatherPlan_UsesExistingWeatherEntryOrCreatesOverrideEntryByZone()
	{
		var existing = new WorldMapRegionMaterialZoneWeatherOverrideEntrySnapshot(
			ZoneId: 1,
			WeatherCode: 7,
			WeatherName: "RAIN");
		var plan = WorldMapRegionMaterialZoneWeatherBroadcastPlanService.CreateChangeWeatherPlan(
			new WorldMapRegionMaterialZoneWeatherChangeContext(
				WorldHasWeatherEntries: true,
				ZoneCount: 2,
				WeatherCode: 7,
				ZoneData: [existing]));

		Assert.Equal(WorldMapRegionMaterialZoneWeatherChangeEntryStatus.ExistingWeatherEntry, plan.Entries[0].Status);
		Assert.Equal(existing, plan.Entries[0].SelectedEntry);
		Assert.Equal(WorldMapRegionMaterialZoneWeatherChangeEntryStatus.CreatedWeatherEntry, plan.Entries[1].Status);
		Assert.Equal(2, plan.Entries[1].SelectedEntry?.ZoneId);
		Assert.Equal(7, plan.Entries[1].SelectedEntry?.WeatherCode);
		Assert.Null(plan.Entries[1].SelectedEntry?.WeatherName);
	}
}
