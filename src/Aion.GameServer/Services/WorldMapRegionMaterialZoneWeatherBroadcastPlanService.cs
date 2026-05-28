using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class WorldMapRegionMaterialZoneWeatherBroadcastPlanService
{
	public const int MinimumWeatherChangeDelayMilliseconds = 20000;
	public const int MaximumWeatherChangeDelayMilliseconds = 240000;
	public const string BroadcastFilter = "player.isSpawned && player.worldId == mapId";

	public static WorldMapRegionMaterialZoneWeatherCheckPlan CreateCheckWeathersTimePlan(
		WorldMapRegionMaterialZoneWeatherCheckContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: WeatherService.checkWeathersTime schedules a delayed
		// task using Rnd.get(20000, 240000), recalculates weather entries per map, and
		// broadcasts SM_WEATHER only to spawned players in the same world id.
		return new WorldMapRegionMaterialZoneWeatherCheckPlan(
			Math.Clamp(context.ScheduledDelayMilliseconds, MinimumWeatherChangeDelayMilliseconds, MaximumWeatherChangeDelayMilliseconds),
			context.WorldMapIds.Select(mapId => new WorldMapRegionMaterialZoneWeatherBroadcastEntry(
				mapId,
				ShouldBroadcast: true,
				BroadcastFilter,
				"setNextWeather then SM_WEATHER broadcast")).ToArray(),
			"WeatherService.checkWeathersTime non-live broadcast plan");
	}

	public static WorldMapRegionMaterialZoneWeatherLoadPlan CreateLoadWeatherPlan(
		WorldMapRegionMaterialZoneWeatherLoadContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: WeatherService.loadWeather sends SM_WEATHER only when
		// worldZoneWeathers contains entries for the player's world id.
		var shouldSend = context.WorldHasWeatherEntries;
		return new WorldMapRegionMaterialZoneWeatherLoadPlan(
			shouldSend
				? WorldMapRegionMaterialZoneWeatherLoadStatus.PacketSent
				: WorldMapRegionMaterialZoneWeatherLoadStatus.NoWeatherEntries,
			context.PlayerWorldId,
			shouldSend,
			"WeatherService.loadWeather non-live packet plan");
	}

	public static WorldMapRegionMaterialZoneWeatherChangePlan CreateChangeWeatherPlan(
		WorldMapRegionMaterialZoneWeatherChangeContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: WeatherService.changeWeather returns false when the map
		// has no weather entries; otherwise it updates every zone with natural transition
		// for -1, WeatherEntry.NONE for 0, an existing table entry for matching code, or a
		// newly-created WeatherEntry(zoneId, weatherCode), then broadcasts SM_WEATHER.
		if (!context.WorldHasWeatherEntries)
		{
			return new WorldMapRegionMaterialZoneWeatherChangePlan(
				WorldMapRegionMaterialZoneWeatherChangeStatus.NoWeatherEntries,
				Array.Empty<WorldMapRegionMaterialZoneWeatherChangeEntry>(),
				ShouldBroadcast: false,
				JavaSource: "WeatherService.changeWeather missing world weather entries");
		}

		var entries = new List<WorldMapRegionMaterialZoneWeatherChangeEntry>();
		for (var zoneId = 1; zoneId <= context.ZoneCount; zoneId++)
		{
			entries.Add(CreateChangeEntry(zoneId, context));
		}

		return new WorldMapRegionMaterialZoneWeatherChangePlan(
			WorldMapRegionMaterialZoneWeatherChangeStatus.WeatherChanged,
			entries,
			ShouldBroadcast: true,
			JavaSource: "WeatherService.changeWeather non-live weather-code override plan");
	}

	private static WorldMapRegionMaterialZoneWeatherChangeEntry CreateChangeEntry(
		int zoneId,
		WorldMapRegionMaterialZoneWeatherChangeContext context)
	{
		if (context.WeatherCode == -1)
		{
			return new WorldMapRegionMaterialZoneWeatherChangeEntry(
				zoneId,
				WorldMapRegionMaterialZoneWeatherChangeEntryStatus.NaturalTransitionRequested,
				SelectedEntry: null);
		}

		if (context.WeatherCode == 0)
		{
			return new WorldMapRegionMaterialZoneWeatherChangeEntry(
				zoneId,
				WorldMapRegionMaterialZoneWeatherChangeEntryStatus.NoneWeather,
				SelectedEntry: null);
		}

		var existing = context.ZoneData.FirstOrDefault(entry =>
			entry.ZoneId == zoneId
			&& entry.WeatherCode == context.WeatherCode);
		return existing is null
			? new WorldMapRegionMaterialZoneWeatherChangeEntry(
				zoneId,
				WorldMapRegionMaterialZoneWeatherChangeEntryStatus.CreatedWeatherEntry,
				new WorldMapRegionMaterialZoneWeatherOverrideEntrySnapshot(zoneId, context.WeatherCode, WeatherName: null))
			: new WorldMapRegionMaterialZoneWeatherChangeEntry(
				zoneId,
				WorldMapRegionMaterialZoneWeatherChangeEntryStatus.ExistingWeatherEntry,
				existing);
	}

	public static WorldMapRegionMaterialZoneWeatherPacketFactoryPlan CreateWeatherPacketFactoryPlan(
		IReadOnlyList<int> weatherCodes)
	{
		ArgumentNullException.ThrowIfNull(weatherCodes);

		// Java parity breadcrumb: WeatherService.loadWeather/checkWeathersTime/changeWeather
		// instantiate new SM_WEATHER(worldZoneWeathers.get(worldId)) after selecting the
		// ordered WeatherEntry array for a world map.
		return new WorldMapRegionMaterialZoneWeatherPacketFactoryPlan(
			weatherCodes,
			new SmWeather(weatherCodes),
			"new SM_WEATHER(weatherEntries) non-live packet factory boundary");
	}
}

