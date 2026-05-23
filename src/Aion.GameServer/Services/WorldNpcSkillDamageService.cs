using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class WorldNpcSkillDamageService
{
	private readonly WorldNpcDamageService _damageService;
	private readonly WorldNpcSkillResultCalculationService _resultCalculation;

	public WorldNpcSkillDamageService(
		WorldNpcDamageService damageService,
		WorldNpcSkillResultCalculationService? resultCalculation = null)
	{
		_damageService = damageService;
		_resultCalculation = resultCalculation ?? new WorldNpcSkillResultCalculationService();
	}

	public async ValueTask<WorldNpcSkillDamageResult> ApplyDamageEffectAsync(
		WorldNpcSkillDamageRequest request,
		CancellationToken cancellationToken = default)
	{
		// Java parity: skillengine/effect/DamageEffect.applyEffect delegates to effected.controller.onAttack.
		var mapping = GetMapping(request.Kind);
		var options = request.Options ?? WorldNpcSkillDamageOptions.Default;
		var calculationResult = _resultCalculation.Calculate(new WorldNpcSkillResultCalculationRequest(
			request.Damage,
			mapping.ShouldApplyAttackerMovementModifier,
			mapping.IgnoreShield,
			mapping.SendResult,
			mapping.ShouldIncreaseByOneTimeBoost,
			options.UsesTemplateDamage,
			options.ResultCalculation));
		var damageResult = await _damageService.ApplyDamageAsync(
			request.Target,
			request.Effector,
			calculationResult.FinalDamage,
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
			delayResult,
			calculationResult);
	}

	public WorldNpcSkillOverTimeEffectStartResult StartOverTimeEffect(WorldNpcSkillOverTimeEffectStartRequest request)
	{
		// Java parity: skillengine/effect/{Bleed,Poison,SpellAttack}Effect.startEffect and AbstractOverTimeEffect.startEffect.
		var profile = GetOverTimeEffectProfile(request.Kind, request.SkillId);
		WorldNpcSkillMagicalOverTimeResult? calculationResult = null;
		WorldNpcSkillEffectReservedResult? reserved = null;
		var status = WorldNpcSkillOverTimeEffectCallerStatus.Applied;

		if (profile.ReserveDamageOnStart)
		{
			calculationResult = _resultCalculation.CalculateMagicalOverTime(new WorldNpcSkillMagicalOverTimeRequest(
				request.BaseValue,
				profile.UseMagicBoost,
				request.MagicalOverTime));
			if (calculationResult.Applied)
			{
				reserved = new WorldNpcSkillEffectReservedResult(
					request.Position,
					calculationResult.FinalDamage,
					WorldNpcEffectResourceType.Hp,
					IsDamage: true,
					Send: false);
			}
			else
			{
				status = WorldNpcSkillOverTimeEffectCallerStatus.CalculationUnresolved;
			}
		}

		var schedulesPeriodicTask = request.CheckTime > TimeSpan.Zero;
		var initialDelay = schedulesPeriodicTask
			? request.CheckTime + TimeSpan.FromMilliseconds(300)
			: (TimeSpan?)null;
		return new WorldNpcSkillOverTimeEffectStartResult(
			status,
			request.Kind,
			request.BaseValue,
			request.SkillId,
			request.Position,
			profile.UseMagicBoost,
			profile.AbnormalState,
			profile.AbnormalState != PlayerAbnormalState.None,
			profile.ReserveDamageOnStart,
			request.CheckTime,
			schedulesPeriodicTask,
			initialDelay,
			reserved,
			calculationResult);
	}

	public async ValueTask<WorldNpcSkillOverTimePeriodicActionResult> ApplyOverTimePeriodicActionAsync(
		WorldNpcSkillOverTimePeriodicActionRequest request,
		CancellationToken cancellationToken = default)
	{
		// Java parity: periodic onPeriodicAction methods for bleed, poison, spell attack, and spell attack drain.
		var profile = GetOverTimeEffectProfile(request.Kind, request.SkillId);
		WorldNpcSkillMagicalOverTimeResult? calculationResult = null;
		var damage = 0;
		var usedReservedDamage = false;
		var recalculatedDamage = false;

		if (profile.RecalculateDamageOnPeriodic)
		{
			if (request.BaseValue == null)
				return WorldNpcSkillOverTimePeriodicActionResult.Skipped(
					WorldNpcSkillOverTimeEffectCallerStatus.MissingBaseValue,
					request.Kind,
					profile.DamageKind);

			calculationResult = _resultCalculation.CalculateMagicalOverTime(new WorldNpcSkillMagicalOverTimeRequest(
				request.BaseValue.Value,
				profile.UseMagicBoost,
				request.MagicalOverTime));
			if (!calculationResult.Applied)
			{
				return WorldNpcSkillOverTimePeriodicActionResult.Skipped(
					WorldNpcSkillOverTimeEffectCallerStatus.CalculationUnresolved,
					request.Kind,
					profile.DamageKind,
					calculationResult);
			}

			damage = calculationResult.FinalDamage;
			recalculatedDamage = true;
		}
		else
		{
			if (request.ReservedDamage == null)
				return WorldNpcSkillOverTimePeriodicActionResult.Skipped(
					WorldNpcSkillOverTimeEffectCallerStatus.MissingReservedDamage,
					request.Kind,
					profile.DamageKind);

			damage = request.ReservedDamage.Value;
			usedReservedDamage = true;
		}

		var damageResult = await ApplyDamageEffectAsync(
			new WorldNpcSkillDamageRequest(
				request.Target,
				request.Effector,
				damage,
				request.SkillId,
				profile.DamageKind,
				request.DamageOptions),
			cancellationToken);
		return WorldNpcSkillOverTimePeriodicActionResult.AppliedResult(
			request.Kind,
			profile.DamageKind,
			damage,
			usedReservedDamage,
			recalculatedDamage,
			calculationResult,
			damageResult);
	}

	public WorldNpcSkillResourceOverTimeStartResult StartResourceOverTimeEffect(WorldNpcSkillResourceOverTimeStartRequest request)
	{
		// Java parity: skillengine/effect/HealOverTimeEffect.startEffect plus inherited AbstractOverTimeEffect.startEffect.
		var profile = GetResourceOverTimeEffectProfile(request.Kind);
		var reserved = profile.ReserveValueOnStart
			? new WorldNpcSkillEffectReservedResult(
				request.Position,
				request.Value,
				profile.ResourceType,
				IsDamage: false,
				Send: false)
			: null;
		var schedulesPeriodicTask = request.CheckTime > TimeSpan.Zero;
		var initialDelay = schedulesPeriodicTask
			? request.CheckTime + TimeSpan.FromMilliseconds(300)
			: (TimeSpan?)null;
		return new WorldNpcSkillResourceOverTimeStartResult(
			WorldNpcSkillResourceOverTimeStatus.Applied,
			request.Kind,
			request.Value,
			request.SkillId,
			request.Position,
			profile.ResourceType,
			profile.IsDamage,
			profile.ReserveValueOnStart,
			request.CheckTime,
			schedulesPeriodicTask,
			initialDelay,
			reserved);
	}

	public WorldNpcSkillResourceOverTimePeriodicActionResult ApplyResourceOverTimePeriodicAction(
		WorldNpcSkillResourceOverTimePeriodicActionRequest request)
	{
		// Java parity: skillengine/effect/{MpAttack,FpAttack,HealOverTime}Effect.onPeriodicAction.
		var profile = GetResourceOverTimeEffectProfile(request.Kind);
		if (profile.RequiresPlayerTarget && !request.TargetIsPlayer)
			return CreateSkippedResourceOverTimeResult(
				WorldNpcSkillResourceOverTimeStatus.TargetNotPlayer,
				request.Kind,
				profile);

		if (profile.IsDamage)
		{
			var value = request.Percent
				? request.MaxResource * request.Value / 100
				: request.Value;
			return CreateAppliedResourceOverTimeResult(
				kind: request.Kind,
				profile: profile,
				valueBeforeCap: value,
				finalValue: value,
				healSkillDeboostApplied: false,
				PercentApplied: request.Percent,
				CurrentResource: request.CurrentResource,
				MaxResource: request.MaxResource,
				TargetIsPlayer: request.TargetIsPlayer);
		}

		var possibleHealValue = request.Value;
		var healSkillDeboostApplied = false;
		if (request.Kind == WorldNpcSkillResourceOverTimeEffectKind.HpHeal &&
			!request.HasItemTemplate &&
			request.HealSkillDeboostedValue != null)
		{
			possibleHealValue = request.HealSkillDeboostedValue.Value;
			healSkillDeboostApplied = true;
		}

		var missingCapacity = Math.Max(0, request.MaxResource - request.CurrentResource);
		var valueAfterCap = Math.Min(missingCapacity, possibleHealValue);
		if (valueAfterCap <= 0)
		{
			return CreateSkippedResourceOverTimeResult(
				WorldNpcSkillResourceOverTimeStatus.NoResourceChange,
				request.Kind,
				profile,
				possibleHealValue,
				valueAfterCap,
				healSkillDeboostApplied,
				request.Percent,
				request.CurrentResource,
				request.MaxResource,
				request.TargetIsPlayer);
		}

		return CreateAppliedResourceOverTimeResult(
			kind: request.Kind,
			profile: profile,
			valueBeforeCap: possibleHealValue,
			finalValue: valueAfterCap,
			healSkillDeboostApplied: healSkillDeboostApplied,
			PercentApplied: request.Percent,
			CurrentResource: request.CurrentResource,
			MaxResource: request.MaxResource,
			TargetIsPlayer: request.TargetIsPlayer);
	}

	public WorldNpcSkillInstantResourceEffectResult CalculateInstantResourceEffect(WorldNpcSkillInstantResourceEffectRequest request)
	{
		// Java parity: skillengine/effect/{MpAttackInstant,FpAttackInstant,DelayedFpAtkInstant}Effect.
		var profile = GetInstantResourceEffectProfile(request.Kind);
		if (profile.RequiresPlayerTarget && !request.TargetIsPlayer)
			return CreateSkippedInstantResourceEffectResult(
				WorldNpcSkillInstantEffectStatus.TargetNotPlayer,
				request,
				profile);
		if (profile.RequiresEnemyTarget && !request.IsEnemy)
			return CreateSkippedInstantResourceEffectResult(
				WorldNpcSkillInstantEffectStatus.NotEnemy,
				request,
				profile);

		var value = request.Percent
			? request.MaxResource * request.Value / 100
			: request.Value;
		var reserved = profile.ReserveValueOnCalculate
			? new WorldNpcSkillEffectReservedResult(
				request.Position,
				value,
				profile.ResourceType,
				IsDamage: true,
				Send: true)
			: null;
		return new WorldNpcSkillInstantResourceEffectResult(
			WorldNpcSkillInstantEffectStatus.Applied,
			request.Kind,
			request.Value,
			value,
			request.SkillId,
			request.Position,
			profile.ResourceType,
			request.Percent,
			request.MaxResource,
			request.TargetIsPlayer,
			request.IsEnemy,
			profile.ReserveValueOnCalculate,
			profile.SchedulesDelayedAction,
			profile.SchedulesDelayedAction ? request.Delay ?? TimeSpan.Zero : null,
			reserved,
			profile.PacketType,
			profile.PacketLog);
	}

	public WorldNpcSkillInstantDrainEffectResult CalculateInstantDrainEffect(WorldNpcSkillInstantDrainEffectRequest request)
	{
		// Java parity: skillengine/effect/{SkillAtkDrainInstant,SpellAtkDrainInstant}Effect.
		var profile = GetInstantDrainEffectProfile(request.Kind);
		var hpAmount = request.ReservedDamage * Math.Max(0, request.HpPercent) / 100;
		var mpAmount = request.ReservedDamage * Math.Max(0, request.MpPercent) / 100;
		return new WorldNpcSkillInstantDrainEffectResult(
			Status: WorldNpcSkillInstantEffectStatus.Applied,
			Kind: request.Kind,
			ReservedDamage: request.ReservedDamage,
			HpPercent: request.HpPercent,
			MpPercent: request.MpPercent,
			HpAmount: hpAmount,
			MpAmount: mpAmount,
			Delay: TimeSpan.FromSeconds(1),
			HpPacketType: profile.HpPacketType,
			MpPacketType: profile.MpPacketType,
			PacketLog: profile.PacketLog);
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
				ReportDelay: true,
				IgnoreShield: true,
				SendResult: false,
				ShouldIncreaseByOneTimeBoost: true,
				ShouldApplyAttackerMovementModifier: true),
			WorldNpcSkillDamageKind.ProcAttackInstant => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.ProcAttackInstant,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: false,
				NotifyDotAttackedObservers: false,
				ApplyDrain: false,
				ReportDelay: false,
				IgnoreShield: false,
				SendResult: false,
				ShouldIncreaseByOneTimeBoost: false,
				ShouldApplyAttackerMovementModifier: false),
			WorldNpcSkillDamageKind.BleedPeriodic => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.Bleed,
				NotifyAttack: false,
				NotifyEffectorAttackObservers: false,
				NotifyDotAttackedObservers: true,
				ApplyDrain: false,
				ReportDelay: false,
				IgnoreShield: false,
				SendResult: true,
				ShouldIncreaseByOneTimeBoost: true,
				ShouldApplyAttackerMovementModifier: true),
			WorldNpcSkillDamageKind.PoisonPeriodic => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.Poison,
				NotifyAttack: false,
				NotifyEffectorAttackObservers: false,
				NotifyDotAttackedObservers: true,
				ApplyDrain: false,
				ReportDelay: false,
				IgnoreShield: false,
				SendResult: true,
				ShouldIncreaseByOneTimeBoost: true,
				ShouldApplyAttackerMovementModifier: true),
			WorldNpcSkillDamageKind.PeriodicSpellAttack => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.SpellAttack,
				NotifyAttack: false,
				NotifyEffectorAttackObservers: false,
				NotifyDotAttackedObservers: true,
				ApplyDrain: false,
				ReportDelay: false,
				IgnoreShield: false,
				SendResult: true,
				ShouldIncreaseByOneTimeBoost: true,
				ShouldApplyAttackerMovementModifier: true),
			WorldNpcSkillDamageKind.SpellAttackDrain => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.SpellAttackDrain,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: true,
				NotifyDotAttackedObservers: false,
				ApplyDrain: true,
				ReportDelay: false,
				IgnoreShield: false,
				SendResult: true,
				ShouldIncreaseByOneTimeBoost: true,
				ShouldApplyAttackerMovementModifier: true),
			WorldNpcSkillDamageKind.ProvokedDamageEffect => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Damage,
				SmAttackStatusLog.ProcAttackInstant,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: false,
				NotifyDotAttackedObservers: false,
				ApplyDrain: false,
				ReportDelay: false,
				IgnoreShield: false,
				SendResult: true,
				ShouldIncreaseByOneTimeBoost: true,
				ShouldApplyAttackerMovementModifier: true),
			_ => new WorldNpcSkillDamageMapping(
				SmAttackStatusType.Regular,
				SmAttackStatusLog.Regular,
				NotifyAttack: true,
				NotifyEffectorAttackObservers: true,
				NotifyDotAttackedObservers: false,
				ApplyDrain: false,
				ReportDelay: false,
				IgnoreShield: false,
				SendResult: true,
				ShouldIncreaseByOneTimeBoost: true,
				ShouldApplyAttackerMovementModifier: true),
		};
	}

	private sealed record WorldNpcSkillDamageMapping(
		SmAttackStatusType AttackStatusType,
		SmAttackStatusLog AttackStatusLog,
		bool NotifyAttack,
		bool NotifyEffectorAttackObservers,
		bool NotifyDotAttackedObservers,
		bool ApplyDrain,
		bool ReportDelay,
		bool IgnoreShield,
		bool SendResult,
		bool ShouldIncreaseByOneTimeBoost,
		bool ShouldApplyAttackerMovementModifier);

	private static WorldNpcSkillOverTimeEffectProfile GetOverTimeEffectProfile(
		WorldNpcSkillOverTimeEffectKind kind,
		int skillId)
	{
		return kind switch
		{
			WorldNpcSkillOverTimeEffectKind.Bleed => new WorldNpcSkillOverTimeEffectProfile(
				DamageKind: WorldNpcSkillDamageKind.BleedPeriodic,
				UseMagicBoost: false,
				AbnormalState: PlayerAbnormalState.Bleed,
				ReserveDamageOnStart: true,
				RecalculateDamageOnPeriodic: false),
			WorldNpcSkillOverTimeEffectKind.Poison => new WorldNpcSkillOverTimeEffectProfile(
				DamageKind: WorldNpcSkillDamageKind.PoisonPeriodic,
				UseMagicBoost: false,
				AbnormalState: PlayerAbnormalState.Poison,
				ReserveDamageOnStart: true,
				RecalculateDamageOnPeriodic: false),
			WorldNpcSkillOverTimeEffectKind.SpellAttack => new WorldNpcSkillOverTimeEffectProfile(
				DamageKind: WorldNpcSkillDamageKind.PeriodicSpellAttack,
				UseMagicBoost: skillId != 21110,
				AbnormalState: PlayerAbnormalState.None,
				ReserveDamageOnStart: true,
				RecalculateDamageOnPeriodic: false),
			WorldNpcSkillOverTimeEffectKind.SpellAttackDrain => new WorldNpcSkillOverTimeEffectProfile(
				DamageKind: WorldNpcSkillDamageKind.SpellAttackDrain,
				UseMagicBoost: true,
				AbnormalState: PlayerAbnormalState.None,
				ReserveDamageOnStart: false,
				RecalculateDamageOnPeriodic: true),
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unhandled Java over-time effect kind."),
		};
	}

	private sealed record WorldNpcSkillOverTimeEffectProfile(
		WorldNpcSkillDamageKind DamageKind,
		bool UseMagicBoost,
		PlayerAbnormalState AbnormalState,
		bool ReserveDamageOnStart,
		bool RecalculateDamageOnPeriodic);

	private static WorldNpcSkillResourceOverTimeEffectProfile GetResourceOverTimeEffectProfile(WorldNpcSkillResourceOverTimeEffectKind kind)
	{
		return kind switch
		{
			WorldNpcSkillResourceOverTimeEffectKind.MpAttack => new WorldNpcSkillResourceOverTimeEffectProfile(
				WorldNpcEffectResourceType.Mp,
				IsDamage: true,
				ReserveValueOnStart: false,
				RequiresPlayerTarget: false,
				PacketType: SmAttackStatusType.DamageMp,
				PacketLog: SmAttackStatusLog.MpAttack),
			WorldNpcSkillResourceOverTimeEffectKind.FpAttack => new WorldNpcSkillResourceOverTimeEffectProfile(
				WorldNpcEffectResourceType.Fp,
				IsDamage: true,
				ReserveValueOnStart: false,
				RequiresPlayerTarget: true,
				PacketType: SmAttackStatusType.FpDamage,
				PacketLog: SmAttackStatusLog.FpAttack),
			WorldNpcSkillResourceOverTimeEffectKind.HpHeal => new WorldNpcSkillResourceOverTimeEffectProfile(
				WorldNpcEffectResourceType.Hp,
				IsDamage: false,
				ReserveValueOnStart: true,
				RequiresPlayerTarget: false,
				PacketType: SmAttackStatusType.Hp,
				PacketLog: SmAttackStatusLog.Heal),
			WorldNpcSkillResourceOverTimeEffectKind.MpHeal => new WorldNpcSkillResourceOverTimeEffectProfile(
				WorldNpcEffectResourceType.Mp,
				IsDamage: false,
				ReserveValueOnStart: true,
				RequiresPlayerTarget: false,
				PacketType: SmAttackStatusType.Mp,
				PacketLog: SmAttackStatusLog.MpHeal),
			WorldNpcSkillResourceOverTimeEffectKind.FpHeal => new WorldNpcSkillResourceOverTimeEffectProfile(
				WorldNpcEffectResourceType.Fp,
				IsDamage: false,
				ReserveValueOnStart: true,
				RequiresPlayerTarget: true,
				PacketType: SmAttackStatusType.Fp,
				PacketLog: SmAttackStatusLog.FpHeal),
			WorldNpcSkillResourceOverTimeEffectKind.DpHeal => new WorldNpcSkillResourceOverTimeEffectProfile(
				WorldNpcEffectResourceType.Dp,
				IsDamage: false,
				ReserveValueOnStart: true,
				RequiresPlayerTarget: true,
				PacketType: null,
				PacketLog: null),
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unhandled Java resource over-time effect kind."),
		};
	}

	private static WorldNpcSkillResourceOverTimePeriodicActionResult CreateSkippedResourceOverTimeResult(
		WorldNpcSkillResourceOverTimeStatus status,
		WorldNpcSkillResourceOverTimeEffectKind kind,
		WorldNpcSkillResourceOverTimeEffectProfile profile,
		int valueBeforeCap = 0,
		int finalValue = 0,
		bool healSkillDeboostApplied = false,
		bool percentApplied = false,
		int currentResource = 0,
		int maxResource = 0,
		bool targetIsPlayer = false)
	{
		return new WorldNpcSkillResourceOverTimePeriodicActionResult(
			Status: status,
			Kind: kind,
			ResourceType: profile.ResourceType,
			IsDamage: profile.IsDamage,
			OriginalValue: 0,
			ValueBeforeCap: valueBeforeCap,
			FinalValue: finalValue,
			CurrentResource: currentResource,
			MaxResource: maxResource,
			PercentApplied: percentApplied,
			TargetIsPlayer: targetIsPlayer,
			HealSkillDeboostApplied: healSkillDeboostApplied,
			PacketType: profile.PacketType,
			PacketLog: profile.PacketLog);
	}

	private static WorldNpcSkillResourceOverTimePeriodicActionResult CreateAppliedResourceOverTimeResult(
		WorldNpcSkillResourceOverTimeEffectKind kind,
		WorldNpcSkillResourceOverTimeEffectProfile profile,
		int valueBeforeCap,
		int finalValue,
		bool healSkillDeboostApplied,
		bool PercentApplied,
		int CurrentResource,
		int MaxResource,
		bool TargetIsPlayer)
	{
		return new WorldNpcSkillResourceOverTimePeriodicActionResult(
			WorldNpcSkillResourceOverTimeStatus.Applied,
			kind,
			profile.ResourceType,
			profile.IsDamage,
			OriginalValue: valueBeforeCap,
			ValueBeforeCap: valueBeforeCap,
			FinalValue: finalValue,
			CurrentResource: CurrentResource,
			MaxResource: MaxResource,
			PercentApplied: PercentApplied,
			TargetIsPlayer: TargetIsPlayer,
			HealSkillDeboostApplied: healSkillDeboostApplied,
			PacketType: profile.PacketType,
			PacketLog: profile.PacketLog);
	}

	private sealed record WorldNpcSkillResourceOverTimeEffectProfile(
		WorldNpcEffectResourceType ResourceType,
		bool IsDamage,
		bool ReserveValueOnStart,
		bool RequiresPlayerTarget,
		SmAttackStatusType? PacketType,
		SmAttackStatusLog? PacketLog);

	private static WorldNpcSkillInstantResourceEffectProfile GetInstantResourceEffectProfile(WorldNpcSkillInstantResourceEffectKind kind)
	{
		return kind switch
		{
			WorldNpcSkillInstantResourceEffectKind.MpAttackInstant => new WorldNpcSkillInstantResourceEffectProfile(
				ResourceType: WorldNpcEffectResourceType.Mp,
				RequiresPlayerTarget: false,
				RequiresEnemyTarget: false,
				ReserveValueOnCalculate: true,
				SchedulesDelayedAction: false,
				Delay: null,
				PacketType: SmAttackStatusType.DamageMp,
				PacketLog: SmAttackStatusLog.MpAttack),
			WorldNpcSkillInstantResourceEffectKind.FpAttackInstant => new WorldNpcSkillInstantResourceEffectProfile(
				ResourceType: WorldNpcEffectResourceType.Fp,
				RequiresPlayerTarget: true,
				RequiresEnemyTarget: false,
				ReserveValueOnCalculate: true,
				SchedulesDelayedAction: false,
				Delay: null,
				PacketType: SmAttackStatusType.FpDamage,
				PacketLog: SmAttackStatusLog.FpAttack),
			WorldNpcSkillInstantResourceEffectKind.DelayedFpAttackInstant => new WorldNpcSkillInstantResourceEffectProfile(
				ResourceType: WorldNpcEffectResourceType.Fp,
				RequiresPlayerTarget: true,
				RequiresEnemyTarget: true,
				ReserveValueOnCalculate: false,
				SchedulesDelayedAction: true,
				Delay: null,
				PacketType: SmAttackStatusType.FpDamage,
				PacketLog: SmAttackStatusLog.FpAttack),
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unhandled Java instant resource effect kind."),
		};
	}

	private static WorldNpcSkillInstantResourceEffectResult CreateSkippedInstantResourceEffectResult(
		WorldNpcSkillInstantEffectStatus status,
		WorldNpcSkillInstantResourceEffectRequest request,
		WorldNpcSkillInstantResourceEffectProfile profile)
	{
		return new WorldNpcSkillInstantResourceEffectResult(
			Status: status,
			Kind: request.Kind,
			OriginalValue: request.Value,
			FinalValue: 0,
			SkillId: request.SkillId,
			Position: request.Position,
			ResourceType: profile.ResourceType,
			PercentApplied: request.Percent,
			MaxResource: request.MaxResource,
			TargetIsPlayer: request.TargetIsPlayer,
			IsEnemy: request.IsEnemy,
			ReserveValueOnCalculate: profile.ReserveValueOnCalculate,
			SchedulesDelayedAction: profile.SchedulesDelayedAction,
			Delay: profile.SchedulesDelayedAction ? request.Delay ?? TimeSpan.Zero : null,
			Reserved: null,
			PacketType: profile.PacketType,
			PacketLog: profile.PacketLog);
	}

	private static WorldNpcSkillInstantDrainEffectProfile GetInstantDrainEffectProfile(WorldNpcSkillInstantDrainEffectKind kind)
	{
		return kind switch
		{
			WorldNpcSkillInstantDrainEffectKind.SkillAttackDrainInstant => new WorldNpcSkillInstantDrainEffectProfile(
				HpPacketType: SmAttackStatusType.AbsorbedHp,
				MpPacketType: SmAttackStatusType.Mp,
				PacketLog: SmAttackStatusLog.SkillAttackDrainInstant),
			WorldNpcSkillInstantDrainEffectKind.SpellAttackDrainInstant => new WorldNpcSkillInstantDrainEffectProfile(
				HpPacketType: SmAttackStatusType.Hp,
				MpPacketType: SmAttackStatusType.AbsorbedMp,
				PacketLog: SmAttackStatusLog.SpellAttackDrainInstant),
			_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unhandled Java instant drain effect kind."),
		};
	}

	private sealed record WorldNpcSkillInstantResourceEffectProfile(
		WorldNpcEffectResourceType ResourceType,
		bool RequiresPlayerTarget,
		bool RequiresEnemyTarget,
		bool ReserveValueOnCalculate,
		bool SchedulesDelayedAction,
		TimeSpan? Delay,
		SmAttackStatusType PacketType,
		SmAttackStatusLog PacketLog);

	private sealed record WorldNpcSkillInstantDrainEffectProfile(
		SmAttackStatusType HpPacketType,
		SmAttackStatusType MpPacketType,
		SmAttackStatusLog PacketLog);
}

