using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionLayoutServiceTests
{
	[Fact]
	public void CreateLayoutForWorld_UsesJavaDimensionSelector()
	{
		var reshanta = WorldMapRegionLayoutService.CreateLayoutForWorld(
			WorldRegionKeyProjectionService.ReshantaWorldId,
			worldSize: 128);
		var poeta = WorldMapRegionLayoutService.CreateLayoutForWorld(
			210010000,
			worldSize: 128);

		Assert.Equal(WorldMapRegionDimension.ThreeDimensional, reshanta.Dimension);
		Assert.Equal(WorldMapRegionDimension.TwoDimensional, poeta.Dimension);
	}

	[Fact]
	public void CreateLayout_For2DMap_PrecreatesInclusiveXYRegionsAndNeighbours()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 256,
			WorldMapRegionDimension.TwoDimensional);

		Assert.Equal(WorldMapRegionDimension.TwoDimensional, layout.Dimension);
		Assert.Equal(256, layout.MaxZ);
		Assert.Equal([0, 1, 2, 1000, 1001, 1002, 2000, 2001, 2002], layout.RegionIds);
		Assert.Equal([1, 1000, 1001], layout.NeighbourRegionIds[0]);
		Assert.Equal([0, 1, 2, 1000, 1002, 2000, 2001, 2002], layout.NeighbourRegionIds[1001]);
	}

	[Fact]
	public void CreateLayout_For3DMap_PrecreatesInclusiveXYAndExclusiveRoundedZRegions()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 256,
			WorldMapRegionDimension.ThreeDimensional);

		Assert.Equal(WorldMapRegionDimension.ThreeDimensional, layout.Dimension);
		Assert.Equal(256, layout.MaxZ);
		Assert.Equal(18, layout.RegionIds.Count);
		Assert.Equal(0, layout.RegionIds[0]);
		Assert.Equal(1, layout.RegionIds[1]);
		Assert.Contains(2_002_001, layout.RegionIds);
		Assert.DoesNotContain(2_002_002, layout.RegionIds);
	}

	[Fact]
	public void CreateLayout_For3DMap_MatchesJavaNeighbourZLoopShape()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 256,
			WorldMapRegionDimension.ThreeDimensional);
		var lowerCorner = WorldRegionIdService.Get3DRegionId(0f, 0f, 0f);
		var upperCorner = WorldRegionIdService.Get3DRegionId(0f, 0f, 128f);

		Assert.Equal([1_000, 1_000_000, 1_001_000], layout.NeighbourRegionIds[lowerCorner]);
		Assert.Equal([0, 1_000, 1_001, 1_000_000, 1_000_001, 1_001_000, 1_001_001], layout.NeighbourRegionIds[upperCorner]);
	}

	[Fact]
	public void CreateLayout_UsesJavaRoundedMaxZForNonDivisibleWorldSize()
	{
		var layout = WorldMapRegionLayoutService.CreateLayout(
			worldSize: 192,
			WorldMapRegionDimension.ThreeDimensional);

		Assert.Equal(256, layout.MaxZ);
		Assert.Equal([0, 1, 1000, 1001, 1000000, 1000001, 1001000, 1001001], layout.RegionIds);
	}

	[Fact]
	public void ResolvePosition_For2DLayout_ReturnsExistingPrecreatedRegionAndNeighbours()
	{
		var layout = WorldMapRegionLayoutService.CreateLayoutForWorld(
			210010000,
			worldSize: 256);
		var position = new WorldPosition(210010000, X: 128f, Y: 128f, Z: 999f, Heading: 0, InstanceId: 1);

		var resolution = WorldMapRegionLayoutService.ResolvePosition(layout, position);

		Assert.True(resolution.RegionExists);
		Assert.Equal(WorldMapRegionDimension.TwoDimensional, resolution.Dimension);
		Assert.Equal(1001, resolution.RegionId);
		Assert.Equal([0, 1, 2, 1000, 1002, 2000, 2001, 2002], resolution.NeighbourRegionIds);
		Assert.Contains("WorldMap2DInstance.getRegion", resolution.JavaSource);
	}

	[Fact]
	public void ResolvePosition_For3DLayout_ReturnsMissingWhenProjectedRegionWasNotPrecreated()
	{
		var layout = WorldMapRegionLayoutService.CreateLayoutForWorld(
			WorldRegionKeyProjectionService.ReshantaWorldId,
			worldSize: 128);
		var positionAtMaxZ = new WorldPosition(
			WorldRegionKeyProjectionService.ReshantaWorldId,
			X: 0f,
			Y: 0f,
			Z: 128f,
			Heading: 0,
			InstanceId: 1);

		var resolution = WorldMapRegionLayoutService.ResolvePosition(layout, positionAtMaxZ);

		Assert.False(resolution.RegionExists);
		Assert.Equal(WorldMapRegionDimension.ThreeDimensional, resolution.Dimension);
		Assert.Equal(1, resolution.RegionId);
		Assert.Empty(resolution.NeighbourRegionIds);
		Assert.Contains("WorldMap3DInstance.getRegion", resolution.JavaSource);
	}
}