public sealed record WorldMapRegionMaterialZoneWeatherCheckContext(
	int ScheduledDelayMilliseconds,
	IReadOnlyList<int> WorldMapIds);

public sealed record WorldMapRegionMaterialZoneWeatherCheckPlan(
	int ScheduledDelayMilliseconds,
	IReadOnlyList<WorldMapRegionMaterialZoneWeatherBroadcastEntry> Broadcasts,
	string JavaSource);

public sealed record WorldMapRegionMaterialZoneWeatherBroadcastEntry(
	int MapId,
	bool ShouldBroadcast,
	string PlayerFilter,
	string SideEffect);

public sealed record WorldMapRegionMaterialZoneWeatherLoadContext(
	int PlayerWorldId,
	bool WorldHasWeatherEntries);

public sealed record WorldMapRegionMaterialZoneWeatherLoadPlan(
	WorldMapRegionMaterialZoneWeatherLoadStatus Status,
	int PlayerWorldId,
	bool ShouldSendPacket,
	string JavaSource);

public sealed record WorldMapRegionMaterialZoneWeatherChangeContext(
	bool WorldHasWeatherEntries,
	int ZoneCount,
	int WeatherCode,
	IReadOnlyList<WorldMapRegionMaterialZoneWeatherOverrideEntrySnapshot> ZoneData);

public sealed record WorldMapRegionMaterialZoneWeatherChangePlan(
	WorldMapRegionMaterialZoneWeatherChangeStatus Status,
	IReadOnlyList<WorldMapRegionMaterialZoneWeatherChangeEntry> Entries,
	bool ShouldBroadcast,
	string JavaSource);

public sealed record WorldMapRegionMaterialZoneWeatherChangeEntry(
	int ZoneId,
	WorldMapRegionMaterialZoneWeatherChangeEntryStatus Status,
	WorldMapRegionMaterialZoneWeatherOverrideEntrySnapshot? SelectedEntry);

public sealed record WorldMapRegionMaterialZoneWeatherOverrideEntrySnapshot(
	int ZoneId,
	int WeatherCode,
	string? WeatherName);

public sealed record WorldMapRegionMaterialZoneWeatherPacketFactoryPlan(
	IReadOnlyList<int> WeatherCodes,
	SmWeather Packet,
	string JavaSource);

public enum WorldMapRegionMaterialZoneWeatherLoadStatus
{
	PacketSent,
	NoWeatherEntries,
}

public enum WorldMapRegionMaterialZoneWeatherChangeStatus
{
	WeatherChanged,
	NoWeatherEntries,
}

public enum WorldMapRegionMaterialZoneWeatherChangeEntryStatus
{
	NaturalTransitionRequested,
	NoneWeather,
	ExistingWeatherEntry,
	CreatedWeatherEntry,
}
