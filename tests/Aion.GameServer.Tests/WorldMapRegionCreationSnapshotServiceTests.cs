using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionCreationSnapshotServiceTests
{
	[Fact]
	public void CreateSnapshot_For2DRegion_ComposesBoundsNeighboursAndFilteredZones()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 256,
			WorldMapRegionDimension.TwoDimensional);
		var regionId = 1001;
		var zones = new[]
		{
			CreatePolygon("zone-in-region", 210010000, 130, 130, 180, 180, 0, 256),
			CreatePolygon("zone-outside-region", 210010000, 300, 300, 340, 340, 0, 256),
			CreatePolygon("zone-other-map", 220010000, 130, 130, 180, 180, 0, 256),
		};

		var snapshot = WorldMapRegionCreationSnapshotService.CreateSnapshot(210010000, layout, regionId, zones);

		Assert.True(snapshot.RegionExists);
		Assert.Equal(WorldMapRegionDimension.TwoDimensional, snapshot.Dimension);
		Assert.Equal(128, snapshot.Bounds.MinX);
		Assert.Equal(128, snapshot.Bounds.MinY);
		Assert.Equal(0, snapshot.Bounds.MinZ);
		Assert.Equal(256, snapshot.Bounds.MaxZ);
		Assert.Equal([0, 1, 2, 1000, 1002, 2000, 2001, 2002], snapshot.NeighbourRegionIds);
		Assert.Equal(["zone-in-region"], snapshot.ZoneIds);
		Assert.Empty(snapshot.DummyZoneMissIds);
		Assert.Contains("createMapRegion", snapshot.JavaSource);
	}

	[Fact]
	public void CreateSnapshot_For3DRegion_ComposesZBoundsAndFilteredZones()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 256,
			WorldMapRegionDimension.ThreeDimensional);
		var regionId = 1_001_001;
		var zones = new[]
		{
			new WorldMapRegionZoneCandidate("sphere", 400010000, WorldMapRegionZoneClassName.Other,
				new WorldMapSphereZoneArea(CenterX: 192, CenterY: 192, CenterZ: 192, Radius: 16)),
			new WorldMapRegionZoneCandidate("sphere-z-miss", 400010000, WorldMapRegionZoneClassName.Other,
				new WorldMapSphereZoneArea(CenterX: 192, CenterY: 192, CenterZ: 320, Radius: 16)),
		};

		var snapshot = WorldMapRegionCreationSnapshotService.CreateSnapshot(400010000, layout, regionId, zones);

		Assert.True(snapshot.RegionExists);
		Assert.Equal(WorldMapRegionDimension.ThreeDimensional, snapshot.Dimension);
		Assert.Equal(128, snapshot.Bounds.MinZ);
		Assert.Equal(256, snapshot.Bounds.MaxZ);
		Assert.Equal(["sphere"], snapshot.ZoneIds);
	}

	[Fact]
	public void CreateSnapshot_ForRegionIdNotPrecreated_ReturnsMissingSnapshot()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 128,
			WorldMapRegionDimension.ThreeDimensional);
		var notPrecreatedAtMaxZ = 1;

		var snapshot = WorldMapRegionCreationSnapshotService.CreateSnapshot(
			400010000,
			layout,
			notPrecreatedAtMaxZ,
			Array.Empty<WorldMapRegionZoneCandidate>());

		Assert.False(snapshot.RegionExists);
		Assert.Empty(snapshot.NeighbourRegionIds);
		Assert.Empty(snapshot.ZoneIds);
		Assert.Contains("would be null", snapshot.JavaSource);
	}

	private static WorldMapRegionZoneCandidate CreatePolygon(
		string zoneId,
		int mapId,
		float minX,
		float minY,
		float maxX,
		float maxY,
		float bottom,
		float top)
	{
		return new WorldMapRegionZoneCandidate(
			zoneId,
			mapId,
			WorldMapRegionZoneClassName.Other,
			new WorldMapPolygonZoneArea(
			[
				new ZonePoint2D(minX, minY),
				new ZonePoint2D(maxX, minY),
				new ZonePoint2D(maxX, maxY),
				new ZonePoint2D(minX, maxY),
			],
			bottom,
			top));
	}
}
