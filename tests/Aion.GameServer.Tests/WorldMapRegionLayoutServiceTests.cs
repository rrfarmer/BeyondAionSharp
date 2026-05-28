using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldMapRegionLayoutServiceTests
{
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
}
