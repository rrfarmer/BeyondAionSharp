using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class WorldRegionKeyProjectionService
{
	public const int ReshantaWorldId = 400010000;

	public static WorldMapRegionDimension GetJavaRegionDimension(int worldId)
	{
		// Java parity breadcrumb: WorldMapInstanceFactory.createWorldMapInstance
		// creates WorldMap3DInstance only for WorldMapType.RESHANTA.
		return worldId == ReshantaWorldId
			? WorldMapRegionDimension.ThreeDimensional
			: WorldMapRegionDimension.TwoDimensional;
	}

	public static NearbyQuestRegionKey CreateNearbyRegionKey(
		WorldPosition position,
		WorldMapRegionDimension dimension,
		int regionSize = WorldRegionIdService.DefaultRegionSize)
	{
		return dimension switch
		{
			WorldMapRegionDimension.TwoDimensional => CreateNearby2DRegionKey(position, regionSize),
			WorldMapRegionDimension.ThreeDimensional => CreateNearby3DRegionKey(position, regionSize),
			_ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unsupported Java world-map region dimension."),
		};
	}

	public static NearbyQuestRegionKey CreateNearbyRegionKey(
		WorldPosition position,
		int regionSize = WorldRegionIdService.DefaultRegionSize)
	{
		return CreateNearbyRegionKey(position, GetJavaRegionDimension(position.WorldId), regionSize);
	}

	public static NearbyQuestRegionKey CreateNearby2DRegionKey(
		WorldPosition position,
		int regionSize = WorldRegionIdService.DefaultRegionSize)
	{
		// Java parity breadcrumb: WorldMap2DInstance.getRegion delegates to
		// RegionUtil.get2dRegionId(x, y), ignoring z.
		return new NearbyQuestRegionKey(
			position.WorldId,
			position.InstanceId,
			WorldRegionIdService.Get2DRegionId(position.X, position.Y, regionSize));
	}

	public static NearbyQuestRegionKey CreateNearby3DRegionKey(
		WorldPosition position,
		int regionSize = WorldRegionIdService.DefaultRegionSize)
	{
		// Java parity breadcrumb: WorldMap3DInstance.getRegion delegates to
		// RegionUtil.get3dRegionId(x, y, z).
		return new NearbyQuestRegionKey(
			position.WorldId,
			position.InstanceId,
			WorldRegionIdService.Get3DRegionId(position.X, position.Y, position.Z, regionSize));
	}

	public static PlayerKnownListRegionKey CreateKnownList2DRegionKey(
		WorldPosition position,
		int regionSize = WorldRegionIdService.DefaultRegionSize)
	{
		return new PlayerKnownListRegionKey(
			position.WorldId,
			position.InstanceId,
			WorldRegionIdService.Get2DRegionId(position.X, position.Y, regionSize));
	}

	public static PlayerKnownListRegionKey CreateKnownListRegionKey(
		WorldPosition position,
		WorldMapRegionDimension dimension,
		int regionSize = WorldRegionIdService.DefaultRegionSize)
	{
		return dimension switch
		{
			WorldMapRegionDimension.TwoDimensional => CreateKnownList2DRegionKey(position, regionSize),
			WorldMapRegionDimension.ThreeDimensional => CreateKnownList3DRegionKey(position, regionSize),
			_ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unsupported Java world-map region dimension."),
		};
	}

	public static PlayerKnownListRegionKey CreateKnownListRegionKey(
		WorldPosition position,
		int regionSize = WorldRegionIdService.DefaultRegionSize)
	{
		return CreateKnownListRegionKey(position, GetJavaRegionDimension(position.WorldId), regionSize);
	}

	public static PlayerKnownListRegionKey CreateKnownList3DRegionKey(
		WorldPosition position,
		int regionSize = WorldRegionIdService.DefaultRegionSize)
	{
		return new PlayerKnownListRegionKey(
			position.WorldId,
			position.InstanceId,
			WorldRegionIdService.Get3DRegionId(position.X, position.Y, position.Z, regionSize));
	}
}

public enum WorldMapRegionDimension
{
	TwoDimensional,
	ThreeDimensional,
}
