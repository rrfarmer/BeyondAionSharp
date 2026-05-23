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
		var hasAttackResult = damageResult.Status is WorldNpcDamageStatus.NoDamage or WorldNpcDamageStatus.Damaged or WorldNpcDamageStatus.Died or WorldNpcDamageStatus.AlreadyDead;
		var shouldNotifyAttackObserver = mapping.NotifyEffectorAttackObservers
			&& request.Effector != null
			&& request.Target != null
			&& hasAttackResult;
		var attackObserverNotification = shouldNotifyAttackObserver
			? new WorldNpcSkillAttackObserverNotification(
				request.Effector!.ObjectId,
				request.Target!.ObjectId,
				request.SkillId)
			: null;
		var shouldNotifyDotAttackedObserver = mapping.NotifyDotAttackedObservers
			&& request.Effector != null
			&& request.Target != null
			&& hasAttackResult;
		var dotAttackedObserverNotification = shouldNotifyDotAttackedObserver
			? new WorldNpcSkillDotAttackedObserverNotification(
				request.Effector!.ObjectId,
				request.Target!.ObjectId,
				request.SkillId)
			: null;
		var drainResult = mapping.ApplyDrain
			? new WorldNpcSkillDrainResult(
				damageResult.Damage * Math.Max(0, options.HpDrainPercent) / 100,
				damageResult.Damage * Math.Max(0, options.MpDrainPercent) / 100)
			: null;
		var delayResult = mapping.ReportDelay
			? new WorldNpcSkillDelayResult(options.Delay ?? TimeSpan.Zero)
			: null;
		return new WorldNpcSkillDamageResult(
			request.Kind,
			damageResult,
			attackObserverNotification,
			dotAttackedObserverNotification,
			drainResult,
			delayResult);
	}

	private static WorldNpcSkillDamageMapping GetMapping(WorldNpcSkillDamageKind kind)
	{
		return kind switch
		{
			WorldNpcSkillDamageKind.DelayedSpellAttackInstant => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.DelayDamage,
				SmAttackStatusLog.DelayedSpellAttackInstant,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: true,
				NotifyDotAttackedObservers: false,
				ApplyDrain: false,
				ReportDelay: true),
			WorldNpcSkillDamageKind.ProcAttackInstant => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.ProcAttackInstant,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: false,
				NotifyDotAttackedObservers: false,
				ApplyDrain: false,
				ReportDelay: false),
			WorldNpcSkillDamageKind.BleedPeriodic => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.Bleed,
				NotifyAttack: false,
				NotifyEffectorAttackObservers: false,
				NotifyDotAttackedObservers: true,
				ApplyDrain: false,
				ReportDelay: false),
			WorldNpcSkillDamageKind.PeriodicSpellAttack => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.SpellAttack,
				NotifyAttack: false,
				NotifyEffectorAttackObservers: false,
				NotifyDotAttackedObservers: true,
				ApplyDrain: false,
				ReportDelay: false),
			WorldNpcSkillDamageKind.SpellAttackDrain => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.SpellAttackDrain,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: true,
				NotifyDotAttackedObservers: false,
				ApplyDrain: true,
				ReportDelay: false),
			WorldNpcSkillDamageKind.ProvokedDamageEffect => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.ProcAttackInstant,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: false,
				NotifyDotAttackedObservers: false,
				ApplyDrain: false,
				ReportDelay: false),
			_ => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Regular,
				SmAttackStatusLog.Regular,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: true,
				NotifyDotAttackedObservers: false,
				ApplyDrain: false,
				ReportDelay: false),
		};
	}

	private sealed record WorldNpcSkillDamageMapping(
		SmAttackStatusType AttackStatusType,
		SmAttackStatusLog AttackStatusLog,
		bool NotifyAttack,
		bool NotifyEffectorAttackObservers,
		bool NotifyDotAttackedObservers,
		bool ApplyDrain,
		bool ReportDelay);
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
	WorldNpcCastingInterruptOptions? CastingInterruptOptions = null,
	int HpDrainPercent = 0,
	int MpDrainPercent = 0,
	TimeSpan? Delay = null)
{
	public static WorldNpcSkillDamageOptions Default { get; } = new();
}

public sealed record WorldNpcSkillDamageResult(
	WorldNpcSkillDamageKind Kind,
	WorldNpcDamageResult DamageResult,
	WorldNpcSkillAttackObserverNotification? AttackObserverNotification,
	WorldNpcSkillDotAttackedObserverNotification? DotAttackedObserverNotification = null,
	WorldNpcSkillDrainResult? DrainResult = null,
	WorldNpcSkillDelayResult? DelayResult = null);

public sealed record WorldNpcSkillAttackObserverNotification(
	int EffectorObjectId,
	int TargetObjectId,
	int SkillId);

public sealed record WorldNpcSkillDotAttackedObserverNotification(
	int EffectorObjectId,
	int TargetObjectId,
	int SkillId);

public sealed record WorldNpcSkillDrainResult(
	int HpAmount,
	int MpAmount);

public sealed record WorldNpcSkillDelayResult(TimeSpan Delay);

public enum WorldNpcSkillDamageKind
{
	RegularDamageEffect,
	ProvokedDamageEffect,
	PeriodicSpellAttack,
	SpellAttackDrain,
	DelayedSpellAttackInstant,
	ProcAttackInstant,
	BleedPeriodic,
}
