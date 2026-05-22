using System.Collections.Concurrent;

namespace Aion.GameServer.Services;

public sealed class WorldNpcAiStateService
{
	private readonly ConcurrentDictionary<int, WorldNpcAiRuntimeState> _states = new();

	public bool TryGetState(int objectId, out WorldNpcAiRuntimeState? state)
	{
		return _states.TryGetValue(objectId, out state);
	}

	public WorldNpcAiRuntimeState StartRouteWalking(int objectId)
	{
		// Java parity: ai/manager/WalkManager.startRouteWalking sets AIState.WALKING and AISubState.WALK_PATH.
		return SetState(objectId, WorldNpcAiState.Walking, WorldNpcAiSubState.WalkPath);
	}

	public WorldNpcAiRuntimeState StartRandomWalking(int objectId)
	{
		// Java parity: ai/manager/WalkManager.startRandomWalking sets AIState.WALKING and AISubState.WALK_RANDOM.
		return SetState(objectId, WorldNpcAiState.Walking, WorldNpcAiSubState.WalkRandom);
	}

	public WorldNpcAiRuntimeState WaitForFormationGroup(int objectId)
	{
		// Java parity: spawnengine/WalkerGroup.targetReached marks arrived members as AISubState.WALK_WAIT_GROUP.
		return SetSubState(objectId, WorldNpcAiSubState.WalkWaitGroup);
	}

	public WorldNpcAiRuntimeState ResumeRouteWalking(int objectId)
	{
		// Java parity: ai/manager/WalkManager.targetReached resumes WALK_WAIT_GROUP members with AISubState.WALK_PATH.
		return SetState(objectId, WorldNpcAiState.Walking, WorldNpcAiSubState.WalkPath);
	}

	public WorldNpcAiRuntimeState StopWalking(int objectId)
	{
		// Java parity: ai/manager/WalkManager.stopWalking sets AIState.IDLE and clears substate unless frozen.
		return _states.AddOrUpdate(
			objectId,
			_ => new WorldNpcAiRuntimeState(WorldNpcAiState.Idle, WorldNpcAiSubState.None),
			(_, current) => current.SubState == WorldNpcAiSubState.Freeze
				? current with { State = WorldNpcAiState.Idle }
				: new WorldNpcAiRuntimeState(WorldNpcAiState.Idle, WorldNpcAiSubState.None));
	}

	public void Clear(int objectId)
	{
		_states.TryRemove(objectId, out _);
	}

	private WorldNpcAiRuntimeState SetState(
		int objectId,
		WorldNpcAiState state,
		WorldNpcAiSubState subState)
	{
		return _states.AddOrUpdate(
			objectId,
			_ => new WorldNpcAiRuntimeState(state, subState),
			(_, _) => new WorldNpcAiRuntimeState(state, subState));
	}

	private WorldNpcAiRuntimeState SetSubState(
		int objectId,
		WorldNpcAiSubState subState)
	{
		return _states.AddOrUpdate(
			objectId,
			_ => new WorldNpcAiRuntimeState(WorldNpcAiState.Idle, subState),
			(_, current) => current with { SubState = subState });
	}
}

public sealed record WorldNpcAiRuntimeState(
	WorldNpcAiState State,
	WorldNpcAiSubState SubState);

public enum WorldNpcAiState
{
	Created,
	Died,
	Despawned,
	Idle,
	Walking,
	Following,
	Returning,
	Fight,
	Fear,
	Confuse,
	ForcedWalking,
}

public enum WorldNpcAiSubState
{
	None,
	Talk,
	Cast,
	WalkPath,
	WalkRandom,
	WalkWaitGroup,
	Freeze,
	TargetLost,
}
