using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcLifeStatsService
{
	private readonly WorldNpcDeathDropWorkflowService _deathWorkflow;
	private readonly ConcurrentDictionary<int, WorldNpcLifeStats> _stats = new();
	private readonly object _sync = new();

	public WorldNpcLifeStatsService(WorldNpcDeathDropWorkflowService deathWorkflow)
	{
		_deathWorkflow = deathWorkflow;
	}

	public bool TryGetStats(int objectId, out WorldNpcLifeStats? stats)
	{
		return _stats.TryGetValue(objectId, out stats);
	}

	public void Clear(int objectId)
	{
		// Java parity: controllers/VisibleObjectController.delete/onDespawn removes the runtime NPC instance and its life stats.
		_stats.TryRemove(objectId, out _);
	}

	public WorldNpcLifeStats Initialize(IWorldNpcObject npc, int maxHp, int maxMp = 0)
	{
		// Java parity: model/stats/container/NpcLifeStats starts current HP/MP at the NPC's max game stats.
		var stats = WorldNpcLifeStats.FromMax(maxHp, maxMp);
		lock (_sync)
			_stats[npc.ObjectId] = stats;
		return stats;
	}

	public async ValueTask<WorldNpcLifeStatsDamageResult> ReduceHpAsync(
		IWorldNpcObject? npc,
		int damage,
		int maxHp,
		int maxMp = 0,
		Player? attacker = null,
		IReadOnlyList<Player>? groupMembers = null,
		TimeSpan? freeForAllDelay = null,
		TimeSpan? decayDelay = null,
		WorldNpcDeathDropOptions? deathOptions = null,
		CancellationToken cancellationToken = default,
		Func<WorldNpcLifeStats, WorldNpcLifeStats, CancellationToken, ValueTask>? beforeDeathAsync = null)
	{
		// Java parity: model/stats/container/CreatureLifeStats.reduceHp clamps to zero and calls owner.controller.onDie once when HP reaches zero.
		if (npc == null)
			return WorldNpcLifeStatsDamageResult.MissingNpc();

		var normalizedDamage = Math.Max(0, damage);
		WorldNpcLifeStats previous;
		WorldNpcLifeStats current;
		WorldNpcLifeStatsDamageStatus status;
		lock (_sync)
		{
			previous = GetOrCreateStats(npc.ObjectId, maxHp, maxMp);
			if (previous.IsDead)
				return new WorldNpcLifeStatsDamageResult(WorldNpcLifeStatsDamageStatus.AlreadyDead, previous, previous, null);

			if (normalizedDamage == 0)
				return new WorldNpcLifeStatsDamageResult(WorldNpcLifeStatsDamageStatus.NoChange, previous, previous, null);

			var nextHp = Math.Max(previous.CurrentHp - normalizedDamage, 0);
			current = previous with
			{
				CurrentHp = nextHp,
				CurrentMp = nextHp == 0 ? 0 : previous.CurrentMp,
			};
			_stats[npc.ObjectId] = current;
			status = current.IsDead ? WorldNpcLifeStatsDamageStatus.Died : WorldNpcLifeStatsDamageStatus.Reduced;
		}

		if (beforeDeathAsync != null)
			await beforeDeathAsync(previous, current, cancellationToken);

		var deathResult = status == WorldNpcLifeStatsDamageStatus.Died
			? await _deathWorkflow.HandleDeathAsync(
				npc,
				attacker,
				groupMembers,
				freeForAllDelay,
				decayDelay,
				deathOptions,
				cancellationToken)
			: null;
		return new WorldNpcLifeStatsDamageResult(status, previous, current, deathResult);
	}

	private WorldNpcLifeStats GetOrCreateStats(int objectId, int maxHp, int maxMp)
	{
		if (_stats.TryGetValue(objectId, out var stats))
			return stats;

		stats = WorldNpcLifeStats.FromMax(maxHp, maxMp);
		_stats[objectId] = stats;
		return stats;
	}
}

public sealed record WorldNpcLifeStats(
	int MaxHp,
	int MaxMp,
	int CurrentHp,
	int CurrentMp)
{
	public bool IsDead => CurrentHp == 0;

	public int GetHpPercentage()
	{
		// Java parity: model/stats/container/CreatureLifeStats.getHpPercentage returns 0 when dead and at least 1 while alive.
		if (CurrentHp == 0 || MaxHp <= 0)
			return 0;

		return Math.Max(1, (int)(100f * CurrentHp / MaxHp));
	}

	public int GetMpPercentage()
	{
		// Java parity: model/stats/container/CreatureLifeStats.getMpPercentage.
		return MaxMp <= 0 ? 0 : (int)(100f * CurrentMp / MaxMp);
	}

	public static WorldNpcLifeStats FromMax(int maxHp, int maxMp = 0)
	{
		var normalizedMaxHp = Math.Max(0, maxHp);
		var normalizedMaxMp = Math.Max(0, maxMp);
		return new WorldNpcLifeStats(normalizedMaxHp, normalizedMaxMp, normalizedMaxHp, normalizedMaxMp);
	}
}

public sealed record WorldNpcLifeStatsDamageResult(
	WorldNpcLifeStatsDamageStatus Status,
	WorldNpcLifeStats? Previous,
	WorldNpcLifeStats? Current,
	WorldNpcDeathDropWorkflowResult? DeathResult)
{
	public static WorldNpcLifeStatsDamageResult MissingNpc()
	{
		return new WorldNpcLifeStatsDamageResult(
			WorldNpcLifeStatsDamageStatus.MissingNpc,
			Previous: null,
			Current: null,
			DeathResult: null);
	}
}

public enum WorldNpcLifeStatsDamageStatus
{
	MissingNpc,
	NoChange,
	Reduced,
	Died,
	AlreadyDead,
}
