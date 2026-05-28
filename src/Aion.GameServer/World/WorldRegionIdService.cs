namespace Aion.GameServer.World;

public static class WorldRegionIdService
{
	public const int DefaultRegionSize = 128;
	public const int X3DOffset = 1_000_000;
	public const int Y3DOffset = 1_000;
	public const int X2DOffset = 1_000;

	public static int Get2DRegionId(float x, float y, int regionSize = DefaultRegionSize)
	{
		// Java parity breadcrumb: com.aionemu.gameserver.world.RegionUtil.get2DRegionId.
		return (int)x / regionSize * X2DOffset + (int)y / regionSize;
	}

	public static int Get3DRegionId(float x, float y, float z, int regionSize = DefaultRegionSize)
	{
		// Java parity breadcrumb: com.aionemu.gameserver.world.RegionUtil.get3DRegionId.
		return (int)x / regionSize * X3DOffset + (int)y / regionSize * Y3DOffset + (int)z / regionSize;
	}

	public static int GetXFrom2DRegionId(int regionId, int regionSize = DefaultRegionSize)
	{
		return regionId / X2DOffset * regionSize;
	}

	public static int GetYFrom2DRegionId(int regionId, int regionSize = DefaultRegionSize)
	{
		return regionId % X2DOffset * regionSize;
	}

	public static int GetXFrom3DRegionId(int regionId, int regionSize = DefaultRegionSize)
	{
		return regionId / X3DOffset * regionSize;
	}

	public static int GetYFrom3DRegionId(int regionId, int regionSize = DefaultRegionSize)
	{
		return regionId % X3DOffset / Y3DOffset * regionSize;
	}

	public static int GetZFrom3DRegionId(int regionId, int regionSize = DefaultRegionSize)
	{
		return regionId % X3DOffset % Y3DOffset * regionSize;
	}
}
