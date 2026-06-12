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
		IInstanceLifecycleHandler? instanceHandler = null,
		bool autoDestroy = true,
		Action<int, WorldMapInstanceRuntimeState>? emptyInstanceScheduler = null)
	{
		// Java parity: InstanceService.getNextAvailableInstance validates instance maps before allocating via WorldMapInstanceFactory.
		var map = worldMaps.GetMap(worldId)
			?? throw new InvalidOperationException($"World map {worldId} is not loaded.");
		if (!map.Summary.IsInstance)
			throw new UnsupportedOperationException($"Invalid call for next available instance of {worldId}");

		var instance = map.CreateNextWorldMapInstance(ownerId, maxPlayers, difficultyId, instanceHandler);
		if (autoDestroy)
			emptyInstanceScheduler?.Invoke(worldId, instance);
		return instance;
	}

	public static WorldMapInstanceRuntimeState GetNextAvailableInstanceForPlayer(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		int playerObjectId,
		int maxPlayers = 0,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null,
		bool autoDestroy = true,
		Action<int, WorldMapInstanceRuntimeState>? emptyInstanceScheduler = null)
	{
		// Java parity: InstanceService.getNextAvailableInstance(worldId, player) creates an instance and registers the player object id.
		var instance = GetNextAvailableInstance(
			worldMaps,
			worldId,
			ownerId: 0,
			maxPlayers: maxPlayers,
			difficultyId: difficultyId,
			instanceHandler: instanceHandler,
			autoDestroy: autoDestroy,
			emptyInstanceScheduler: emptyInstanceScheduler);
		instance.Register(playerObjectId);
		return instance;
	}

	public static WorldMapInstanceRuntimeState GetNextAvailableInstanceForPlayer(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		Player player,
		InstanceCooltimeTable instanceCooltimes,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null,
		bool autoDestroy = true,
		Action<int, WorldMapInstanceRuntimeState>? emptyInstanceScheduler = null)
	{
		// Java parity: InstanceService.getNextAvailableInstance(worldId, player) derives max players from InstanceCooltimeData.getMaxMemberCount.
		var maxPlayers = instanceCooltimes.GetMaxMemberCount(worldId, player.Race.ToString());
		return GetNextAvailableInstanceForPlayer(
			worldMaps,
			worldId,
			player.ObjectId,
			maxPlayers,
			difficultyId,
			instanceHandler,
			autoDestroy,
			emptyInstanceScheduler);
	}

	public static WorldMapInstanceRuntimeState GetOrRegisterInstance(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		int playerObjectId,
		int maxPlayers = 0,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null,
		bool autoDestroy = true,
		Action<int, WorldMapInstanceRuntimeState>? emptyInstanceScheduler = null)
	{
		// Java parity: InstanceService.getOrRegisterInstance returns a registered instance or creates/registers a new one.
		return worldMaps.GetRegisteredInstance(worldId, playerObjectId)
			?? GetNextAvailableInstanceForPlayer(
				worldMaps,
				worldId,
				playerObjectId,
				maxPlayers,
				difficultyId,
				instanceHandler,
				autoDestroy,
				emptyInstanceScheduler);
	}

	public static WorldMapInstanceRuntimeState GetOrRegisterInstance(
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		Player player,
		InstanceCooltimeTable instanceCooltimes,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null,
		bool autoDestroy = true,
		Action<int, WorldMapInstanceRuntimeState>? emptyInstanceScheduler = null)
	{
		// Java parity: InstanceService.getOrRegisterInstance(worldId, player) reuses existing registration before allocating a player-scoped instance.
		return worldMaps.GetRegisteredInstance(worldId, player.ObjectId)
			?? GetNextAvailableInstanceForPlayer(
				worldMaps,
				worldId,
				player,
				instanceCooltimes,
				difficultyId,
				instanceHandler,
				autoDestroy,
				emptyInstanceScheduler);
	}

	public static InstancePortalRuntimePlan CreatePortalTransferInstance(
		WorldMapRuntimeStateTable worldMaps,
		Player player,
		WorldPosition portalLocation,
		int ownerId = 0,
		int maxPlayers = 0,
		byte difficultyId = 0,
		IInstanceLifecycleHandler? instanceHandler = null,
		bool autoDestroy = true,
		Action<int, WorldMapInstanceRuntimeState>? emptyInstanceScheduler = null)
	{
		// Java parity: PortalService.port creates the next instance, registers requester, then PortalService.transfer sets startPos.
		var instance = GetNextAvailableInstance(
			worldMaps,
			portalLocation.WorldId,
			ownerId,
			maxPlayers,
			difficultyId,
			instanceHandler,
			autoDestroy,
			emptyInstanceScheduler);
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
		Func<int, int, int>? nonPlayerObjectCleanup = null,
		Action<int, int>? walkerFormationCleanup = null,
		Func<int, int, IReadOnlyList<InstancePlayerForcedExitTeleportPlan>>? playerForcedExitPlanning = null)
	{
		// Java parity: InstanceService.destroyInstance removes the WorldMapInstance, unregisters temporary spawns,
		// plans player forced exits, deletes non-player visible objects, calls instance.getInstanceHandler().onInstanceDestroy(),
		// then calls WalkerFormator.onInstanceDestroy(worldId, instanceId).
		if (!worldMaps.TryGetWorldMapInstance(worldId, instanceId, out var instance) || instance == null)
			return InstanceDestroyRuntimePlan.Missing(worldId, instanceId);

		var canceledEmptyInstanceTask = instance.CancelEmptyInstanceTask();
		if (!worldMaps.RemoveWorldMapInstance(worldId, instanceId))
			return InstanceDestroyRuntimePlan.Missing(worldId, instanceId);

		temporarySpawnCleanup?.Invoke(worldId, instance.InstanceId);
		var forcedExitTeleportPlans = playerForcedExitPlanning?.Invoke(worldId, instance.InstanceId) ?? Array.Empty<InstancePlayerForcedExitTeleportPlan>();
		var deletedNonPlayerObjects = nonPlayerObjectCleanup?.Invoke(worldId, instance.InstanceId) ?? 0;
		var notified = instance.NotifyInstanceDestroyed();
		walkerFormationCleanup?.Invoke(worldId, instance.InstanceId);
		return new InstanceDestroyRuntimePlan(
			worldId,
			instance.InstanceId,
			instance,
			Removed: true,
			DestroyHandlerNotified: notified,
			DeletedNonPlayerObjectCount: deletedNonPlayerObjects,
			CanceledEmptyInstanceTask: canceledEmptyInstanceTask,
			ForcedExitTeleportPlans: forcedExitTeleportPlans,
			JavaSource: "InstanceService.destroyInstance removes map instance before instance.getInstanceHandler().onInstanceDestroy()");
	}

	public static IReadOnlyList<InstancePlayerForcedExitPlan> CreatePlayerForcedExitPlans(
		IEnumerable<Player> players,
		int worldId,
		int instanceId)
	{
		// Java parity: InstanceService.destroyInstance sends STR_MSG_LEAVE_INSTANCE_FORCE(0)
		// then calls TeleportService.moveToInstanceExit(player, player.getWorldId(), player.getRace()).
		return players
			.Where(player => player.GetPosition().WorldId == worldId && player.GetPosition().InstanceId == instanceId)
			.Select(player => new InstancePlayerForcedExitPlan(
				player.ObjectId,
				player.GetPosition().WorldId,
				player.GetPosition().InstanceId,
				player.Race.ToString(),
				SmSystemMessage.LeaveInstanceForce(0),
				"TeleportService.moveToInstanceExit(player, player.getWorldId(), player.getRace())"))
			.ToArray();
	}

	public static InstanceExitResolutionPlan ResolveInstanceExit(
		InstanceExitTable instanceExits,
		WorldMapRuntimeStateTable worldMaps,
		int worldId,
		string race)
	{
		// Java parity: TeleportService.moveToInstanceExit uses the race-specific InstanceExit only
		// when InstanceService.instanceExists(exitWorld, 1); otherwise it falls back to bind location.
		var exit = instanceExits.GetInstanceExit(worldId, race);
		if (exit == null)
			return InstanceExitResolutionPlan.BindFallback(worldId, race, "No instance exit found for race/world");

		return worldMaps.InstanceExists(exit.ExitWorldId, 1)
			? InstanceExitResolutionPlan.ExitDestination(worldId, race, exit, exit.ToWorldPosition())
			: InstanceExitResolutionPlan.BindFallback(worldId, race, "Exit world instance 1 does not exist");
	}

	public static IReadOnlyList<InstancePlayerForcedExitResolutionPlan> CreatePlayerForcedExitResolutionPlans(
		IEnumerable<Player> players,
		int worldId,
		int instanceId,
		InstanceExitTable instanceExits,
		WorldMapRuntimeStateTable worldMaps)
	{
		// Java parity: InstanceService.destroyInstance sends the forced-leave packet before
		// InstanceService.moveToExitPoint resolves TeleportService.moveToInstanceExit for each player.
		return CreatePlayerForcedExitPlans(players, worldId, instanceId)
			.Select(plan => new InstancePlayerForcedExitResolutionPlan(
				plan,
				ResolveInstanceExit(instanceExits, worldMaps, plan.WorldId, plan.Race),
				"InstanceService.destroyInstance -> send STR_MSG_LEAVE_INSTANCE_FORCE(0) -> moveToExitPoint"))
			.ToArray();
	}

	public static IReadOnlyList<InstancePlayerForcedExitTeleportPlan> CreatePlayerForcedExitTeleportPlans(
		IEnumerable<Player> players,
		int worldId,
		int instanceId,
		InstanceExitTable instanceExits,
		WorldMapRuntimeStateTable worldMaps,
		PlayerInitialDataTable playerInitialData)
	{
		// Java parity: TeleportService.moveToInstanceExit either teleports to the instance exit
		// or calls moveToBindLocation, which resolves the player's bind point or initial race spawn.
		var playerList = players.ToArray();
		var playersByObjectId = playerList.ToDictionary(player => player.ObjectId);
		return CreatePlayerForcedExitResolutionPlans(playerList, worldId, instanceId, instanceExits, worldMaps)
			.Select(plan =>
			{
				var destination = plan.ExitResolution.Destination;
				return new InstancePlayerForcedExitTeleportPlan(
					plan,
					destination,
					"InstanceService.destroyInstance -> moveToExitPoint -> TeleportService.moveToInstanceExit");
			})
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
	bool CanceledEmptyInstanceTask,
	IReadOnlyList<InstancePlayerForcedExitTeleportPlan> ForcedExitTeleportPlans,
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
			CanceledEmptyInstanceTask: false,
			ForcedExitTeleportPlans: Array.Empty<InstancePlayerForcedExitTeleportPlan>(),
			JavaSource: "InstanceService.destroyInstance is a no-op for unknown or already removed modeled instances");
	}
}

