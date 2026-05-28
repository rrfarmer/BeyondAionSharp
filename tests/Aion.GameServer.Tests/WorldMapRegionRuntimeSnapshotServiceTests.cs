using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionRuntimeSnapshotServiceTests
{
	[Fact]
	public void CreateSnapshot_ComposesCreationPrerequisitesWithLifecycleState()
	{
		var creation = CreateCreationSnapshot();
		var lifecycle = new WorldMapRegionLifecycleRegionState(
			RegionId: creation.RegionId,
			IsActive: true,
			PlayerCount: 2,
			DeactivationPending: false);

		var snapshot = WorldMapRegionRuntimeSnapshotService.CreateSnapshot(creation, lifecycle);

		Assert.True(snapshot.CanCreateLiveRegion);
		Assert.True(snapshot.RegionExists);
		Assert.True(snapshot.IsActive);
		Assert.Equal(2, snapshot.PlayerCount);
		Assert.False(snapshot.DeactivationPending);
		Assert.Equal([0, 1, 2, 1000, 1002, 2000, 2001, 2002], snapshot.NeighbourRegionIds);
		Assert.Equal(["zone-fort", "zone-sub"], snapshot.ZoneIds);
		Assert.Equal(["zone-sub", "zone-fort"], snapshot.ConstructorOrderedZoneIds);
		Assert.Empty(snapshot.MissingZoneSortIds);
		Assert.Contains(snapshot.MissingLivePieces, piece => piece.Contains("ConcurrentHashMap", StringComparison.Ordinal));
		Assert.Contains("MapRegion constructor", snapshot.JavaSource);
	}

	[Fact]
	public void CreateSnapshot_BlocksLiveReadinessWhenRegionWasNotPrecreated()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 128,
			WorldMapRegionDimension.ThreeDimensional);
		var creation = WorldMapRegionCreationSnapshotService.CreateSnapshot(
			400010000,
			layout,
			regionId: 1,
			Array.Empty<WorldMapRegionZoneCandidate>());
		var lifecycle = new WorldMapRegionLifecycleRegionState(1, IsActive: false, PlayerCount: 0);

		var snapshot = WorldMapRegionRuntimeSnapshotService.CreateSnapshot(creation, lifecycle);

		Assert.False(snapshot.RegionExists);
		Assert.False(snapshot.CanCreateLiveRegion);
		Assert.Empty(snapshot.NeighbourRegionIds);
		Assert.Empty(snapshot.ZoneIds);
		Assert.Empty(snapshot.ConstructorOrderedZoneIds);
		Assert.Empty(snapshot.MissingZoneSortIds);
		Assert.Contains("blocked", snapshot.JavaSource);
	}

	[Fact]
	public void CreateSnapshot_CarriesPendingDeactivationMetadataForFutureSchedulerBoundary()
	{
		var creation = CreateCreationSnapshot();
		var lifecycle = new WorldMapRegionLifecycleRegionState(
			RegionId: creation.RegionId,
			IsActive: true,
			PlayerCount: 0,
			DeactivationPending: true);

		var snapshot = WorldMapRegionRuntimeSnapshotService.CreateSnapshot(creation, lifecycle);

		Assert.True(snapshot.DeactivationPending);
		Assert.Equal(0, snapshot.PlayerCount);
		Assert.True(snapshot.CanCreateLiveRegion);
	}

	private static WorldMapRegionCreationSnapshot CreateCreationSnapshot()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 256,
			WorldMapRegionDimension.TwoDimensional);
		var zones = new[]
		{
			new WorldMapRegionZoneCandidate(
				"zone-fort",
				210010000,
				WorldMapRegionZoneClassName.Other,
				new WorldMapPolygonZoneArea(
				[
					new ZonePoint2D(130, 130),
					new ZonePoint2D(180, 130),
					new ZonePoint2D(180, 180),
					new ZonePoint2D(130, 180),
				],
				Bottom: 0,
				Top: 256)),
			new WorldMapRegionZoneCandidate(
				"zone-sub",
				210010000,
				WorldMapRegionZoneClassName.Other,
				new WorldMapPolygonZoneArea(
				[
					new ZonePoint2D(132, 132),
					new ZonePoint2D(182, 132),
					new ZonePoint2D(182, 182),
					new ZonePoint2D(132, 182),
				],
				Bottom: 0,
				Top: 256)),
		};
		var sortCandidates = new[]
		{
			new WorldMapRegionZoneSortCandidate(
				"zone-fort",
				WorldMapRegionZoneSortClassName.Fort,
				Priority: 0,
				WorldMapRegionZoneSortService.GetJavaZoneNameId("FORT")),
			new WorldMapRegionZoneSortCandidate(
				"zone-sub",
				WorldMapRegionZoneSortClassName.Sub,
				Priority: 0,
				WorldMapRegionZoneSortService.GetJavaZoneNameId("SUB")),
		};

		return WorldMapRegionCreationSnapshotService.CreateSnapshot(210010000, layout, regionId: 1001, zones, sortCandidates);
	}
}
