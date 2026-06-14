using System.Collections.Concurrent;
using Aion.GameServer.Configuration;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcRandomWalkService
{
	private static readonly TimeSpan MoveTaskUpdatePeriod = TimeSpan.FromMilliseconds(200);
	private const double MoveOffset = 0.05d;
	private readonly GameWorld _world;
	private readonly IGameClientConnectionRegistry _connectionRegistry;
	private readonly GameServerOptions _options;
	private readonly ThreadPoolManager? _threadPoolManager;
	private readonly WorldNpcAiStateService? _npcAiStates;
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly CreaturePvpZoneCounterService? _creaturePvpZoneCounterService;
	private readonly Func<float, float> _nextFloat;
	private readonly Func<int, int, int> _nextDelaySeconds;
	private readonly ConcurrentDictionary<int, WorldNpcRandomWalkRuntimeState> _activeStates = new();
	private readonly ConcurrentDictionary<int, ScheduledTask> _pendingTargetTasks = new();
	private readonly ConcurrentDictionary<int, ScheduledTask> _pendingArrivalTasks = new();
	private readonly ConcurrentDictionary<int, ScheduledTask> _pendingMovementTickTasks = new();

	public WorldNpcRandomWalkService(
		GameWorld world,
		IGameClientConnectionRegistry connectionRegistry,
		GameServerOptions options,
		ThreadPoolManager? threadPoolManager = null,
		WorldNpcAiStateService? npcAiStates = null,
		Func<float, float>? nextFloat = null,
		Func<int, int, int>? nextDelaySeconds = null,
		GameServerRuntimeContext? runtimeContext = null,
		CreaturePvpZoneCounterService? creaturePvpZoneCounterService = null)
	{
		_world = world;
		_connectionRegistry = connectionRegistry;
		_options = options;
		_threadPoolManager = threadPoolManager;
		_npcAiStates = npcAiStates;
		_runtimeContext = runtimeContext;
		_creaturePvpZoneCounterService = creaturePvpZoneCounterService;
		_nextFloat = nextFloat ?? (maxExclusive => (float)(Random.Shared.NextDouble() * maxExclusive));
		_nextDelaySeconds = nextDelaySeconds ?? ((minimum, maximum) => Random.Shared.Next(minimum, maximum + 1));
	}

	public int ActiveStateCount => _activeStates.Count;

	public int PendingTargetTaskCount => _pendingTargetTasks.Count;

	public int PendingArrivalTaskCount => _pendingArrivalTasks.Count;

	public int PendingMovementTickTaskCount => _pendingMovementTickTasks.Count;

	public bool TryGetActiveState(int objectId, out WorldNpcRandomWalkRuntimeState? state)
	{
		return _activeStates.TryGetValue(objectId, out state);
	}

	public async Task<WorldNpcRandomWalkWorldStartResult> StartWorldRandomWalkingAsync(
		int worldId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ai/manager/WalkManager.startWalking visits spawned NPCs and starts random walkers before path walkers.
		cancellationToken.ThrowIfCancellationRequested();
		var results = new List<WorldNpcRandomWalkStartResult>();
		foreach (var npc in _world.GetNpcs(worldId).OfType<WorldNpc>())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (npc.RandomWalkRange <= 0)
				continue;

			results.Add(await StartRandomWalkingAsync(npc.ObjectId, cancellationToken));
		}

		return WorldNpcRandomWalkWorldStartResult.FromResults(results);
	}

	public ValueTask<WorldNpcRandomWalkStartResult> StartRandomWalkingAsync(
		int objectId,
		CancellationToken cancellationToken = default)
	{
		// Java parity: ai/manager/WalkManager.startWalking tries startRandomWalking before startRouteWalking.
		cancellationToken.ThrowIfCancellationRequested();
		if (!_options.Ai.NpcMovementEnabled)
			return ValueTask.FromResult(WorldNpcRandomWalkStartResult.NotStarted(WorldNpcRandomWalkStartStatus.MovementDisabled));
		if (_threadPoolManager == null)
			return ValueTask.FromResult(WorldNpcRandomWalkStartResult.NotStarted(WorldNpcRandomWalkStartStatus.MissingScheduler));
		if (_activeStates.ContainsKey(objectId))
			return ValueTask.FromResult(WorldNpcRandomWalkStartResult.NotStarted(WorldNpcRandomWalkStartStatus.AlreadyWalking));
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return ValueTask.FromResult(WorldNpcRandomWalkStartResult.NotStarted(WorldNpcRandomWalkStartStatus.MissingNpc));
		if (npc.RandomWalkRange <= 0)
			return ValueTask.FromResult(WorldNpcRandomWalkStartResult.NotStarted(WorldNpcRandomWalkStartStatus.NotRandomWalker));

		var delay = GetNextRandomWalkDelay();
		var state = new WorldNpcRandomWalkRuntimeState(
			objectId,
			npc.SpawnLocation,
			npc.RandomWalkRange,
			delay,
			Target: null);
		if (!_activeStates.TryAdd(objectId, state))
			return ValueTask.FromResult(WorldNpcRandomWalkStartResult.NotStarted(WorldNpcRandomWalkStartStatus.AlreadyWalking));

		_npcAiStates?.StartRandomWalking(objectId);
		ScheduleRandomPointSelection(objectId, delay, cancellationToken);
		return ValueTask.FromResult(WorldNpcRandomWalkStartResult.Scheduled(delay));
	}

	public bool StopRandomWalking(int objectId)
	{
		// Java parity: ai/manager/WalkManager.stopWalking aborts movement and returns the NPC to IDLE.
		CancelPendingTargetTask(objectId);
		CancelPendingArrivalTask(objectId);
		CancelPendingMovementTickTask(objectId);
		_npcAiStates?.StopWalking(objectId);
		return _activeStates.TryRemove(objectId, out _);
	}

	public ValueTask<WorldNpcRandomWalkTargetReachedResult> TargetReachedAsync(
		int objectId,
		CancellationToken cancellationToken = default)
	{
		return TargetReachedCoreAsync(objectId, cancellationToken, cancellationToken);
	}

	private ValueTask<WorldNpcRandomWalkTargetReachedResult> TargetReachedCoreAsync(
		int objectId,
		CancellationToken cancellationToken,
		CancellationToken scheduleCancellationToken)
	{
		// Java parity: ai/manager/WalkManager.targetReached handles WALK_RANDOM by choosing the next random point.
		cancellationToken.ThrowIfCancellationRequested();
		CancelPendingArrivalTask(objectId);
		CancelPendingMovementTickTask(objectId);
		if (!_activeStates.TryGetValue(objectId, out var state) || state.Target == null)
			return ValueTask.FromResult(WorldNpcRandomWalkTargetReachedResult.NotHandled(WorldNpcRandomWalkTargetReachedStatus.MissingTarget));
		if (!TryUpdateNpcPositionToTarget(objectId, state.Target))
			return ValueTask.FromResult(WorldNpcRandomWalkTargetReachedResult.NotHandled(WorldNpcRandomWalkTargetReachedStatus.MissingNpc));
		RevalidateNpcCreaturePvpZones(objectId);
		if (!IsStillWalking(objectId))
		{
			_activeStates.TryRemove(objectId, out _);
			return ValueTask.FromResult(WorldNpcRandomWalkTargetReachedResult.NotHandled(WorldNpcRandomWalkTargetReachedStatus.NotWalking));
		}

		var delay = GetNextRandomWalkDelay();
		_activeStates[objectId] = state with
		{
			Delay = delay,
			Target = null,
		};
		ScheduleRandomPointSelection(objectId, delay, scheduleCancellationToken);
		return ValueTask.FromResult(WorldNpcRandomWalkTargetReachedResult.ScheduledNext(delay));
	}

	private void ScheduleRandomPointSelection(
		int objectId,
		TimeSpan delay,
		CancellationToken cancellationToken)
	{
		// Java parity: ai/manager/WalkManager.chooseNextRandomPoint schedules with Rnd.get(minimum, maximum) seconds.
		ScheduledTask? scheduledTask = null;
		scheduledTask = _threadPoolManager!.Schedule(
			async taskCancellationToken =>
			{
				if (scheduledTask != null)
					RemovePendingTargetTask(objectId, scheduledTask);

				await ChooseNextRandomPointAsync(objectId, taskCancellationToken, cancellationToken);
			},
			delay,
			cancellationToken);
		_pendingTargetTasks[objectId] = scheduledTask;
		_ = scheduledTask.Completion.ContinueWith(
			_ => RemovePendingTargetTask(objectId, scheduledTask),
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	private async Task ChooseNextRandomPointAsync(
		int objectId,
		CancellationToken cancellationToken,
		CancellationToken scheduleCancellationToken)
	{
		// Java parity: ai/manager/WalkManager.chooseNextRandomPoint chooses x/y inside spawn random_walk range and moveToPoint(..., owner.getZ()).
		cancellationToken.ThrowIfCancellationRequested();
		if (!_activeStates.TryGetValue(objectId, out var state))
			return;
		if (!IsStillWalking(objectId))
		{
			_activeStates.TryRemove(objectId, out _);
			return;
		}
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
		{
			_activeStates.TryRemove(objectId, out _);
			_npcAiStates?.Clear(objectId);
			return;
		}

		var diameter = state.RandomWalkRange * 2;
		var target = new WorldNpcRandomWalkTarget(
			_nextFloat(diameter) - state.RandomWalkRange + state.SpawnLocation.X,
			_nextFloat(diameter) - state.RandomWalkRange + state.SpawnLocation.Y,
			npc.Position.Z);
		var nextState = state with { Target = target };
		_activeStates[objectId] = nextState;

		var packet = new SM_MOVE(
			npc.ObjectId,
			npc.Position.X,
			npc.Position.Y,
			npc.Position.Z,
			npc.Position.Heading,
			MovementMask.NPC_STARTMOVE,
			target.X,
			target.Y,
			target.Z);
		var sentCount = await _connectionRegistry.BroadcastToVisiblePlayersAsync(npc.Position, npc.ObjectId, packet);
		var broadcastState = nextState with { BroadcastCount = sentCount };
		_activeStates[objectId] = broadcastState;
		ScheduleTargetArrival(objectId, broadcastState, scheduleCancellationToken);
	}

	private void ScheduleTargetArrival(
		int objectId,
		WorldNpcRandomWalkRuntimeState state,
		CancellationToken cancellationToken)
	{
		// Java parity: taskmanager/tasks/MoveTaskManager emits MOVE_ARRIVED once WALK_RANDOM reaches its moveToPoint target.
		if (_threadPoolManager == null || state.Target == null)
			return;

		var arrivalDelay = CalculateArrivalDelay(objectId, state.Target);
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

				await TargetReachedCoreAsync(objectId, taskCancellationToken, cancellationToken);
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
		WorldNpcRandomWalkRuntimeState state,
		TimeSpan arrivalDelay,
		CancellationToken cancellationToken)
	{
		// Java parity: controllers/movement/NpcMoveController.moveToLocation advances by speed on each 200 ms MoveTaskManager tick.
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
					var nextArrivalDelay = state.Target == null ? null : CalculateArrivalDelay(objectId, state.Target);
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

	private bool IsStillWalking(int objectId)
	{
		if (_npcAiStates == null || !_npcAiStates.TryGetState(objectId, out var state) || state == null)
			return true;

		return state.State == WorldNpcAiState.Walking;
	}

	private TimeSpan GetNextRandomWalkDelay()
	{
		var minimum = Math.Max(0, _options.Ai.NpcMovementMinimumDelaySeconds);
		var maximum = Math.Max(minimum, _options.Ai.NpcMovementMaximumDelaySeconds);
		return TimeSpan.FromSeconds(_nextDelaySeconds(minimum, maximum));
	}

	private TimeSpan? CalculateArrivalDelay(
		int objectId,
		WorldNpcRandomWalkTarget target)
	{
		// Java parity: controllers/movement/NpcMoveController.moveToLocation uses owner.getGameStats().getMovementSpeedFloat().
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return null;

		var speed = npc.Template.RunSpeed;
		if (speed <= 0)
			return null;

		var distance = CalculateDistance(npc.Position, target);
		if (distance <= MoveOffset)
			return MoveTaskUpdatePeriod;

		var travelSeconds = Math.Max(0d, distance - MoveOffset) / speed;
		var delay = TimeSpan.FromMilliseconds(Math.Ceiling(travelSeconds * 1000d));
		return delay < MoveTaskUpdatePeriod ? MoveTaskUpdatePeriod : delay;
	}

	private void CancelPendingTargetTask(int objectId)
	{
		if (_pendingTargetTasks.TryRemove(objectId, out var scheduledTask))
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

	private void RemovePendingTargetTask(int objectId, ScheduledTask scheduledTask)
	{
		((ICollection<KeyValuePair<int, ScheduledTask>>)_pendingTargetTasks).Remove(
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

	private bool TryUpdateNpcPositionToTarget(
		int objectId,
		WorldNpcRandomWalkTarget target)
	{
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return false;

		var updatedNpc = npc with
		{
			Position = new WorldPosition(
				npc.Position.WorldId,
				target.X,
				target.Y,
				target.Z,
				npc.Position.Heading,
				npc.Position.GetMapRegion()),
		};
		return _world.TryUpdateObject(objectId, updatedNpc);
	}

	private void RevalidateNpcCreaturePvpZones(int objectId)
	{
		// Java parity: MoveTaskManager destination reached -> ZoneUpdateService.add -> Creature.revalidateZones.
		if (!_world.TryGetObject(objectId, out var gameObject) || gameObject is not WorldNpc npc)
			return;

		var staticData = _runtimeContext?.DataManager?.StaticData;
		CreaturePvpZoneRevalidationService.Revalidate(
			npc.ObjectId,
			npc.Position,
			staticData?.CreaturePvpZones,
			_creaturePvpZoneCounterService);
	}

	private bool TryAdvanceNpcPositionTowardsTarget(
		int objectId,
		WorldNpcRandomWalkRuntimeState state)
	{
		if (state.Target == null)
			return false;
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
			Position = new WorldPosition(
				npc.Position.WorldId,
				(float)((state.Target.X - npc.Position.X) * fraction + npc.Position.X),
				(float)((state.Target.Y - npc.Position.Y) * fraction + npc.Position.Y),
				(float)((state.Target.Z - npc.Position.Z) * fraction + npc.Position.Z),
				npc.Position.Heading,
				npc.Position.GetMapRegion()),
		};
		return _world.TryUpdateObject(objectId, updatedNpc);
	}

	private static double CalculateDistance(
		WorldPosition position,
		WorldNpcRandomWalkTarget target)
	{
		var deltaX = position.X - target.X;
		var deltaY = position.Y - target.Y;
		var deltaZ = position.Z - target.Z;
		return Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
	}
}

public sealed record WorldNpcRandomWalkRuntimeState(
	int ObjectId,
	WorldPosition SpawnLocation,
	int RandomWalkRange,
	TimeSpan Delay,
	WorldNpcRandomWalkTarget? Target,
	int BroadcastCount = 0);

public sealed record WorldNpcRandomWalkTarget(
	float X,
	float Y,
	float Z);

public sealed record WorldNpcRandomWalkStartResult(
	bool Started,
	WorldNpcRandomWalkStartStatus Status,
	TimeSpan Delay)
{
	public static WorldNpcRandomWalkStartResult Scheduled(TimeSpan delay)
	{
		return new WorldNpcRandomWalkStartResult(
			Started: true,
			WorldNpcRandomWalkStartStatus.Scheduled,
			delay);
	}

	public static WorldNpcRandomWalkStartResult NotStarted(WorldNpcRandomWalkStartStatus status)
	{
		return new WorldNpcRandomWalkStartResult(
			Started: false,
			status,
			Delay: TimeSpan.Zero);
	}
}

public enum WorldNpcRandomWalkStartStatus
{
	Scheduled,
	MovementDisabled,
	MissingScheduler,
	AlreadyWalking,
	MissingNpc,
	NotRandomWalker,
}

public sealed record WorldNpcRandomWalkTargetReachedResult(
	bool Handled,
	WorldNpcRandomWalkTargetReachedStatus Status,
	TimeSpan NextDelay)
{
	public static WorldNpcRandomWalkTargetReachedResult ScheduledNext(TimeSpan nextDelay)
	{
		return new WorldNpcRandomWalkTargetReachedResult(
			Handled: true,
			WorldNpcRandomWalkTargetReachedStatus.ScheduledNext,
			nextDelay);
	}

	public static WorldNpcRandomWalkTargetReachedResult NotHandled(WorldNpcRandomWalkTargetReachedStatus status)
	{
		return new WorldNpcRandomWalkTargetReachedResult(
			Handled: false,
			status,
			NextDelay: TimeSpan.Zero);
	}
}

public enum WorldNpcRandomWalkTargetReachedStatus
{
	ScheduledNext,
	MissingTarget,
	MissingNpc,
	NotWalking,
}

public sealed record WorldNpcRandomWalkWorldStartResult(
	bool Started,
	WorldNpcRandomWalkWorldStartStatus Status,
	int RandomWalkStartCount,
	IReadOnlyList<WorldNpcRandomWalkStartResult> Results)
{
	public static WorldNpcRandomWalkWorldStartResult FromResults(
		IReadOnlyList<WorldNpcRandomWalkStartResult> results)
	{
		var startCount = results.Count(result => result.Started);
		return new WorldNpcRandomWalkWorldStartResult(
			Started: startCount > 0,
			startCount > 0
				? WorldNpcRandomWalkWorldStartStatus.Started
				: WorldNpcRandomWalkWorldStartStatus.NoStartedRandomWalks,
			startCount,
			results);
	}
}

public enum WorldNpcRandomWalkWorldStartStatus
{
	Started,
	NoStartedRandomWalks,
}
