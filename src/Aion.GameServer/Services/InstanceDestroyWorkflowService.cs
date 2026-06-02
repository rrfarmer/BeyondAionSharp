using Aion.GameServer.Dataholders;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class InstanceDestroyWorkflowService
{
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly GameWorld _world;
	private readonly WorldNpcSpawnService _spawnService;
	private readonly WorldNpcWalkerRouteWalkingService _walkerRouteWalking;

	public InstanceDestroyWorkflowService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		WorldNpcSpawnService spawnService,
		WorldNpcWalkerRouteWalkingService walkerRouteWalking)
	{
		_runtimeContext = runtimeContext;
		_world = world;
		_spawnService = spawnService;
		_walkerRouteWalking = walkerRouteWalking;
	}

	public InstanceDestroyWorkflowResult DestroyInstance(int worldId, int instanceId)
	{
		var staticData = _runtimeContext.DataManager?.StaticData;
		return DestroyInstance(worldId, instanceId, staticData?.InstanceExits, staticData?.PlayerInitialData);
	}

	public InstanceDestroyWorkflowResult DestroyInstance(
		int worldId,
		int instanceId,
		InstanceExitTable? instanceExits,
		PlayerInitialDataTable? playerInitialData)
	{
		var unregisteredTemporarySpawnCount = 0;
		WorldNpcWalkerInstanceDestroyCleanupResult? walkerCleanup = null;
		var destroyPlan = InstanceRuntimeService.DestroyInstance(
			_runtimeContext.WorldMapStates,
			worldId,
			instanceId,
			temporarySpawnCleanup: (mapId, destroyedInstanceId) =>
			{
				unregisteredTemporarySpawnCount = _spawnService.UnregisterTemporarySpawnsForInstance(mapId, destroyedInstanceId);
			},
			nonPlayerObjectCleanup: _spawnService.DeleteNonPlayerObjectsForInstance,
			walkerFormationCleanup: (mapId, destroyedInstanceId) =>
			{
				walkerCleanup = _walkerRouteWalking.OnInstanceDestroy(mapId, destroyedInstanceId);
			},
			playerForcedExitPlanning: (mapId, destroyedInstanceId) =>
			{
				if (instanceExits == null || playerInitialData == null)
					return Array.Empty<InstancePlayerForcedExitTeleportPlan>();

				var players = _world.GetPlayers(mapId)
					.Where(player => player.Position.InstanceId == destroyedInstanceId)
					.ToArray();
				return InstanceRuntimeService.CreatePlayerForcedExitTeleportPlans(
					players,
					mapId,
					destroyedInstanceId,
					instanceExits,
					_runtimeContext.WorldMapStates,
					playerInitialData);
			});

		return new InstanceDestroyWorkflowResult(
			destroyPlan,
			unregisteredTemporarySpawnCount,
			walkerCleanup,
			"InstanceService.destroyInstance -> TemporarySpawnEngine.onInstanceDestroy -> player forced exit -> VisibleObjectController.delete -> InstanceHandler.onInstanceDestroy -> WalkerFormator.onInstanceDestroy");
	}
}

public sealed record InstanceDestroyWorkflowResult(
	InstanceDestroyRuntimePlan DestroyPlan,
	int UnregisteredTemporarySpawnCount,
	WorldNpcWalkerInstanceDestroyCleanupResult? WalkerCleanup,
	string JavaSource);