public sealed record InstancePlayerForcedExitPlan(
	int PlayerObjectId,
	int WorldId,
	int InstanceId,
	string Race,
	SmSystemMessage ForceLeaveMessage,
	string MoveToInstanceExitJavaSource);

public sealed record InstancePlayerForcedExitResolutionPlan(
	InstancePlayerForcedExitPlan ForcedExit,
	InstanceExitResolutionPlan ExitResolution,
	string JavaSource);

public sealed record InstancePlayerForcedExitTeleportPlan(
	InstancePlayerForcedExitResolutionPlan ForcedExitResolution,
	WorldPosition? Destination,
	string JavaSource);

public sealed record InstanceExitResolutionPlan(
	InstanceExitResolutionStatus Status,
	int SourceWorldId,
	string Race,
	InstanceExitSummary? Exit,
	WorldPosition? Destination,
	string JavaSource,
	string? FallbackReason)
{
	public static InstanceExitResolutionPlan ExitDestination(
		int sourceWorldId,
		string race,
		InstanceExitSummary exit,
		WorldPosition destination)
	{
		return new InstanceExitResolutionPlan(
			InstanceExitResolutionStatus.ExitDestination,
			sourceWorldId,
			race,
			exit,
			destination,
			"TeleportService.moveToInstanceExit -> teleportTo(exitWorld, x, y, z, h)",
			null);
	}

	public static InstanceExitResolutionPlan BindFallback(int sourceWorldId, string race, string reason)
	{
		return new InstanceExitResolutionPlan(
			InstanceExitResolutionStatus.BindLocationFallback,
			sourceWorldId,
			race,
			null,
			null,
			"TeleportService.moveToInstanceExit -> moveToBindLocation",
			reason);
	}
}

public enum InstanceExitResolutionStatus
{
	ExitDestination,
	BindLocationFallback,
}
