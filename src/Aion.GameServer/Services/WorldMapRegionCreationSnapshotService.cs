namespace Aion.GameServer.Services;

public static class WorldMapRegionCreationSnapshotService
{
	public static WorldMapRegionCreationSnapshot CreateSnapshot(
		int mapId,
		WorldMapRegionLayout layout,
		int regionId,
		IEnumerable<WorldMapRegionZoneCandidate> zones,
		IEnumerable<WorldMapRegionZoneSortCandidate>? zoneSortCandidates = null)
	{
		// Java parity breadcrumb: WorldMap2DInstance/WorldMap3DInstance.createMapRegion
		// decode region bounds, call WorldMapInstance.filterZones, and construct MapRegion.
		var bounds = WorldMapRegionZoneFilterService.CreateRegionBounds(layout, regionId);
		var zoneFilter = WorldMapRegionZoneFilterService.FilterZones(mapId, regionId, bounds, zones);
		var regionExists = layout.NeighbourRegionIds.TryGetValue(regionId, out var neighbourRegionIds);
		var zoneIds = zoneFilter.MatchedZones.Select(zone => zone.ZoneId).ToArray();
		var sortCandidatesById = (zoneSortCandidates ?? Array.Empty<WorldMapRegionZoneSortCandidate>())
			.GroupBy(zone => zone.ZoneId, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
		var sortableZones = new List<WorldMapRegionZoneSortCandidate>();
		var missingZoneSortIds = new List<string>();
		foreach (var zoneId in zoneIds)
		{
			if (sortCandidatesById.TryGetValue(zoneId, out var sortCandidate))
				sortableZones.Add(sortCandidate);
			else
				missingZoneSortIds.Add(zoneId);
		}

		var constructorOrderedZoneIds = missingZoneSortIds.Count == 0
			? WorldMapRegionZoneSortService.SortByJavaMapRegionOrder(sortableZones)
				.Select(zone => zone.ZoneId)
				.ToArray()
			: Array.Empty<string>();

		return new WorldMapRegionCreationSnapshot(
			mapId,
			regionId,
			layout.Dimension,
			regionExists,
			bounds,
			neighbourRegionIds ?? Array.Empty<int>(),
			zoneIds,
			constructorOrderedZoneIds,
			missingZoneSortIds.ToArray(),
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
	IReadOnlyList<string> ConstructorOrderedZoneIds,
	IReadOnlyList<string> MissingZoneSortIds,
	IReadOnlyList<string> DummyZoneMissIds,
	string JavaSource);
