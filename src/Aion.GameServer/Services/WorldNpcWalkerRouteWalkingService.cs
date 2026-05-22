using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerRouteWalkingService
{
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly GameWorld _world;
	private readonly IWorldNpcWalkerSpawnPlanCacheService _walkerSpawnPlans;
	private readonly WorldNpcWalkerRouteService _routeService;
	private readonly WorldNpcWalkerMovementStateService _movementStates;
	private readonly WorldNpcWalkerMovementBroadcastService _movementBroadcasts;
	private readonly ConcurrentDictionary<int, WorldNpcWalkerMovementState> _activeStates = new();

	public WorldNpcWalkerRouteWalkingService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IWorldNpcWalkerSpawnPlanCacheService walkerSpawnPlans,
		WorldNpcWalkerRouteService routeService,
		WorldNpcWalkerMovementStateService movementStates,
		WorldNpcWalkerMovementBroadcastService movementBroadcasts)
	{
		_runtimeContext = runtimeContext;
		_world = world;
		_walkerSpawnPlans = walkerSpawnPlans;
		_routeService = routeService;
		_movementStates = movementStates;
		_movementBroadcasts = movementBroadcasts;
	}

	public int ActiveStateCount => _activeStates.Count;

	public bool TryGetActiveState(int objectId, out WorldNpcWalkerMovementState? state)
	{
		return _activeStates.TryGetValue(objectId, out state);
	}

	public async Task<WorldNpcWalkerRouteWalkingStartResult> StartRouteWalkingAsync(
		int objectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ai/manager/WalkManager.startRouteWalking starts only spawned path walkers and initializes their route target.
		cancellationToken.ThrowIfCancellationRequested();
		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null)
			return WorldNpcWalkerRouteWalkingStartResult.NotStarted(WorldNpcWalkerRouteWalkingStartStatus.MissingStaticData);

		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return WorldNpcWalkerRouteWalkingStartResult.NotStarted(WorldNpcWalkerRouteWalkingStartStatus.MissingNpc);

		var routePlan = _routeService.ResolveRoute(npc, staticData.WalkerTemplates, staticData.WalkerVersions);
		if (routePlan.Status != WorldNpcWalkerRouteStatus.Ready)
			return WorldNpcWalkerRouteWalkingStartResult.NotStarted(WorldNpcWalkerRouteWalkingStartStatus.MissingRoute);

		var worldPlan = _walkerSpawnPlans.GetWorldPlan(npc.Position.WorldId);
		if (worldPlan == null)
			return WorldNpcWalkerRouteWalkingStartResult.NotStarted(WorldNpcWalkerRouteWalkingStartStatus.MissingWorldPlan);

		var singleWalker = worldPlan.SpawnPlan.Walkers.FirstOrDefault(walker => walker.ObjectId == objectId);
		if (singleWalker != null)
			return await StartSingleWalkerAsync(singleWalker, npc, routePlan, cancellationToken);

		var formation = worldPlan.SpawnPlan.Formations.FirstOrDefault(formation =>
			formation.Members.Any(member => member.ObjectId == objectId));
		if (formation != null)
			return await StartFormationAsync(formation, routePlan, cancellationToken);

		return WorldNpcWalkerRouteWalkingStartResult.NotStarted(WorldNpcWalkerRouteWalkingStartStatus.NotActiveWalker);
	}

	private async Task<WorldNpcWalkerRouteWalkingStartResult> StartSingleWalkerAsync(
		WorldNpcWalkerSpawnCandidate walker,
		WorldNpc npc,
		WorldNpcWalkerRoutePlan routePlan,
		CancellationToken cancellationToken)
	{
		var state = _movementStates.StartSingleRouteWalking(walker, npc.Position, routePlan);
		if (state == null)
			return WorldNpcWalkerRouteWalkingStartResult.NotStarted(WorldNpcWalkerRouteWalkingStartStatus.MissingMovementTarget);

		_activeStates[walker.ObjectId] = state;
		var broadcast = await _movementBroadcasts.BroadcastWalkerMovementAsync(walker.ObjectId, state, cancellationToken: cancellationToken);
		return WorldNpcWalkerRouteWalkingStartResult.CreateStarted([state], broadcast.SentCount);
	}

	private async Task<WorldNpcWalkerRouteWalkingStartResult> StartFormationAsync(
		WorldNpcWalkerFormationResult formation,
		WorldNpcWalkerRoutePlan routePlan,
		CancellationToken cancellationToken)
	{
		// Java parity: WalkManager.findClosestRouteStep returns WalkerGroup.groupStep for grouped walkers; initial groupStep is 0.
		var formationState = _movementStates.CreateFormationRouteState(
			formation,
			routePlan,
			currentStepIndex: 0,
			targetStepIndex: 0);
		var sentCount = 0;
		foreach (var state in formationState.MemberStates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			_activeStates[state.ObjectId] = state;
			var broadcast = await _movementBroadcasts.BroadcastWalkerMovementAsync(state.ObjectId, state, cancellationToken: cancellationToken);
			sentCount += broadcast.SentCount;
		}

		return WorldNpcWalkerRouteWalkingStartResult.CreateStarted(formationState.MemberStates, sentCount);
	}
}

public sealed record WorldNpcWalkerRouteWalkingStartResult(
	bool Started,
	WorldNpcWalkerRouteWalkingStartStatus Status,
	IReadOnlyList<WorldNpcWalkerMovementState> States,
	int BroadcastCount)
{
	public static WorldNpcWalkerRouteWalkingStartResult CreateStarted(
		IReadOnlyList<WorldNpcWalkerMovementState> states,
		int broadcastCount)
	{
		return new WorldNpcWalkerRouteWalkingStartResult(
			Started: true,
			WorldNpcWalkerRouteWalkingStartStatus.Started,
			states,
			broadcastCount);
	}

	public static WorldNpcWalkerRouteWalkingStartResult NotStarted(
		WorldNpcWalkerRouteWalkingStartStatus status)
	{
		return new WorldNpcWalkerRouteWalkingStartResult(
			Started: false,
			status,
			Array.Empty<WorldNpcWalkerMovementState>(),
			BroadcastCount: 0);
	}
}

public enum WorldNpcWalkerRouteWalkingStartStatus
{
	Started,
	MissingStaticData,
	MissingNpc,
	MissingRoute,
	MissingWorldPlan,
	NotActiveWalker,
	MissingMovementTarget,
}