public sealed record WorldNpcSkillOverTimeEffectStartRequest(
	WorldNpcSkillOverTimeEffectKind Kind,
	float BaseValue,
	int SkillId,
	int Position,
	TimeSpan CheckTime,
	WorldNpcSkillMagicalOverTimeOptions? MagicalOverTime = null);

public sealed record WorldNpcSkillOverTimeEffectStartResult(
	WorldNpcSkillOverTimeEffectCallerStatus Status,
	WorldNpcSkillOverTimeEffectKind Kind,
	float BaseValue,
	int SkillId,
	int Position,
	bool UseMagicBoost,
	PlayerAbnormalState AbnormalState,
	bool AppliesAbnormalState,
	bool ReserveDamageOnStart,
	TimeSpan CheckTime,
	bool SchedulesPeriodicTask,
	TimeSpan? InitialDelay,
	WorldNpcSkillEffectReservedResult? Reserved,
	WorldNpcSkillMagicalOverTimeResult? CalculationResult)
{
	public bool HasUnresolvedInputs => Status == WorldNpcSkillOverTimeEffectCallerStatus.CalculationUnresolved ||
		CalculationResult?.HasUnresolvedInputs == true;
}

public sealed record WorldNpcSkillOverTimePeriodicActionRequest(
	IWorldNpcObject? Target,
	Player? Effector,
	int SkillId,
	WorldNpcSkillOverTimeEffectKind Kind,
	int? ReservedDamage = null,
	float? BaseValue = null,
	WorldNpcSkillMagicalOverTimeOptions? MagicalOverTime = null,
	WorldNpcSkillDamageOptions? DamageOptions = null);

