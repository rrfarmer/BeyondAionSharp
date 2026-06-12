using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using GameWorld = Aion.GameServer.World.World;
using SM_ATTACK_STATUS = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus;

namespace Aion.GameServer.Services;

public sealed class WorldNpcDamageService
{
	private readonly GameWorld _world;
	private readonly WorldNpcLifeStatsService _lifeStats;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly WorldNpcCombatStateService? _combatStates;
	private readonly WorldNpcCombatEventService? _combatEvents;
	private readonly WorldNpcCastingInterruptService? _castingInterrupts;

	public WorldNpcDamageService(
		GameWorld world,
		WorldNpcLifeStatsService lifeStats,
		IGameClientConnectionRegistry? connectionRegistry = null,
		WorldNpcCombatStateService? combatStates = null,
		WorldNpcCombatEventService? combatEvents = null,
		WorldNpcCastingInterruptService? castingInterrupts = null)
	{
		_world = world;
		_lifeStats = lifeStats;
		_connectionRegistry = connectionRegistry;
		_combatStates = combatStates;
		_combatEvents = combatEvents;
		_castingInterrupts = castingInterrupts;
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
		WorldNpcCastingInterruptResult? castingInterrupt = null;
		if (damage != 0 && damageOptions.NotifyAttack)
		{
			castingInterrupt = _castingInterrupts?.EvaluateIncomingDamage(
				spawnedNpc.ObjectId,
				attacker.ObjectId,
				damage,
				damageOptions.NotifyAttack,
				maxHp,
				damageOptions.CastingInterruptOptions);
		}
		WorldNpcAttackedObserverNotification? attackedObserverNotification = null;
		if (damage != 0 && damageOptions.NotifyAttack)
		{
			attackedObserverNotification = _combatEvents?.NotifyAttackedObservers(
				spawnedNpc.ObjectId,
				attacker.ObjectId,
				damageOptions.SkillId);
		}
		_combatStates?.AddDamage(
			spawnedNpc.ObjectId,
			attacker.ObjectId,
			damage,
			damageOptions.NotifyAttack,
			damageOptions.HopType);
		var supportAiRequests = _combatEvents?.NotifyNearbySupportAi(
			spawnedNpc,
			_world.GetNpcs(spawnedNpc.Position.WorldId)) ?? Array.Empty<WorldNpcSupportAiRequest>();
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
		var combatState = _combatStates?.IncrementAttackedCount(spawnedNpc.ObjectId);
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
			attackStatusBroadcastCount,
			combatState,
			attackedObserverNotification,
			supportAiRequests,
			castingInterrupt);
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
	SmAttackStatusLog AttackStatusLog = SmAttackStatusLog.Regular,
	WorldNpcDamageHopType HopType = WorldNpcDamageHopType.Damage,
	WorldNpcCastingInterruptOptions? CastingInterruptOptions = null)
{
	public static WorldNpcDamageOptions Default { get; } = new(NotifyAttack: true);
}

public sealed record WorldNpcDamageResult(
	WorldNpcDamageStatus Status,
	WorldNpcLifeStatsDamageResult? LifeStats,
	int Damage,
	bool NotifyAttack,
	SmAttackStatus? AttackStatusPacket = null,
	int AttackStatusBroadcastCount = 0,
	WorldNpcCombatRuntimeState? CombatState = null,
	WorldNpcAttackedObserverNotification? AttackedObserverNotification = null,
	IReadOnlyList<WorldNpcSupportAiRequest>? SupportAiRequests = null,
	WorldNpcCastingInterruptResult? CastingInterrupt = null)
{
	public static WorldNpcDamageResult Skipped(WorldNpcDamageStatus status)
	{
		return new WorldNpcDamageResult(
			status,
			null,
			0,
			NotifyAttack: false,
			AttackStatusPacket: null,
			AttackStatusBroadcastCount: 0,
			CombatState: null,
			AttackedObserverNotification: null,
			SupportAiRequests: Array.Empty<WorldNpcSupportAiRequest>(),
			CastingInterrupt: null);
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
