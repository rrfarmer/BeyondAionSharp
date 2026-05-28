namespace Aion.GameServer.Services;

public static class WorldMapRegionZoneIdentityService
{
	public static WorldMapRegionZoneIdentitySnapshot CreateSnapshot(
		WorldMapRegionZoneIdentityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: ZoneInstance.getTownId delegates to ZoneTemplate.getTownId,
		// isDominionZone compares ZoneTemplate.getZoneType() to DOMINION, addHandler appends
		// handlers, and forEach iterates HashMap-backed creature values through CollectionUtil.
		return new WorldMapRegionZoneIdentitySnapshot(
			context.ZoneId,
			context.TownId,
			IsDominionZone: context.ZoneClassName == WorldMapRegionZoneSortClassName.Dominion,
			context.CreatureObjectIds.ToArray(),
			context.HandlerNames.ToArray(),
			CreatureIterationOrderIsStable: false,
			JavaSource: "ZoneInstance lightweight identity/accessor snapshot; live HashMap and handlers disabled");
	}
}

public sealed record WorldMapRegionZoneIdentityContext(
	string ZoneId,
	WorldMapRegionZoneSortClassName ZoneClassName,
	int TownId,
	IReadOnlyList<int> CreatureObjectIds,
	IReadOnlyList<string> HandlerNames);

public sealed record WorldMapRegionZoneIdentitySnapshot(
	string ZoneId,
	int TownId,
	bool IsDominionZone,
	IReadOnlyList<int> CreatureObjectIds,
	IReadOnlyList<string> HandlerNames,
	bool CreatureIterationOrderIsStable,
	string JavaSource);
