using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class InstanceRuntimeService
{
	public static WorldMapInstanceRuntimeState GetNextAvailableInstance(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		int ownerId = 0,
		int maxPlayers = 0,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null)
	{
		// Java parity: InstanceService.getNextAvailableInstance validates instance maps before allocating via WorldMapInstanceFactory.
		var map = worldMaps.GetMap(worldId)
			?? throw new InvalidOperationException($"World map {worldId} is not loaded.");
		if (!map.Summary.IsInstance)
			throw new UnsupportedOperationException($"Invalid call for next available instance of {worldId}");

		return map.CreateNextWorldMapInstance(ownerId, maxPlayers, difficultyId, instanceHandler);
	}

	public static WorldMapInstanceRuntimeState GetNextAvailableInstanceForPlayer(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		int playerObjectId,
		int maxPlayers = 0,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null)
	{
		// Java parity: InstanceService.getNextAvailableInstance(worldId, player) creates an instance and registers the player object id.
		var instance = GetNextAvailableInstance(
			worldMaps,
			worldId,
			ownerId: 0,
			maxPlayers: maxPlayers,
			difficultyId: difficultyId,
			instanceHandler: instanceHandler);
		instance.Register(playerObjectId);
		return instance;
	}

	public static WorldMapInstanceRuntimeState GetNextAvailableInstanceForPlayer(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		Player player,
		InstanceCooltimeTable instanceCooltimes,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null)
	{
		// Java parity: InstanceService.getNextAvailableInstance(worldId, player) derives max players from InstanceCooltimeData.getMaxMemberCount.
		var maxPlayers = instanceCooltimes.GetMaxMemberCount(worldId, player.Race);
		return GetNextAvailableInstanceForPlayer(worldMaps, worldId, player.ObjectId, maxPlayers, difficultyId, instanceHandler);
	}

	public static WorldMapInstanceRuntimeState GetOrRegisterInstance(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		int playerObjectId,
		int maxPlayers = 0,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null)
	{
		// Java parity: InstanceService.getOrRegisterInstance returns a registered instance or creates/registers a new one.
		return worldMaps.GetRegisteredInstance(worldId, playerObjectId)
			?? GetNextAvailableInstanceForPlayer(worldMaps, worldId, playerObjectId, maxPlayers, difficultyId, instanceHandler);
	}

	public static WorldMapInstanceRuntimeState GetOrRegisterInstance(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		Player player,
		InstanceCooltimeTable instanceCooltimes,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null)
	{
		// Java parity: InstanceService.getOrRegisterInstance(worldId, player) reuses existing registration before allocating a player-scoped instance.
		return worldMaps.GetRegisteredInstance(worldId, player.ObjectId)
			?? GetNextAvailableInstanceForPlayer(worldMaps, worldId, player, instanceCooltimes, difficultyId, instanceHandler);
	}

	public static InstancePortalRuntimePlan CreatePortalTransferInstance(
		WorldMapRuntimeStateTable worldMaps,
		Player player,
		WorldPosition portalLocation,
		int ownerId = 0,
		int maxPlayers = 0,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null)
	{
		// Java parity: PortalService.port creates the next instance, registers requester, then PortalService.transfer sets startPos.
		var instance = GetNextAvailableInstance(worldMaps, portalLocation.WorldId, ownerId, maxPlayers, difficultyId, instanceHandler);
		instance.Register(player.ObjectId);
		var startPosition = portalLocation with { InstanceId = instance.InstanceId };
		instance.SetStartPositionIfMissing(startPosition);
		return new InstancePortalRuntimePlan(instance, startPosition);
	}

	public static InstanceDestroyRuntimePlan DestroyInstance(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		int instanceId,
		Action<int, int>? temporarySpawnCleanup = null,
		Func<int, int, int>? nonPlayerObjectCleanup = null)
	{
		// Java parity: InstanceService.destroyInstance removes the WorldMapInstance, unregisters temporary spawns,
		// deletes non-player visible objects, then calls instance.getInstanceHandler().onInstanceDestroy().
		if (!worldMaps.TryGetWorldMapInstance(worldId, instanceId, out var instance) || instance == null)
			return InstanceDestroyRuntimePlan.Missing(worldId, instanceId);

		if (!worldMaps.RemoveWorldMapInstance(worldId, instanceId))
			return InstanceDestroyRuntimePlan.Missing(worldId, instanceId);

		temporarySpawnCleanup?.Invoke(worldId, instance.InstanceId);
		var deletedNonPlayerObjects = nonPlayerObjectCleanup?.Invoke(worldId, instance.InstanceId) ?? 0;
		var notified = instance.NotifyInstanceDestroyed();
		return new InstanceDestroyRuntimePlan(
			worldId,
			instance.InstanceId,
			instance,
			Removed: true,
			DestroyHandlerNotified: notified,
			DeletedNonPlayerObjectCount: deletedNonPlayerObjects,
			"InstanceService.destroyInstance removes map instance before instance.getInstanceHandler().onInstanceDestroy()");
	}

	public static IReadOnlyList<InstancePlayerForcedExitPlan> CreatePlayerForcedExitPlans(
		IEnumerable<Player> players,
		int worldId,
		int instanceId)
	{
		// Java parity: InstanceService.destroyInstance sends STR_MSG_LEAVE_INSTANCE_FORCE(0)
		// then calls TeleportService.moveToInstanceExit(player, player.getWorldId(), player.getRace()).
		return players
			.Where(player => player.Position.WorldId == worldId && player.Position.InstanceId == instanceId)
			.Select(player => new InstancePlayerForcedExitPlan(
				player.ObjectId,
				player.Position.WorldId,
				player.Position.InstanceId,
				player.Race,
				SmSystemMessage.LeaveInstanceForce(0),
				"TeleportService.moveToInstanceExit(player, player.getWorldId(), player.getRace())"))
			.ToArray();
	}
}

public sealed class UnsupportedOperationException : InvalidOperationException
{
	public UnsupportedOperationException(string message)
		: base(message)
	{
	}
}

public sealed record InstancePortalRuntimePlan(
	WorldMapInstanceRuntimeState Instance,
	WorldPosition Destination);

public sealed record InstanceDestroyRuntimePlan(
	int WorldId,
	int InstanceId,
	WorldMapInstanceRuntimeState? Instance,
	bool Removed,
	bool DestroyHandlerNotified,
	int DeletedNonPlayerObjectCount,
	string JavaSource)
{
	public static InstanceDestroyRuntimePlan Missing(int worldId, int instanceId)
	{
		return new InstanceDestroyRuntimePlan(
			worldId,
			instanceId == 0 ? 1 : instanceId,
			null,
			Removed: false,
			DestroyHandlerNotified: false,
			DeletedNonPlayerObjectCount: 0,
			"InstanceService.destroyInstance is a no-op for unknown or already removed modeled instances");
	}
}

public sealed record InstancePlayerForcedExitPlan(
	int PlayerObjectId,
	int WorldId,
	int InstanceId,
	string Race,
	SmSystemMessage ForceLeaveMessage,
	string MoveToInstanceExitJavaSource);