public sealed record WorldNpcSkillOverTimePeriodicActionResult(
	WorldNpcSkillOverTimeEffectCallerStatus Status,
	WorldNpcSkillOverTimeEffectKind Kind,
	WorldNpcSkillDamageKind DamageKind,
	int Damage,
	bool UsedReservedDamage,
	bool RecalculatedDamage,
	WorldNpcSkillMagicalOverTimeResult? CalculationResult,
	WorldNpcSkillDamageResult? DamageResult)
{
	public bool Applied => Status == WorldNpcSkillOverTimeEffectCallerStatus.Applied;

	public static WorldNpcSkillOverTimePeriodicActionResult Skipped(
		WorldNpcSkillOverTimeEffectCallerStatus status,
		WorldNpcSkillOverTimeEffectKind kind,
		WorldNpcSkillDamageKind damageKind,
		WorldNpcSkillMagicalOverTimeResult? calculationResult = null)
	{
		return new WorldNpcSkillOverTimePeriodicActionResult(
			status,
			kind,
			damageKind,
			Damage: 0,
			UsedReservedDamage: false,
			RecalculatedDamage: false,
			CalculationResult: calculationResult,
			DamageResult: null);
	}

	public static WorldNpcSkillOverTimePeriodicActionResult AppliedResult(
		WorldNpcSkillOverTimeEffectKind kind,
		WorldNpcSkillDamageKind damageKind,
		int damage,
		bool usedReservedDamage,
		bool recalculatedDamage,
		WorldNpcSkillMagicalOverTimeResult? calculationResult,
		WorldNpcSkillDamageResult damageResult)
	{
		return new WorldNpcSkillOverTimePeriodicActionResult(
			WorldNpcSkillOverTimeEffectCallerStatus.Applied,
			kind,
			damageKind,
			damage,
			usedReservedDamage,
			recalculatedDamage,
			calculationResult,
			damageResult);
	}
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
	TimeSpan? Delay = null,
	bool UsesTemplateDamage = false,
	WorldNpcSkillResultCalculationOptions? ResultCalculation = null)
{
	public static WorldNpcSkillDamageOptions Default { get; } = new();
}

