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
	private readonly GameWorld _world;
	private readonly IGameClientConnectionRegistry _connectionRegistry;
	private readonly GameServerOptions _options;
	private readonly ThreadPoolManager? _threadPoolManager;
	private readonly WorldNpcAiStateService? _npcAiStates;
	private readonly Func<float, float> _nextFloat;
	private readonly Func<int, int, int> _nextDelaySeconds;
	private readonly ConcurrentDictionary<int, WorldNpcRandomWalkRuntimeState> _activeStates = new();
	private readonly ConcurrentDictionary<int, ScheduledTask> _pendingTargetTasks = new();

	public WorldNpcRandomWalkService(
		GameWorld world,
		IGameClientConnectionRegistry connectionRegistry,
		GameServerOptions options,
		ThreadPoolManager? threadPoolManager = null,
		WorldNpcAiStateService? npcAiStates = null,
		Func<float, float>? nextFloat = null,
		Func<int, int, int>? nextDelaySeconds = null)
	{
		_world = world;
		_connectionRegistry = connectionRegistry;
		_options = options;
		_threadPoolManager = threadPoolManager;
		_npcAiStates = npcAiStates;
		_nextFloat = nextFloat ?? (maxExclusive => (float)(Random.Shared.NextDouble() * maxExclusive));
		_nextDelaySeconds = nextDelaySeconds ?? ((minimum, maximum) => Random.Shared.Next(minimum, maximum + 1));
	}

	public int ActiveStateCount => _activeStates.Count;

	public int PendingTargetTaskCount => _pendingTargetTasks.Count;

	public bool TryGetActiveState(int objectId, out WorldNpcRandomWalkRuntimeState? state)
	{
		return _activeStates.TryGetValue(objectId, out state);
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
		_npcAiStates?.StopWalking(objectId);
		return _activeStates.TryRemove(objectId, out _);
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

				await ChooseNextRandomPointAsync(objectId, taskCancellationToken);
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
		CancellationToken cancellationToken)
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

		var packet = new SmMove(npc, MovementMask.NpcStartMove, target.X, target.Y, target.Z);
		var sentCount = await _connectionRegistry.BroadcastToVisiblePlayersAsync(npc.Position, npc.ObjectId, packet);
		_activeStates[objectId] = nextState with { BroadcastCount = sentCount };
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

	private void CancelPendingTargetTask(int objectId)
	{
		if (_pendingTargetTasks.TryRemove(objectId, out var scheduledTask))
			scheduledTask.Cancel();
	}

	private void RemovePendingTargetTask(int objectId, ScheduledTask scheduledTask)
	{
		((ICollection<KeyValuePair<int, ScheduledTask>>)_pendingTargetTasks).Remove(
			new KeyValuePair<int, ScheduledTask>(objectId, scheduledTask));
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
