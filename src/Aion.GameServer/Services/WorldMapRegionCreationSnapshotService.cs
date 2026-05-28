namespace Aion.GameServer.Services;

public static class WorldMapRegionCreationSnapshotService
{
	public static WorldMapRegionCreationSnapshot CreateSnapshot(
		int mapId,
		WorldMapRegionLayout layout,
		int regionId,
		IEnumerable<WorldMapRegionZoneCandidate> zones)
	{
		// Java parity breadcrumb: WorldMap2DInstance/WorldMap3DInstance.createMapRegion
		// decode region bounds, call WorldMapInstance.filterZones, and construct MapRegion.
		var bounds = WorldMapRegionZoneFilterService.CreateRegionBounds(layout, regionId);
		var zoneFilter = WorldMapRegionZoneFilterService.FilterZones(mapId, regionId, bounds, zones);
		var regionExists = layout.NeighbourRegionIds.TryGetValue(regionId, out var neighbourRegionIds);

		return new WorldMapRegionCreationSnapshot(
			mapId,
			regionId,
			layout.Dimension,
			regionExists,
			bounds,
			neighbourRegionIds ?? Array.Empty<int>(),
			zoneFilter.MatchedZones.Select(zone => zone.ZoneId).ToArray(),
			zoneFilter.DummyZonesMissingWholeMapIntersection.Select(zone => zone.ZoneId).ToArray(),
			regionExists
				? "createMapRegion(regionId) prerequisite snapshot; live MapRegion construction disabled"
				: "regions.get(regionId) would be null because the id was not precreated");
	}
}

public sealed record WorldMapRegionCreationSnapshot(
	int MapId,
	int RegionId,
	WorldMapRegionDimension Dimension,
	bool RegionExists,
	WorldMapRegionBounds Bounds,
	IReadOnlyList<int> NeighbourRegionIds,
	IReadOnlyList<string> ZoneIds,
	IReadOnlyList<string> DummyZoneMissIds,
	string JavaSource);
