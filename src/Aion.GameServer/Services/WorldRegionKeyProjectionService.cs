using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class WorldRegionKeyProjectionService
{
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