public sealed record WorldNpcSkillDamageResult(
	WorldNpcSkillDamageKind Kind,
	WorldNpcDamageResult DamageResult,
	WorldNpcSkillAttackObserverNotification? AttackObserverNotification,
	WorldNpcSkillDotAttackedObserverNotification? DotAttackedObserverNotification = null,
	WorldNpcSkillDrainResult? DrainResult = null,
	WorldNpcSkillDelayResult? DelayResult = null,
	WorldNpcSkillResultCalculationResult? CalculationResult = null);

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
	PoisonPeriodic,
}

public enum WorldNpcSkillOverTimeEffectKind
{
	Bleed,
	Poison,
	SpellAttack,
	SpellAttackDrain,
}

public enum WorldNpcSkillOverTimeEffectCallerStatus
{
	Applied,
	MissingReservedDamage,
	MissingBaseValue,
	CalculationUnresolved,
}

public sealed record WorldNpcSkillResourceOverTimeStartRequest(
	WorldNpcSkillResourceOverTimeEffectKind Kind,
	int Value,
	int SkillId,
	int Position,
	TimeSpan CheckTime);

public sealed record WorldNpcSkillResourceOverTimeStartResult(
	WorldNpcSkillResourceOverTimeStatus Status,
	WorldNpcSkillResourceOverTimeEffectKind Kind,
	int Value,
	int SkillId,
	int Position,
	WorldNpcEffectResourceType ResourceType,
	bool IsDamage,
	bool ReserveValueOnStart,
	TimeSpan CheckTime,
	bool SchedulesPeriodicTask,
	TimeSpan? InitialDelay,
	WorldNpcSkillEffectReservedResult? Reserved);

