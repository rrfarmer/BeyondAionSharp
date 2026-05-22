using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
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
	private readonly ThreadPoolManager? _threadPoolManager;
	private readonly ConcurrentDictionary<int, WorldNpcWalkerMovementState> _activeStates = new();
	private readonly ConcurrentDictionary<int, ScheduledTask> _pendingRestTasks = new();

	public WorldNpcWalkerRouteWalkingService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IWorldNpcWalkerSpawnPlanCacheService walkerSpawnPlans,
		WorldNpcWalkerRouteService routeService,
		WorldNpcWalkerMovementStateService movementStates,
		WorldNpcWalkerMovementBroadcastService movementBroadcasts,
		ThreadPoolManager? threadPoolManager = null)
	{
		_runtimeContext = runtimeContext;
		_world = world;
		_walkerSpawnPlans = walkerSpawnPlans;
		_routeService = routeService;
		_movementStates = movementStates;
		_movementBroadcasts = movementBroadcasts;
		_threadPoolManager = threadPoolManager;
	}

	public int ActiveStateCount => _activeStates.Count;

	public int PendingRestTaskCount => _pendingRestTasks.Count;

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

	public async Task<WorldNpcWalkerRouteWalkingTargetReachedResult> TargetReachedAsync(
		int objectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ai/manager/WalkManager.targetReached routes single path walkers to chooseNextRouteStep.
		cancellationToken.ThrowIfCancellationRequested();
		if (!_activeStates.TryGetValue(objectId, out var currentState))
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.MissingState);
		if (currentState.IsFormationMember)
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.FormationMember);

		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null)
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.MissingStaticData);
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.MissingNpc);

		var routePlan = _routeService.ResolveRoute(npc, staticData.WalkerTemplates, staticData.WalkerVersions);
		if (routePlan.Status != WorldNpcWalkerRouteStatus.Ready)
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.MissingRoute);

		var worldPlan = _walkerSpawnPlans.GetWorldPlan(npc.Position.WorldId);
		if (worldPlan == null)
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.MissingWorldPlan);
		var walker = worldPlan.SpawnPlan.Walkers.FirstOrDefault(walker => walker.ObjectId == objectId);
		if (walker == null)
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.NotActiveWalker);

		var advance = _movementStates.AdvanceSingleRouteWalking(currentState, walker, routePlan);
		if (advance.IsStopped)
		{
			CancelPendingRestTask(objectId);
			_activeStates.TryRemove(objectId, out _);
			return WorldNpcWalkerRouteWalkingTargetReachedResult.Stopped();
		}

		var nextState = advance.State;
		if (nextState == null)
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.MissingMovementTarget);

		_activeStates[objectId] = nextState;
		if (advance.RestDelay <= TimeSpan.Zero)
		{
			CancelPendingRestTask(objectId);
			var broadcast = await _movementBroadcasts.BroadcastWalkerMovementAsync(objectId, nextState, cancellationToken: cancellationToken);
			return WorldNpcWalkerRouteWalkingTargetReachedResult.Advanced(nextState, broadcast.SentCount);
		}

		ScheduleRestedBroadcast(objectId, nextState, advance.RestDelay, cancellationToken);
		return WorldNpcWalkerRouteWalkingTargetReachedResult.Scheduled(nextState, advance.RestDelay);
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

	private void ScheduleRestedBroadcast(
		int objectId,
		WorldNpcWalkerMovementState state,
		TimeSpan restDelay,
		CancellationToken cancellationToken)
	{
		// Java parity: WalkManager.chooseNextRouteStep schedules moveToNextPoint after currentStep.restTime.
		CancelPendingRestTask(objectId);
		if (_threadPoolManager == null)
		{
			_ = _movementBroadcasts.BroadcastWalkerMovementAsync(objectId, state, cancellationToken: cancellationToken);
			return;
		}

		var scheduledTask = _threadPoolManager.Schedule(
			async taskCancellationToken =>
			{
				await _movementBroadcasts.BroadcastWalkerMovementAsync(objectId, state, cancellationToken: taskCancellationToken);
			},
			restDelay,
			cancellationToken);
		_pendingRestTasks[objectId] = scheduledTask;
		_ = scheduledTask.Completion.ContinueWith(
			_ => RemovePendingRestTask(objectId, scheduledTask),
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	private void CancelPendingRestTask(int objectId)
	{
		if (_pendingRestTasks.TryRemove(objectId, out var scheduledTask))
			scheduledTask.Cancel();
	}

	private void RemovePendingRestTask(int objectId, ScheduledTask scheduledTask)
	{
		((ICollection<KeyValuePair<int, ScheduledTask>>)_pendingRestTasks).Remove(
			new KeyValuePair<int, ScheduledTask>(objectId, scheduledTask));
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

public sealed record WorldNpcWalkerRouteWalkingTargetReachedResult(
	bool Handled,
	WorldNpcWalkerRouteWalkingTargetReachedStatus Status,
	WorldNpcWalkerMovementState? State,
	TimeSpan RestDelay,
	int BroadcastCount)
{
	public static WorldNpcWalkerRouteWalkingTargetReachedResult Advanced(
		WorldNpcWalkerMovementState state,
		int broadcastCount)
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: true,
			WorldNpcWalkerRouteWalkingTargetReachedStatus.Advanced,
			state,
			RestDelay: TimeSpan.Zero,
			broadcastCount);
	}

	public static WorldNpcWalkerRouteWalkingTargetReachedResult Scheduled(
		WorldNpcWalkerMovementState state,
		TimeSpan restDelay)
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: true,
			WorldNpcWalkerRouteWalkingTargetReachedStatus.Scheduled,
			state,
			restDelay,
			BroadcastCount: 0);
	}

	public static WorldNpcWalkerRouteWalkingTargetReachedResult Stopped()
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: true,
			WorldNpcWalkerRouteWalkingTargetReachedStatus.Stopped,
			State: null,
			RestDelay: TimeSpan.Zero,
			BroadcastCount: 0);
	}

	public static WorldNpcWalkerRouteWalkingTargetReachedResult NotHandled(
		WorldNpcWalkerRouteWalkingTargetReachedStatus status)
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: false,
			status,
			State: null,
			RestDelay: TimeSpan.Zero,
			BroadcastCount: 0);
	}
}

public enum WorldNpcWalkerRouteWalkingTargetReachedStatus
{
	Advanced,
	Scheduled,
	Stopped,
	MissingState,
	FormationMember,
	MissingStaticData,
	MissingNpc,
	MissingRoute,
	MissingWorldPlan,
	NotActiveWalker,
	MissingMovementTarget,
}
