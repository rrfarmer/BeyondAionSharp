using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldRegionIdServiceTests
{
	[Theory]
	[InlineData(0f, 0f, 0)]
	[InlineData(127.99f, 127.99f, 0)]
	[InlineData(128f, 0f, 1000)]
	[InlineData(0f, 128f, 1)]
	[InlineData(256f, 384f, 2003)]
	[InlineData(255.99f, 255.99f, 1001)]
	public void Get2DRegionId_UsesJavaIntegerCastAndRegionOffsets(float x, float y, int expectedRegionId)
	{
		Assert.Equal(expectedRegionId, WorldRegionIdService.Get2DRegionId(x, y));
	}

	[Theory]
	[InlineData(0f, 0f, 0f, 0)]
	[InlineData(127.99f, 127.99f, 127.99f, 0)]
	[InlineData(128f, 0f, 0f, 1000000)]
	[InlineData(0f, 128f, 0f, 1000)]
	[InlineData(0f, 0f, 128f, 1)]
	[InlineData(256f, 384f, 512f, 2003004)]
	[InlineData(255.99f, 255.99f, 255.99f, 1001001)]
	public void Get3DRegionId_UsesJavaIntegerCastAndRegionOffsets(float x, float y, float z, int expectedRegionId)
	{
		Assert.Equal(expectedRegionId, WorldRegionIdService.Get3DRegionId(x, y, z));
	}

	[Fact]
	public void GetRegionStartCoordinates_ReversesJavaRegionIdComponents()
	{
		var region2D = WorldRegionIdService.Get2DRegionId(256f, 384f);
		var region3D = WorldRegionIdService.Get3DRegionId(256f, 384f, 512f);

		Assert.Equal(256, WorldRegionIdService.GetXFrom2DRegionId(region2D));
		Assert.Equal(384, WorldRegionIdService.GetYFrom2DRegionId(region2D));
		Assert.Equal(256, WorldRegionIdService.GetXFrom3DRegionId(region3D));
		Assert.Equal(384, WorldRegionIdService.GetYFrom3DRegionId(region3D));
		Assert.Equal(512, WorldRegionIdService.GetZFrom3DRegionId(region3D));
	}

	[Fact]
	public void GetRegionIds_SupportCustomRegionSizeLikeJavaOverloads()
	{
		Assert.Equal(1001, WorldRegionIdService.Get2DRegionId(64f, 64f, regionSize: 64));
		Assert.Equal(1001001, WorldRegionIdService.Get3DRegionId(64f, 64f, 64f, regionSize: 64));
		Assert.Equal(64, WorldRegionIdService.GetXFrom2DRegionId(1001, regionSize: 64));
		Assert.Equal(64, WorldRegionIdService.GetYFrom2DRegionId(1001, regionSize: 64));
		Assert.Equal(64, WorldRegionIdService.GetXFrom3DRegionId(1001001, regionSize: 64));
		Assert.Equal(64, WorldRegionIdService.GetYFrom3DRegionId(1001001, regionSize: 64));
		Assert.Equal(64, WorldRegionIdService.GetZFrom3DRegionId(1001001, regionSize: 64));
	}
}