public sealed record WorldNpcSkillResourceOverTimePeriodicActionRequest(
	WorldNpcSkillResourceOverTimeEffectKind Kind,
	int Value,
	int SkillId,
	int CurrentResource,
	int MaxResource,
	bool Percent = false,
	bool TargetIsPlayer = true,
	bool HasItemTemplate = false,
	int? HealSkillDeboostedValue = null);

public sealed record WorldNpcSkillResourceOverTimePeriodicActionResult(
	WorldNpcSkillResourceOverTimeStatus Status,
	WorldNpcSkillResourceOverTimeEffectKind Kind,
	WorldNpcEffectResourceType ResourceType,
	bool IsDamage,
	int OriginalValue,
	int ValueBeforeCap,
	int FinalValue,
	int CurrentResource,
	int MaxResource,
	bool PercentApplied,
	bool TargetIsPlayer,
	bool HealSkillDeboostApplied,
	SmAttackStatusType? PacketType,
	SmAttackStatusLog? PacketLog)
{
	public bool Applied => Status == WorldNpcSkillResourceOverTimeStatus.Applied;
}

public enum WorldNpcSkillResourceOverTimeEffectKind
{
	MpAttack,
	FpAttack,
	HpHeal,
	MpHeal,
	FpHeal,
	DpHeal,
}

