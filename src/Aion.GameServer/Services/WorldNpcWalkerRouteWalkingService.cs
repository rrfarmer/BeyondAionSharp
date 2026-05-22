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
	private readonly ConcurrentDictionary<int, WorldNpcWalkerFormationKey> _formationKeysByObjectId = new();
	private readonly ConcurrentDictionary<WorldNpcWalkerFormationKey, WorldNpcWalkerFormationRuntimeState> _formationStates = new();
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

	public int ActiveFormationStateCount => _formationStates.Count;

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
			return await StartFormationAsync(worldPlan.WorldId, formation, routePlan, cancellationToken);

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
		if (!TryUpdateNpcPositionToReachedTarget(objectId, currentState))
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.MissingNpc);
		if (currentState.IsFormationMember)
			return await TargetReachedFormationMemberAsync(objectId, currentState, routePlan, worldPlan, cancellationToken);

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

	private async Task<WorldNpcWalkerRouteWalkingTargetReachedResult> TargetReachedFormationMemberAsync(
		int objectId,
		WorldNpcWalkerMovementState currentState,
		WorldNpcWalkerRoutePlan routePlan,
		WorldNpcWalkerWorldSpawnPlan worldPlan,
		CancellationToken cancellationToken)
	{
		// Java parity: spawnengine/WalkerGroup.targetReached waits until every member is in WALK_WAIT_GROUP before advancing the route.
		var formation = worldPlan.SpawnPlan.Formations.FirstOrDefault(formation =>
			formation.Members.Any(member => member.ObjectId == objectId));
		if (formation == null)
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.NotActiveWalker);
		if (!_formationKeysByObjectId.TryGetValue(objectId, out var formationKey))
			formationKey = CreateFormationKey(worldPlan.WorldId, formation);
		if (!_formationStates.TryGetValue(formationKey, out var runtimeState))
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.MissingFormationState);

		WorldNpcWalkerFormationMovementAdvance advance;
		lock (runtimeState.SyncRoot)
		{
			runtimeState.ArrivedObjectIds.Add(objectId);
			if (runtimeState.ArrivedObjectIds.Count < formation.Members.Count)
			{
				return WorldNpcWalkerRouteWalkingTargetReachedResult.WaitingGroup(
					currentState,
					runtimeState.ArrivedObjectIds.Count,
					formation.Members.Count);
			}

			advance = _movementStates.AdvanceFormationRouteWalking(runtimeState.MovementState, formation, routePlan);
			runtimeState.MovementState = advance.State;
			runtimeState.ArrivedObjectIds.Clear();
			foreach (var memberState in advance.State.MemberStates)
				_activeStates[memberState.ObjectId] = memberState;
		}

		if (advance.RestDelay <= TimeSpan.Zero)
		{
			CancelPendingRestTasks(advance.State.MemberStates);
			var sentCount = await BroadcastStatesAsync(advance.State.MemberStates, cancellationToken);
			return WorldNpcWalkerRouteWalkingTargetReachedResult.AdvancedGroup(advance.State.MemberStates, sentCount);
		}

		foreach (var memberState in advance.State.MemberStates)
			ScheduleRestedBroadcast(memberState.ObjectId, memberState, advance.RestDelay, cancellationToken);
		return WorldNpcWalkerRouteWalkingTargetReachedResult.ScheduledGroup(advance.State.MemberStates, advance.RestDelay);
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
		int worldId,
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
		var formationKey = CreateFormationKey(worldId, formation);
		_formationStates[formationKey] = new WorldNpcWalkerFormationRuntimeState(formationState);
		var sentCount = 0;
		foreach (var state in formationState.MemberStates)
		{
			cancellationToken.ThrowIfCancellationRequested();
			_formationKeysByObjectId[state.ObjectId] = formationKey;
			_activeStates[state.ObjectId] = state;
			var broadcast = await _movementBroadcasts.BroadcastWalkerMovementAsync(state.ObjectId, state, cancellationToken: cancellationToken);
			sentCount += broadcast.SentCount;
		}

		return WorldNpcWalkerRouteWalkingStartResult.CreateStarted(formationState.MemberStates, sentCount);
	}

	private async Task<int> BroadcastStatesAsync(
		IReadOnlyList<WorldNpcWalkerMovementState> states,
		CancellationToken cancellationToken)
	{
		var sentCount = 0;
		foreach (var state in states)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var broadcast = await _movementBroadcasts.BroadcastWalkerMovementAsync(state.ObjectId, state, cancellationToken: cancellationToken);
			sentCount += broadcast.SentCount;
		}

		return sentCount;
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

		ScheduledTask? scheduledTask = null;
		scheduledTask = _threadPoolManager.Schedule(
			async taskCancellationToken =>
			{
				try
				{
					await _movementBroadcasts.BroadcastWalkerMovementAsync(objectId, state, cancellationToken: taskCancellationToken);
				}
				finally
				{
					if (scheduledTask != null)
						RemovePendingRestTask(objectId, scheduledTask);
				}
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

	private void CancelPendingRestTasks(IReadOnlyList<WorldNpcWalkerMovementState> states)
	{
		foreach (var state in states)
			CancelPendingRestTask(state.ObjectId);
	}

	private void RemovePendingRestTask(int objectId, ScheduledTask scheduledTask)
	{
		((ICollection<KeyValuePair<int, ScheduledTask>>)_pendingRestTasks).Remove(
			new KeyValuePair<int, ScheduledTask>(objectId, scheduledTask));
	}

	private bool TryUpdateNpcPositionToReachedTarget(
		int objectId,
		WorldNpcWalkerMovementState currentState)
	{
		// Java parity: controllers/movement/NpcMoveController.updatePosition has moved the owner to pointX/Y/Z before targetReached fires.
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return false;

		var updatedNpc = npc with
		{
			Position = npc.Position with
			{
				X = currentState.Target.X,
				Y = currentState.Target.Y,
				Z = currentState.Target.Z,
			},
		};
		return _world.TryUpdateObject(objectId, updatedNpc);
	}

	private static WorldNpcWalkerFormationKey CreateFormationKey(
		int worldId,
		WorldNpcWalkerFormationResult formation)
	{
		return new WorldNpcWalkerFormationKey(
			worldId,
			formation.RouteId,
			formation.VersionRouteId,
			string.Join(",", formation.Members.Select(member => member.ObjectId).OrderBy(objectId => objectId)));
	}

	private sealed class WorldNpcWalkerFormationRuntimeState
	{
		public WorldNpcWalkerFormationRuntimeState(WorldNpcWalkerFormationMovementState movementState)
		{
			MovementState = movementState;
		}

		public object SyncRoot { get; } = new();

		public WorldNpcWalkerFormationMovementState MovementState { get; set; }

		public HashSet<int> ArrivedObjectIds { get; } = [];
	}

	private readonly record struct WorldNpcWalkerFormationKey(
		int WorldId,
		string RouteId,
		string VersionRouteId,
		string MemberObjectIds);
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
	IReadOnlyList<WorldNpcWalkerMovementState> States,
	TimeSpan RestDelay,
	int BroadcastCount,
	int ArrivedCount,
	int ExpectedArrivalCount)
{
	public static WorldNpcWalkerRouteWalkingTargetReachedResult Advanced(
		WorldNpcWalkerMovementState state,
		int broadcastCount)
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: true,
			WorldNpcWalkerRouteWalkingTargetReachedStatus.Advanced,
			state,
			[state],
			RestDelay: TimeSpan.Zero,
			broadcastCount,
			ArrivedCount: 0,
			ExpectedArrivalCount: 0);
	}

	public static WorldNpcWalkerRouteWalkingTargetReachedResult AdvancedGroup(
		IReadOnlyList<WorldNpcWalkerMovementState> states,
		int broadcastCount)
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: true,
			WorldNpcWalkerRouteWalkingTargetReachedStatus.Advanced,
			State: null,
			states,
			RestDelay: TimeSpan.Zero,
			broadcastCount,
			ArrivedCount: states.Count,
			ExpectedArrivalCount: states.Count);
	}

	public static WorldNpcWalkerRouteWalkingTargetReachedResult Scheduled(
		WorldNpcWalkerMovementState state,
		TimeSpan restDelay)
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: true,
			WorldNpcWalkerRouteWalkingTargetReachedStatus.Scheduled,
			state,
			[state],
			restDelay,
			BroadcastCount: 0,
			ArrivedCount: 0,
			ExpectedArrivalCount: 0);
	}

	public static WorldNpcWalkerRouteWalkingTargetReachedResult ScheduledGroup(
		IReadOnlyList<WorldNpcWalkerMovementState> states,
		TimeSpan restDelay)
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: true,
			WorldNpcWalkerRouteWalkingTargetReachedStatus.Scheduled,
			State: null,
			states,
			restDelay,
			BroadcastCount: 0,
			ArrivedCount: states.Count,
			ExpectedArrivalCount: states.Count);
	}

	public static WorldNpcWalkerRouteWalkingTargetReachedResult WaitingGroup(
		WorldNpcWalkerMovementState state,
		int arrivedCount,
		int expectedArrivalCount)
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: true,
			WorldNpcWalkerRouteWalkingTargetReachedStatus.WaitingGroup,
			state,
			[state],
			RestDelay: TimeSpan.Zero,
			BroadcastCount: 0,
			arrivedCount,
			expectedArrivalCount);
	}

	public static WorldNpcWalkerRouteWalkingTargetReachedResult Stopped()
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: true,
			WorldNpcWalkerRouteWalkingTargetReachedStatus.Stopped,
			State: null,
			States: Array.Empty<WorldNpcWalkerMovementState>(),
			RestDelay: TimeSpan.Zero,
			BroadcastCount: 0,
			ArrivedCount: 0,
			ExpectedArrivalCount: 0);
	}

	public static WorldNpcWalkerRouteWalkingTargetReachedResult NotHandled(
		WorldNpcWalkerRouteWalkingTargetReachedStatus status)
	{
		return new WorldNpcWalkerRouteWalkingTargetReachedResult(
			Handled: false,
			status,
			State: null,
			States: Array.Empty<WorldNpcWalkerMovementState>(),
			RestDelay: TimeSpan.Zero,
			BroadcastCount: 0,
			ArrivedCount: 0,
			ExpectedArrivalCount: 0);
	}
}

public enum WorldNpcWalkerRouteWalkingTargetReachedStatus
{
	Advanced,
	Scheduled,
	WaitingGroup,
	Stopped,
	MissingState,
	FormationMember,
	MissingFormationState,
	MissingStaticData,
	MissingNpc,
	MissingRoute,
	MissingWorldPlan,
	NotActiveWalker,
	MissingMovementTarget,
}
