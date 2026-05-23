using Aion.GameServer.Model.GameObjects;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcDamageService
{
	private readonly GameWorld _world;
	private readonly WorldNpcLifeStatsService _lifeStats;

	public WorldNpcDamageService(GameWorld world, WorldNpcLifeStatsService lifeStats)
	{
		_world = world;
		_lifeStats = lifeStats;
	}

	public async ValueTask<WorldNpcDamageResult> ApplyDamageAsync(
		IWorldNpcObject? npc,
		Player? attacker,
		int damage,
		WorldNpcDamageOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: controllers/CreatureController.onAttack guards unspawned owners before calling CreatureLifeStats.reduceHp.
		if (npc == null)
			return WorldNpcDamageResult.Skipped(WorldNpcDamageStatus.MissingNpc);
		if (!_world.TryGetObject(npc.ObjectId, out var gameObject) || gameObject is not IWorldNpcObject spawnedNpc)
			return WorldNpcDamageResult.Skipped(WorldNpcDamageStatus.NotSpawned);
		if (attacker == null)
			return WorldNpcDamageResult.Skipped(WorldNpcDamageStatus.MissingAttacker);
		if (!TryResolveMaxStats(spawnedNpc, out var maxHp, out var maxMp))
			return WorldNpcDamageResult.Skipped(WorldNpcDamageStatus.MissingLifeStats);

		var damageOptions = options ?? WorldNpcDamageOptions.Default;
		var lifeStatsResult = await _lifeStats.ReduceHpAsync(
			spawnedNpc,
			damage,
			maxHp,
			maxMp,
			attacker,
			damageOptions.GroupMembers,
			damageOptions.FreeForAllDelay,
			damageOptions.DecayDelay,
			damageOptions.DeathOptions,
			cancellationToken);
		return new WorldNpcDamageResult(
			MapStatus(lifeStatsResult.Status),
			lifeStatsResult,
			Math.Max(0, damage),
			damageOptions.NotifyAttack);
	}

	private bool TryResolveMaxStats(IWorldNpcObject npc, out int maxHp, out int maxMp)
	{
		if (_lifeStats.TryGetStats(npc.ObjectId, out var stats))
		{
			maxHp = stats!.MaxHp;
			maxMp = stats.MaxMp;
			return maxHp > 0;
		}

		maxHp = npc.Template.MaxHp;
		maxMp = 0;
		return maxHp > 0;
	}

	private static WorldNpcDamageStatus MapStatus(WorldNpcLifeStatsDamageStatus status)
	{
		return status switch
		{
			WorldNpcLifeStatsDamageStatus.NoChange => WorldNpcDamageStatus.NoDamage,
			WorldNpcLifeStatsDamageStatus.Reduced => WorldNpcDamageStatus.Damaged,
			WorldNpcLifeStatsDamageStatus.Died => WorldNpcDamageStatus.Died,
			WorldNpcLifeStatsDamageStatus.AlreadyDead => WorldNpcDamageStatus.AlreadyDead,
			WorldNpcLifeStatsDamageStatus.MissingNpc => WorldNpcDamageStatus.MissingNpc,
			_ => WorldNpcDamageStatus.MissingLifeStats,
		};
	}
}

public sealed record WorldNpcDamageOptions(
	bool NotifyAttack,
	IReadOnlyList<Player>? GroupMembers = null,
	TimeSpan? FreeForAllDelay = null,
	TimeSpan? DecayDelay = null,
	WorldNpcDeathDropOptions? DeathOptions = null)
{
	public static WorldNpcDamageOptions Default { get; } = new(NotifyAttack: true);
}

public sealed record WorldNpcDamageResult(
	WorldNpcDamageStatus Status,
	WorldNpcLifeStatsDamageResult? LifeStats,
	int Damage,
	bool NotifyAttack)
{
	public static WorldNpcDamageResult Skipped(WorldNpcDamageStatus status)
	{
		return new WorldNpcDamageResult(status, null, 0, NotifyAttack: false);
	}
}

public enum WorldNpcDamageStatus
{
	MissingNpc,
	NotSpawned,
	MissingAttacker,
	MissingLifeStats,
	NoDamage,
	Damaged,
	Died,
	AlreadyDead,
}