public enum WorldNpcSkillResourceOverTimeStatus
{
	Applied,
	TargetNotPlayer,
	NoResourceChange,
}

public sealed record WorldNpcSkillInstantResourceEffectRequest(
	WorldNpcSkillInstantResourceEffectKind Kind,
	int Value,
	int SkillId,
	int Position,
	int MaxResource,
	bool Percent = false,
	bool TargetIsPlayer = true,
	bool IsEnemy = true,
	TimeSpan? Delay = null);

public sealed record WorldNpcSkillInstantResourceEffectResult(
	WorldNpcSkillInstantEffectStatus Status,
	WorldNpcSkillInstantResourceEffectKind Kind,
	int OriginalValue,
	int FinalValue,
	int SkillId,
	int Position,
	WorldNpcEffectResourceType ResourceType,
	bool PercentApplied,
	int MaxResource,
	bool TargetIsPlayer,
	bool IsEnemy,
	bool ReserveValueOnCalculate,
	bool SchedulesDelayedAction,
	TimeSpan? Delay,
	WorldNpcSkillEffectReservedResult? Reserved,
	SmAttackStatusType PacketType,
	SmAttackStatusLog PacketLog)
{
	public bool Applied => Status == WorldNpcSkillInstantEffectStatus.Applied;
}

public sealed record WorldNpcSkillInstantDrainEffectRequest(
	WorldNpcSkillInstantDrainEffectKind Kind,
	int ReservedDamage,
	int HpPercent = 0,
	int MpPercent = 0);

public sealed record WorldNpcSkillInstantDrainEffectResult(
	WorldNpcSkillInstantEffectStatus Status,
	WorldNpcSkillInstantDrainEffectKind Kind,
	int ReservedDamage,
	int HpPercent,
	int MpPercent,
	int HpAmount,
	int MpAmount,
	TimeSpan Delay,
	SmAttackStatusType HpPacketType,
	SmAttackStatusType MpPacketType,
	SmAttackStatusLog PacketLog)
{
	public bool Applied => Status == WorldNpcSkillInstantEffectStatus.Applied;
}

public enum WorldNpcSkillInstantResourceEffectKind
{
	MpAttackInstant,
	FpAttackInstant,
	DelayedFpAttackInstant,
}

public enum WorldNpcSkillInstantDrainEffectKind
{
	SkillAttackDrainInstant,
	SpellAttackDrainInstant,
}

public enum WorldNpcSkillInstantEffectStatus
{
	Applied,
	TargetNotPlayer,
	NotEnemy,
}
