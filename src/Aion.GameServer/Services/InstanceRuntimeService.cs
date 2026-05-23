using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class InstanceRuntimeService
{
	public static WorldMapInstanceRuntimeState GetNextAvailableInstance(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		int ownerId = 0,
		int maxPlayers = 0)
	{
		// Java parity: InstanceService.getNextAvailableInstance validates instance maps before allocating via WorldMapInstanceFactory.
		var map = worldMaps.GetMap(worldId)
			?? throw new InvalidOperationException($"World map {worldId} is not loaded.");
		if (!map.Summary.IsInstance)
			throw new UnsupportedOperationException($"Invalid call for next available instance of {worldId}");

		return map.CreateNextWorldMapInstance(ownerId, maxPlayers);
	}

	public static WorldMapInstanceRuntimeState GetNextAvailableInstanceForPlayer(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		int playerObjectId,
		int maxPlayers = 0)
	{
		// Java parity: InstanceService.getNextAvailableInstance(worldId, player) creates an instance and registers the player object id.
		var instance = GetNextAvailableInstance(worldMaps, worldId, ownerId: 0, maxPlayers);
		instance.Register(playerObjectId);
		return instance;
	}

	public static WorldMapInstanceRuntimeState GetNextAvailableInstanceForPlayer(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		Player player,
		InstanceCooltimeTable instanceCooltimes)
	{
		// Java parity: InstanceService.getNextAvailableInstance(worldId, player) derives max players from InstanceCooltimeData.getMaxMemberCount.
		var maxPlayers = instanceCooltimes.GetMaxMemberCount(worldId, player.Race);
		return GetNextAvailableInstanceForPlayer(worldMaps, worldId, player.ObjectId, maxPlayers);
	}

	public static WorldMapInstanceRuntimeState GetOrRegisterInstance(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		int playerObjectId,
		int maxPlayers = 0)
	{
		// Java parity: InstanceService.getOrRegisterInstance returns a registered instance or creates/registers a new one.
		return worldMaps.GetRegisteredInstance(worldId, playerObjectId)
			?? GetNextAvailableInstanceForPlayer(worldMaps, worldId, playerObjectId, maxPlayers);
	}

	public static WorldMapInstanceRuntimeState GetOrRegisterInstance(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		Player player,
		InstanceCooltimeTable instanceCooltimes)
	{
		// Java parity: InstanceService.getOrRegisterInstance(worldId, player) reuses existing registration before allocating a player-scoped instance.
		return worldMaps.GetRegisteredInstance(worldId, player.ObjectId)
			?? GetNextAvailableInstanceForPlayer(worldMaps, worldId, player, instanceCooltimes);
	}
}

public sealed class UnsupportedOperationException : InvalidOperationException
{
	public UnsupportedOperationException(string message)
		: base(message)
	{
	}
}
