using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcWalkerRouteWalkingService
{
	private static readonly TimeSpan MoveTaskUpdatePeriod = TimeSpan.FromMilliseconds(200);
	private const double MoveOffset = 0.05d;
	private readonly GameServerRuntimeContext _runtimeContext;
	private readonly GameWorld _world;
	private readonly IWorldNpcWalkerSpawnPlanCacheService _walkerSpawnPlans;
	private readonly WorldNpcWalkerRouteService _routeService;
	private readonly WorldNpcWalkerMovementStateService _movementStates;
	private readonly WorldNpcWalkerMovementBroadcastService _movementBroadcasts;
	private readonly ThreadPoolManager? _threadPoolManager;
	private readonly WorldNpcAiStateService? _npcAiStates;
	private readonly CreaturePvpZoneCounterService? _creaturePvpZoneCounterService;
	private readonly ConcurrentDictionary<int, WorldNpcWalkerMovementState> _activeStates = new();
	private readonly ConcurrentDictionary<int, WorldNpcWalkerFormationKey> _formationKeysByObjectId = new();
	private readonly ConcurrentDictionary<WorldNpcWalkerFormationKey, WorldNpcWalkerFormationRuntimeState> _formationStates = new();
	private readonly ConcurrentDictionary<int, ScheduledTask> _pendingRestTasks = new();
	private readonly ConcurrentDictionary<int, ScheduledTask> _pendingArrivalTasks = new();
	private readonly ConcurrentDictionary<int, ScheduledTask> _pendingMovementTickTasks = new();

	public WorldNpcWalkerRouteWalkingService(
		GameServerRuntimeContext runtimeContext,
		GameWorld world,
		IWorldNpcWalkerSpawnPlanCacheService walkerSpawnPlans,
		WorldNpcWalkerRouteService routeService,
		WorldNpcWalkerMovementStateService movementStates,
		WorldNpcWalkerMovementBroadcastService movementBroadcasts,
		ThreadPoolManager? threadPoolManager = null,
		WorldNpcAiStateService? npcAiStates = null,
		CreaturePvpZoneCounterService? creaturePvpZoneCounterService = null)
	{
		_runtimeContext = runtimeContext;
		_world = world;
		_walkerSpawnPlans = walkerSpawnPlans;
		_routeService = routeService;
		_movementStates = movementStates;
		_movementBroadcasts = movementBroadcasts;
		_threadPoolManager = threadPoolManager;
		_npcAiStates = npcAiStates;
		_creaturePvpZoneCounterService = creaturePvpZoneCounterService;
	}

	public int ActiveStateCount => _activeStates.Count;

	public int ActiveFormationStateCount => _formationStates.Count;

	public int PendingRestTaskCount => _pendingRestTasks.Count;

	public int PendingArrivalTaskCount => _pendingArrivalTasks.Count;

	public int PendingMovementTickTaskCount => _pendingMovementTickTasks.Count;

	public bool TryGetActiveState(int objectId, out WorldNpcWalkerMovementState? state)
	{
		return _activeStates.TryGetValue(objectId, out state);
	}

	public WorldNpcWalkerInstanceDestroyCleanupResult OnInstanceDestroy(int worldId, int instanceId)
	{
		// Java parity: WalkerFormator.onInstanceDestroy(worldId, instanceId) clears the instance-scoped
		// walker formation cache after InstanceService.destroyInstance notifies the instance handler.
		var removedObjectIds = _activeStates.Keys
			.Where(objectId => IsObjectInInstance(objectId, worldId, instanceId))
			.ToArray();
		var removedFormationKeys = new HashSet<WorldNpcWalkerFormationKey>();
		foreach (var objectId in removedObjectIds)
		{
			_activeStates.TryRemove(objectId, out _);
			CancelPendingRestTask(objectId);
			CancelPendingArrivalTask(objectId);
			CancelPendingMovementTickTask(objectId);
			_npcAiStates?.StopWalking(objectId);
			if (_formationKeysByObjectId.TryRemove(objectId, out var formationKey))
				removedFormationKeys.Add(formationKey);
		}

		var removedFormationStateCount = 0;
		foreach (var formationKey in removedFormationKeys)
		{
			if (_formationStates.TryRemove(formationKey, out _))
				removedFormationStateCount++;
		}

		return new WorldNpcWalkerInstanceDestroyCleanupResult(
			worldId,
			instanceId,
			removedObjectIds,
			removedFormationStateCount,
			"WalkerFormator.onInstanceDestroy(worldId, instanceId) -> WalkerFormationsCache.onInstanceDestroy");
	}

	public async Task<WorldNpcWalkerRouteWalkingWorldStartResult> StartWorldRouteWalkingAsync(
		int worldId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ai/manager/WalkManager.startWalking delegates spawned path-walker owners into startRouteWalking.
		cancellationToken.ThrowIfCancellationRequested();
		var staticData = _runtimeContext.DataManager?.StaticData;
		if (staticData == null)
			return WorldNpcWalkerRouteWalkingWorldStartResult.NotStarted(WorldNpcWalkerRouteWalkingWorldStartStatus.MissingStaticData);

		var worldPlan = _walkerSpawnPlans.GetWorldPlan(worldId);
		if (worldPlan == null)
			return WorldNpcWalkerRouteWalkingWorldStartResult.NotStarted(WorldNpcWalkerRouteWalkingWorldStartStatus.MissingWorldPlan);

		var results = new List<WorldNpcWalkerRouteWalkingStartResult>();
		foreach (var walker in worldPlan.SpawnPlan.Walkers)
		{
			cancellationToken.ThrowIfCancellationRequested();
			results.Add(await StartRouteWalkingAsync(walker.ObjectId, cancellationToken));
		}

		foreach (var formation in worldPlan.SpawnPlan.Formations)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var firstMember = formation.Members.FirstOrDefault();
			if (firstMember != null)
				results.Add(await StartRouteWalkingAsync(firstMember.ObjectId, cancellationToken));
		}

		return WorldNpcWalkerRouteWalkingWorldStartResult.FromResults(results);
	}

	public async Task<WorldNpcWalkerRouteWalkingStartResult> StartRouteWalkingAsync(
		int objectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ai/manager/WalkManager.startRouteWalking starts only spawned path walkers and initializes their route target.
		cancellationToken.ThrowIfCancellationRequested();
		if (_activeStates.ContainsKey(objectId))
			return WorldNpcWalkerRouteWalkingStartResult.NotStarted(WorldNpcWalkerRouteWalkingStartStatus.AlreadyWalking);
		if (_npcAiStates?.TryGetState(objectId, out var aiState) == true
			&& aiState?.State == WorldNpcAiState.Walking)
		{
			return WorldNpcWalkerRouteWalkingStartResult.NotStarted(WorldNpcWalkerRouteWalkingStartStatus.AlreadyWalking);
		}

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

		CancelPendingArrivalTask(objectId);
		CancelPendingMovementTickTask(objectId);
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
		RevalidateNpcCreaturePvpZones(objectId);
		if (currentState.IsFormationMember)
			return await TargetReachedFormationMemberAsync(objectId, currentState, routePlan, worldPlan, cancellationToken);

		var walker = worldPlan.SpawnPlan.Walkers.FirstOrDefault(walker => walker.ObjectId == objectId);
		if (walker == null)
			return WorldNpcWalkerRouteWalkingTargetReachedResult.NotHandled(WorldNpcWalkerRouteWalkingTargetReachedStatus.NotActiveWalker);

		var advance = _movementStates.AdvanceSingleRouteWalking(currentState, walker, routePlan);
		if (advance.IsStopped)
		{
			CancelPendingRestTask(objectId);
			CancelPendingArrivalTask(objectId);
			CancelPendingMovementTickTask(objectId);
			_activeStates.TryRemove(objectId, out _);
			_npcAiStates?.StopWalking(objectId);
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
			ScheduleTargetArrival(objectId, nextState, cancellationToken);
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
			_npcAiStates?.WaitForFormationGroup(objectId);
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
			{
				_activeStates[memberState.ObjectId] = memberState;
				_npcAiStates?.ResumeRouteWalking(memberState.ObjectId);
			}
		}

		if (advance.RestDelay <= TimeSpan.Zero)
		{
			CancelPendingRestTasks(advance.State.MemberStates);
			var sentCount = await BroadcastStatesAsync(advance.State.MemberStates, cancellationToken);
			ScheduleTargetArrivals(advance.State.MemberStates, cancellationToken);
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
		_npcAiStates?.StartRouteWalking(walker.ObjectId);
		var broadcast = await _movementBroadcasts.BroadcastWalkerMovementAsync(walker.ObjectId, state, cancellationToken: cancellationToken);
		ScheduleTargetArrival(walker.ObjectId, state, cancellationToken);
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
			_npcAiStates?.StartRouteWalking(state.ObjectId);
			var broadcast = await _movementBroadcasts.BroadcastWalkerMovementAsync(state.ObjectId, state, cancellationToken: cancellationToken);
			ScheduleTargetArrival(state.ObjectId, state, cancellationToken);
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
					ScheduleTargetArrival(objectId, state, cancellationToken);
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

	private void ScheduleTargetArrivals(
		IReadOnlyList<WorldNpcWalkerMovementState> states,
		CancellationToken cancellationToken)
	{
		foreach (var state in states)
			ScheduleTargetArrival(state.ObjectId, state, cancellationToken);
	}

	private void ScheduleTargetArrival(
		int objectId,
		WorldNpcWalkerMovementState state,
		CancellationToken cancellationToken)
	{
		// Java parity: taskmanager/tasks/MoveTaskManager ticks every 200 ms and sends MOVE_ARRIVED once NpcAI.isDestinationReached sees WalkManager.isArrivedAtPoint.
		if (_threadPoolManager == null)
			return;

		var arrivalDelay = CalculateArrivalDelay(objectId, state);
		if (arrivalDelay == null)
			return;

		CancelPendingArrivalTask(objectId);
		CancelPendingMovementTickTask(objectId);
		ScheduledTask? scheduledTask = null;
		scheduledTask = _threadPoolManager.Schedule(
			async taskCancellationToken =>
			{
				if (scheduledTask != null)
					RemovePendingArrivalTask(objectId, scheduledTask);

				await TargetReachedAsync(objectId, taskCancellationToken);
			},
			arrivalDelay.Value,
			cancellationToken);
		_pendingArrivalTasks[objectId] = scheduledTask;
		ScheduleMovementTick(objectId, state, arrivalDelay.Value, cancellationToken);
		_ = scheduledTask.Completion.ContinueWith(
			_ => RemovePendingArrivalTask(objectId, scheduledTask),
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	private void ScheduleMovementTick(
		int objectId,
		WorldNpcWalkerMovementState state,
		TimeSpan arrivalDelay,
		CancellationToken cancellationToken)
	{
		// Java parity: taskmanager/tasks/MoveTaskManager updates moving NPC positions every 200 ms before checking destination arrival.
		if (_threadPoolManager == null || arrivalDelay <= MoveTaskUpdatePeriod)
			return;

		CancelPendingMovementTickTask(objectId);
		ScheduledTask? scheduledTask = null;
		scheduledTask = _threadPoolManager.Schedule(
			_ =>
			{
				if (scheduledTask != null)
					RemovePendingMovementTickTask(objectId, scheduledTask);

				if (TryAdvanceNpcPositionTowardsTarget(objectId, state))
				{
					var nextArrivalDelay = CalculateArrivalDelay(objectId, state);
					if (nextArrivalDelay is { } delay && delay > MoveTaskUpdatePeriod)
						ScheduleMovementTick(objectId, state, delay, cancellationToken);
				}

				return ValueTask.CompletedTask;
			},
			MoveTaskUpdatePeriod,
			cancellationToken);
		_pendingMovementTickTasks[objectId] = scheduledTask;
		_ = scheduledTask.Completion.ContinueWith(
			_ => RemovePendingMovementTickTask(objectId, scheduledTask),
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	private TimeSpan? CalculateArrivalDelay(
		int objectId,
		WorldNpcWalkerMovementState state)
	{
		// Java parity: controllers/movement/NpcMoveController.moveToLocation uses owner.getGameStats().getMovementSpeedFloat().
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return null;

		var speed = npc.Template.RunSpeed;
		if (speed <= 0)
			return null;

		var distance = CalculateDistance(npc.Position, state.Target);
		if (distance <= MoveOffset)
			return MoveTaskUpdatePeriod;

		var travelSeconds = Math.Max(0d, distance - MoveOffset) / speed;
		var delay = TimeSpan.FromMilliseconds(Math.Ceiling(travelSeconds * 1000d));
		return delay < MoveTaskUpdatePeriod ? MoveTaskUpdatePeriod : delay;
	}

	private void CancelPendingRestTask(int objectId)
	{
		if (_pendingRestTasks.TryRemove(objectId, out var scheduledTask))
			scheduledTask.Cancel();
	}

	private void CancelPendingArrivalTask(int objectId)
	{
		if (_pendingArrivalTasks.TryRemove(objectId, out var scheduledTask))
			scheduledTask.Cancel();
	}

	private void CancelPendingMovementTickTask(int objectId)
	{
		if (_pendingMovementTickTasks.TryRemove(objectId, out var scheduledTask))
			scheduledTask.Cancel();
	}

	private bool IsObjectInInstance(int objectId, int worldId, int instanceId)
	{
		return _world.TryGetObject(objectId, out var gameObject)
			&& gameObject is WorldNpc npc
			&& npc.Position.WorldId == worldId
			&& npc.Position.InstanceId == instanceId;
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

	private void RemovePendingArrivalTask(int objectId, ScheduledTask scheduledTask)
	{
		((ICollection<KeyValuePair<int, ScheduledTask>>)_pendingArrivalTasks).Remove(
			new KeyValuePair<int, ScheduledTask>(objectId, scheduledTask));
	}

	private void RemovePendingMovementTickTask(int objectId, ScheduledTask scheduledTask)
	{
		((ICollection<KeyValuePair<int, ScheduledTask>>)_pendingMovementTickTasks).Remove(
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

	private void RevalidateNpcCreaturePvpZones(int objectId)
	{
		// Java parity: MoveTaskManager destination reached -> ZoneUpdateService.add -> Creature.revalidateZones.
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return;

		var staticData = _runtimeContext.DataManager?.StaticData;
		CreaturePvpZoneRevalidationService.Revalidate(
			npc.ObjectId,
			npc.Position,
			staticData?.CreaturePvpZones,
			_creaturePvpZoneCounterService);
	}

	private bool TryAdvanceNpcPositionTowardsTarget(
		int objectId,
		WorldNpcWalkerMovementState state)
	{
		// Java parity: controllers/movement/NpcMoveController.moveToLocation advances by movementSpeed * elapsedMillis / 1000.
		if (!_activeStates.TryGetValue(objectId, out var activeState) || activeState != state)
			return false;
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return false;

		var speed = npc.Template.RunSpeed;
		if (speed <= 0)
			return false;

		var distance = CalculateDistance(npc.Position, state.Target);
		if (distance <= MoveOffset)
			return false;

		var stepDistance = speed * MoveTaskUpdatePeriod.TotalSeconds;
		var travelDistance = Math.Min(stepDistance, Math.Max(0d, distance - MoveOffset));
		if (travelDistance <= 0)
			return false;

		var fraction = travelDistance / distance;
		var updatedNpc = npc with
		{
			Position = npc.Position with
			{
				X = (float)((state.Target.X - npc.Position.X) * fraction + npc.Position.X),
				Y = (float)((state.Target.Y - npc.Position.Y) * fraction + npc.Position.Y),
				Z = (float)((state.Target.Z - npc.Position.Z) * fraction + npc.Position.Z),
			},
		};
		return _world.TryUpdateObject(objectId, updatedNpc);
	}

	private static double CalculateDistance(
		WorldPosition position,
		WorldNpcWalkerRouteStepTarget target)
	{
		var deltaX = position.X - target.X;
		var deltaY = position.Y - target.Y;
		var deltaZ = position.Z - target.Z;
		return Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
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
	AlreadyWalking,
	MissingStaticData,
	MissingNpc,
	MissingRoute,
	MissingWorldPlan,
	NotActiveWalker,
	MissingMovementTarget,
}

public sealed record WorldNpcWalkerRouteWalkingWorldStartResult(
	bool Started,
	WorldNpcWalkerRouteWalkingWorldStartStatus Status,
	int RouteStartCount,
	int StateCount,
	int BroadcastCount,
	IReadOnlyList<WorldNpcWalkerRouteWalkingStartResult> Results)
{
	public static WorldNpcWalkerRouteWalkingWorldStartResult FromResults(
		IReadOnlyList<WorldNpcWalkerRouteWalkingStartResult> results)
	{
		var startedResults = results.Where(result => result.Started).ToArray();
		return new WorldNpcWalkerRouteWalkingWorldStartResult(
			Started: startedResults.Length > 0,
			startedResults.Length > 0
				? WorldNpcWalkerRouteWalkingWorldStartStatus.Started
				: WorldNpcWalkerRouteWalkingWorldStartStatus.NoStartedRoutes,
			startedResults.Length,
			startedResults.Sum(result => result.States.Count),
			startedResults.Sum(result => result.BroadcastCount),
			results);
	}

	public static WorldNpcWalkerRouteWalkingWorldStartResult NotStarted(
		WorldNpcWalkerRouteWalkingWorldStartStatus status)
	{
		return new WorldNpcWalkerRouteWalkingWorldStartResult(
			Started: false,
			status,
			RouteStartCount: 0,
			StateCount: 0,
			BroadcastCount: 0,
			Results: Array.Empty<WorldNpcWalkerRouteWalkingStartResult>());
	}
}

public enum WorldNpcWalkerRouteWalkingWorldStartStatus
{
	Started,
	NoStartedRoutes,
	MissingStaticData,
	MissingWorldPlan,
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

public sealed record WorldNpcWalkerInstanceDestroyCleanupResult(
	int WorldId,
	int InstanceId,
	IReadOnlyList<int> RemovedObjectIds,
	int RemovedFormationStateCount,
	string JavaSource)
{
	public int RemovedActiveStateCount => RemovedObjectIds.Count;
}
