using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionZoneFilterServiceTests
{
	[Fact]
	public void CreateRegionBounds_For2DLayout_UsesJava2DCreateMapRegionZRange()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 256,
			WorldMapRegionDimension.TwoDimensional);

		var bounds = WorldMapRegionZoneFilterService.CreateRegionBounds(layout, regionId: 1001);

		Assert.Equal(128, bounds.MinX);
		Assert.Equal(128, bounds.MinY);
		Assert.Equal(0, bounds.MinZ);
		Assert.Equal(256, bounds.MaxZ);
		Assert.Equal(256, bounds.MaxX);
		Assert.Equal(256, bounds.MaxY);
	}

	[Fact]
	public void CreateRegionBounds_For3DLayout_UsesJava3DCreateMapRegionZRange()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 256,
			WorldMapRegionDimension.ThreeDimensional);

		var bounds = WorldMapRegionZoneFilterService.CreateRegionBounds(layout, regionId: 1_001_001);

		Assert.Equal(128, bounds.MinX);
		Assert.Equal(128, bounds.MinY);
		Assert.Equal(128, bounds.MinZ);
		Assert.Equal(256, bounds.MaxZ);
		Assert.Equal(256, bounds.MaxX);
		Assert.Equal(256, bounds.MaxY);
	}

	[Fact]
	public void FilterZones_KeepsPolygonCylinderAndSphereIntersectionsForMap()
	{
		var bounds = new WorldMapRegionBounds(128, 128, 0, 256, RegionSize: 128);
		var zones = new[]
		{
			CreatePolygon("polygon", 210010000, 120, 120, 140, 140, 0, 256),
			new WorldMapRegionZoneCandidate("cylinder", 210010000, WorldMapRegionZoneClassName.Other,
				new WorldMapCylinderZoneArea(CenterX: 260, CenterY: 192, Radius: 8, Bottom: 0, Top: 256)),
			new WorldMapRegionZoneCandidate("sphere", 210010000, WorldMapRegionZoneClassName.Other,
				new WorldMapSphereZoneArea(CenterX: 256, CenterY: 256, CenterZ: 128, Radius: 16)),
			CreatePolygon("other-map", 220010000, 120, 120, 140, 140, 0, 256),
			new WorldMapRegionZoneCandidate("outside-z", 210010000, WorldMapRegionZoneClassName.Other,
				new WorldMapCylinderZoneArea(CenterX: 192, CenterY: 192, Radius: 64, Bottom: 300, Top: 400)),
		};

		var result = WorldMapRegionZoneFilterService.FilterZones(210010000, 1001, bounds, zones);

		Assert.Equal(["polygon", "cylinder", "sphere"], result.MatchedZones.Select(zone => zone.ZoneId));
		Assert.Empty(result.DummyZonesMissingWholeMapIntersection);
		Assert.Contains("WorldMapInstance.filterZones", result.JavaSource);
	}

	[Fact]
	public void FilterZones_PreservesJavaRectangleAreaNoIntersectionStubAndDummyMissReport()
	{
		var bounds = new WorldMapRegionBounds(0, 0, 0, 128, RegionSize: 128);
		var zones = new[]
		{
			new WorldMapRegionZoneCandidate("rectangle", 210010000, WorldMapRegionZoneClassName.Other,
				new WorldMapRectangleZoneArea(MinX: 0, MinY: 0, MaxX: 128, MaxY: 128, Bottom: 0, Top: 128)),
			new WorldMapRegionZoneCandidate("dummy", 210010000, WorldMapRegionZoneClassName.Dummy,
				new WorldMapPolygonZoneArea(
				[
					new ZonePoint2D(300, 300),
					new ZonePoint2D(320, 300),
					new ZonePoint2D(320, 320),
					new ZonePoint2D(300, 320),
				], Bottom: 0, Top: 128)),
		};

		var result = WorldMapRegionZoneFilterService.FilterZones(210010000, 0, bounds, zones);

		Assert.Empty(result.MatchedZones);
		Assert.Equal(["dummy"], result.DummyZonesMissingWholeMapIntersection.Select(zone => zone.ZoneId));
	}

	[Fact]
	public void FilterZones_PreservesJavaSemisphereIntersectionCondition()
	{
		var bounds = new WorldMapRegionBounds(0, 0, 0, 128, RegionSize: 128);
		var zones = new[]
		{
			new WorldMapRegionZoneCandidate("semisphere", 400010000, WorldMapRegionZoneClassName.Other,
				new WorldMapSemisphereZoneArea(CenterX: 64, CenterY: 64, CenterZ: 128, Radius: 16)),
		};

		var result = WorldMapRegionZoneFilterService.FilterZones(400010000, 0, bounds, zones);

		Assert.Equal(["semisphere"], result.MatchedZones.Select(zone => zone.ZoneId));
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
