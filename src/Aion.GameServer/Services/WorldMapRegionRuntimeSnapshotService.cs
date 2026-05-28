namespace Aion.GameServer.Services;

public static class WorldMapRegionRuntimeSnapshotService
{
	public static WorldMapRegionRuntimeSnapshot CreateSnapshot(
		WorldMapRegionCreationSnapshot creation,
		WorldMapRegionLifecycleRegionState lifecycle)
	{
		// Java parity breadcrumb: MapRegion constructor stores id, parent, sorted zones,
		// neighboursIncludingSelf, object map, player count, active state, and deactivation flag.
		return new WorldMapRegionRuntimeSnapshot(
			creation.MapId,
			creation.RegionId,
			creation.Dimension,
			creation.RegionExists,
			creation.Bounds,
			creation.NeighbourRegionIds,
			creation.ZoneIds,
			creation.ConstructorOrderedZoneIds,
			creation.MissingZoneSortIds,
			lifecycle.IsActive,
			lifecycle.PlayerCount,
			lifecycle.DeactivationPending,
			CanCreateLiveRegion: creation.RegionExists,
			MissingLivePieces:
			[
				"WorldMapInstance parent reference",
				"ConcurrentHashMap<Integer, VisibleObject> objects",
				"Live ZoneInstance[] storage and zone handlers",
				"MapRegion[] neighboursIncludingSelf object references",
				"ThreadPoolManager activation/deactivation notifications",
			],
			JavaSource: creation.RegionExists
				? "MapRegion constructor/runtime field readiness snapshot; live construction disabled"
				: "MapRegion runtime snapshot blocked because region id was not precreated");
	}
}

public sealed record WorldMapRegionRuntimeSnapshot(
	int MapId,
	int RegionId,
	WorldMapRegionDimension Dimension,
	bool RegionExists,
	WorldMapRegionBounds Bounds,
	IReadOnlyList<int> NeighbourRegionIds,
	IReadOnlyList<string> ZoneIds,
	IReadOnlyList<string> ConstructorOrderedZoneIds,
	IReadOnlyList<string> MissingZoneSortIds,
	bool IsActive,
	int PlayerCount,
	bool DeactivationPending,
	bool CanCreateLiveRegion,
	IReadOnlyList<string> MissingLivePieces,
	string JavaSource);
