using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class WorldMapRegionZoneCapabilityService
{
	public static WorldMapRegionZoneCapabilityPlan CreatePlan(
		WorldMapRegionZoneCapabilityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: ZoneInstance.canFly/canGlide/canPutKisk/canRecall/canRide/
		// canFlyRide use zone flags unless flags are -1/0 or the WorldMap has overridden
		// that option; PvP and duel checks have their own ZoneClassName branches.
		var worldMap = context.WorldMap;
		var currentWorldFlags = context.CurrentWorldFlags;
		var zoneFlags = context.ZoneFlags;
		var zoneType = context.ZoneClassName;

		return new WorldMapRegionZoneCapabilityPlan(
			CanFly: ResolveOption(zoneFlags, worldMap, currentWorldFlags, WorldZoneAttributes.Fly, worldMap.IsFlightAllowed),
			CanGlide: ResolveOption(zoneFlags, worldMap, currentWorldFlags, WorldZoneAttributes.Glide, worldMap.CanGlide),
			CanPutKisk: ResolveOption(zoneFlags, worldMap, currentWorldFlags, WorldZoneAttributes.Bind, worldMap.CanPutKisk),
			CanRecall: ResolveOption(zoneFlags, worldMap, currentWorldFlags, WorldZoneAttributes.Recall, worldMap.CanRecall),
			CanReturnToBattle: worldMap.CanReturnToBattle(currentWorldFlags),
			CanRide: ResolveOption(zoneFlags, worldMap, currentWorldFlags, WorldZoneAttributes.Ride, worldMap.CanRide),
			CanFlyRide: ResolveOption(zoneFlags, worldMap, currentWorldFlags, WorldZoneAttributes.FlyRide, worldMap.CanFlyRide),
			IsPvpAllowed: zoneType == WorldMapRegionZoneSortClassName.Pvp
				? HasFlag(zoneFlags, WorldZoneAttributes.PvpEnabled)
				: worldMap.IsPvpAllowed(currentWorldFlags),
			IsSameRaceDuelAllowed: zoneType != WorldMapRegionZoneSortClassName.Duel
				|| zoneFlags == 0
				|| worldMap.HasOverriddenOption(WorldZoneAttributes.DuelSameRaceEnabled, currentWorldFlags)
					? worldMap.IsSameRaceDuelsAllowed(currentWorldFlags)
					: HasFlag(zoneFlags, WorldZoneAttributes.DuelSameRaceEnabled),
			IsOtherRaceDuelAllowed: zoneType != WorldMapRegionZoneSortClassName.Duel
				|| zoneFlags == 0
				|| worldMap.HasOverriddenOption(WorldZoneAttributes.DuelOtherRaceEnabled, currentWorldFlags)
					? worldMap.IsOtherRaceDuelsAllowed(currentWorldFlags)
					: HasFlag(zoneFlags, WorldZoneAttributes.DuelOtherRaceEnabled),
			JavaSource: "ZoneInstance capability/options snapshot; live World singleton and ZoneTemplate storage disabled");
	}

	private static bool ResolveOption(
		int zoneFlags,
		WorldMapSummary worldMap,
		WorldZoneAttributes currentWorldFlags,
		WorldZoneAttributes option,
		Func<WorldZoneAttributes, bool> worldFallback)
	{
		if (zoneFlags is -1 or 0 || worldMap.HasOverriddenOption(option, currentWorldFlags))
			return worldFallback(currentWorldFlags);

		return HasFlag(zoneFlags, option);
	}

	private static bool HasFlag(int zoneFlags, WorldZoneAttributes attribute)
	{
		return (zoneFlags & (int)attribute) != 0;
	}
}

public sealed record WorldMapRegionZoneCapabilityContext(
	WorldMapSummary WorldMap,
	WorldZoneAttributes CurrentWorldFlags,
	WorldMapRegionZoneSortClassName ZoneClassName,
	int ZoneFlags);

public sealed record WorldMapRegionZoneCapabilityPlan(
	bool CanFly,
	bool CanGlide,
	bool CanPutKisk,
	bool CanRecall,
	bool CanReturnToBattle,
	bool CanRide,
	bool CanFlyRide,
	bool IsPvpAllowed,
	bool IsSameRaceDuelAllowed,
	bool IsOtherRaceDuelAllowed,
	string JavaSource);
