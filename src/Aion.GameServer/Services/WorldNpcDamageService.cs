using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class WorldNpcDamageService
{
	private readonly GameWorld _world;
	private readonly WorldNpcLifeStatsService _lifeStats;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;

	public WorldNpcDamageService(
		GameWorld world,
		WorldNpcLifeStatsService lifeStats,
		IGameClientConnectionRegistry? connectionRegistry = null)
	{
		_world = world;
		_lifeStats = lifeStats;
		_connectionRegistry = connectionRegistry;
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
		SmAttackStatus? attackStatusPacket = null;
		var attackStatusBroadcastCount = 0;
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
			cancellationToken,
			beforeDeathAsync: async (previous, current, _) =>
			{
				(attackStatusPacket, attackStatusBroadcastCount) = await CreateAndBroadcastAttackStatusAsync(
					spawnedNpc,
					previous,
					current,
					damageOptions);
			});
		if (attackStatusPacket == null && damageOptions.SkillId != 0 && lifeStatsResult.Previous != null && lifeStatsResult.Current != null)
		{
			(attackStatusPacket, attackStatusBroadcastCount) = await CreateAndBroadcastAttackStatusAsync(
				spawnedNpc,
				lifeStatsResult.Previous,
				lifeStatsResult.Current,
				damageOptions);
		}
		return new WorldNpcDamageResult(
			MapStatus(lifeStatsResult.Status),
			lifeStatsResult,
			Math.Max(0, damage),
			damageOptions.NotifyAttack,
			attackStatusPacket,
			attackStatusBroadcastCount);
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

	private async ValueTask<(SmAttackStatus? Packet, int BroadcastCount)> CreateAndBroadcastAttackStatusAsync(
		IWorldNpcObject npc,
		WorldNpcLifeStats previous,
		WorldNpcLifeStats current,
		WorldNpcDamageOptions options)
	{
		// Java parity: CreatureLifeStats.reduceHp sends SM_ATTACK_STATUS when HP changes or a skill id is present.
		var hpDelta = previous.CurrentHp - current.CurrentHp;
		if (hpDelta == 0 && options.SkillId == 0)
			return (null, 0);

		var packet = new SmAttackStatus(
			npc.ObjectId,
			options.AttackStatusType,
			options.SkillId,
			hpDelta,
			current.GetHpPercentage(),
			options.AttackStatusLog);
		if (_connectionRegistry == null)
			return (packet, 0);

		var broadcastCount = await _connectionRegistry.BroadcastToVisiblePlayersAsync(
			npc.Position,
			npc.ObjectId,
			packet,
			includeSourcePlayer: true);
		return (packet, broadcastCount);
	}
}

public sealed record WorldNpcDamageOptions(
	bool NotifyAttack,
	IReadOnlyList<Player>? GroupMembers = null,
	TimeSpan? FreeForAllDelay = null,
	TimeSpan? DecayDelay = null,
	WorldNpcDeathDropOptions? DeathOptions = null,
	SmAttackStatusType AttackStatusType = SmAttackStatusType.Regular,
	int SkillId = 0,
	SmAttackStatusLog AttackStatusLog = SmAttackStatusLog.Regular)
{
	public static WorldNpcDamageOptions Default { get; } = new(NotifyAttack: true);
}

public sealed record WorldNpcDamageResult(
	WorldNpcDamageStatus Status,
	WorldNpcLifeStatsDamageResult? LifeStats,
	int Damage,
	bool NotifyAttack,
	SmAttackStatus? AttackStatusPacket = null,
	int AttackStatusBroadcastCount = 0)
{
	public static WorldNpcDamageResult Skipped(WorldNpcDamageStatus status)
	{
		return new WorldNpcDamageResult(status, null, 0, NotifyAttack: false, AttackStatusPacket: null, AttackStatusBroadcastCount: 0);
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
