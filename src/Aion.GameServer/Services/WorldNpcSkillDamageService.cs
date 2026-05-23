using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class WorldNpcSkillDamageService
{
	private readonly WorldNpcDamageService _damageService;

	public WorldNpcSkillDamageService(WorldNpcDamageService damageService)
	{
		_damageService = damageService;
	}

	public async ValueTask<WorldNpcSkillDamageResult> ApplyDamageEffectAsync(
		WorldNpcSkillDamageRequest request,
		CancellationToken cancellationToken = default)
	{
		// Java parity: skillengine/effect/DamageEffect.applyEffect delegates to effected.controller.onAttack.
		var mapping = GetMapping(request.Kind);
		var options = request.Options ?? WorldNpcSkillDamageOptions.Default;
		var damageResult = await _damageService.ApplyDamageAsync(
			request.Target,
			request.Effector,
			request.Damage,
			new WorldNpcDamageOptions(
				NotifyAttack: mapping.NotifyAttack,
				GroupMembers: options.GroupMembers,
				FreeForAllDelay: options.FreeForAllDelay,
				DecayDelay: options.DecayDelay,
				DeathOptions: options.DeathOptions,
				AttackStatusType: mapping.AttackStatusType,
				SkillId: request.SkillId,
				AttackStatusLog: mapping.AttackStatusLog,
				HopType: options.HopType,
				CastingInterruptOptions: options.CastingInterruptOptions),
			cancellationToken);
		var shouldNotifyAttackObserver = mapping.NotifyEffectorAttackObservers
			&& request.Effector != null
			&& request.Target != null
			&& damageResult.Status is WorldNpcDamageStatus.NoDamage or WorldNpcDamageStatus.Damaged or WorldNpcDamageStatus.Died or WorldNpcDamageStatus.AlreadyDead;
		var attackObserverNotification = shouldNotifyAttackObserver
			? new WorldNpcSkillAttackObserverNotification(
				request.Effector!.ObjectId,
				request.Target!.ObjectId,
				request.SkillId)
			: null;
		return new WorldNpcSkillDamageResult(request.Kind, damageResult, attackObserverNotification);
	}

	private static WorldNpcSkillDamageMapping GetMapping(WorldNpcSkillDamageKind kind)
	{
		return kind switch
		{
			WorldNpcSkillDamageKind.ProvokedDamageEffect => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.ProcAttackInstant,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: false),
			_ => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Regular,
				SmAttackStatusLog.Regular,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: true),
		};
	}

	private sealed record WorldNpcSkillDamageMapping(
		SmAttackStatusType AttackStatusType,
		SmAttackStatusLog AttackStatusLog,
		bool NotifyAttack,
		bool NotifyEffectorAttackObservers);
}

public sealed record WorldNpcSkillDamageRequest(
	IWorldNpcObject? Target,
	Player? Effector,
	int Damage,
	int SkillId,
	WorldNpcSkillDamageKind Kind = WorldNpcSkillDamageKind.RegularDamageEffect,
	WorldNpcSkillDamageOptions? Options = null);

public sealed record WorldNpcSkillDamageOptions(
	IReadOnlyList<Player>? GroupMembers = null,
	TimeSpan? FreeForAllDelay = null,
	TimeSpan? DecayDelay = null,
	WorldNpcDeathDropOptions? DeathOptions = null,
	WorldNpcDamageHopType HopType = WorldNpcDamageHopType.Damage,
	WorldNpcCastingInterruptOptions? CastingInterruptOptions = null)
{
	public static WorldNpcSkillDamageOptions Default { get; } = new();
}

public sealed record WorldNpcSkillDamageResult(
	WorldNpcSkillDamageKind Kind,
	WorldNpcDamageResult DamageResult,
	WorldNpcSkillAttackObserverNotification? AttackObserverNotification);

public sealed record WorldNpcSkillAttackObserverNotification(
	int EffectorObjectId,
	int TargetObjectId,
	int SkillId);

public enum WorldNpcSkillDamageKind
{
	RegularDamageEffect,
	ProvokedDamageEffect,
}
