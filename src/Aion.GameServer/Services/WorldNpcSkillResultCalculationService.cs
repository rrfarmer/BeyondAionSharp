namespace Aion.GameServer.Services;

public sealed class WorldNpcSkillResultCalculationService
{
	public WorldNpcSkillResultCalculationResult Calculate(WorldNpcSkillResultCalculationRequest request)
	{
		// Java parity: controllers/attack/AttackUtil.calculateSkillResult stages the EffectReserved damage result.
		var options = request.Options ?? WorldNpcSkillResultCalculationOptions.Default;
		var inputDamage = Math.Max(0, request.InputDamage);
		var cannotMiss = options.CannotMiss || options.AttackStatusCalculation?.CannotMiss == true;
		var canDodgeOrResist = !cannotMiss;
		var baseDamageMultiplier = CalculateBaseDamageMultiplier(
			options.BaseDamageMultiplier,
			request.ShouldIncreaseByOneTimeBoost,
			inputDamage);
		var damageBeforeRandom = baseDamageMultiplier.Applied
			? baseDamageMultiplier.FinalDamage
			: inputDamage;
		var random = CalculateRandomMultiplier(options);
		var attackStatus = CalculateAttackStatus(options.AttackStatusCalculation, options.AttackStatus);
		var finalDamage = random.Status == WorldNpcSkillResultCalculationStatus.Calculated
			? (int)(damageBeforeRandom * random.Multiplier)
			: damageBeforeRandom;
		var normalizedFinalDamage = Math.Max(0, finalDamage);
		var criticalDamage = CalculateSkillCriticalDamage(options.CriticalDamage, attackStatus.FinalStatus, normalizedFinalDamage);
		var damageAfterCritical = criticalDamage.Applied ? criticalDamage.FinalDamage : normalizedFinalDamage;
		var blockedDamage = CalculateSkillBlockedDamage(options.BlockedDamage, attackStatus.FinalStatus, damageAfterCritical);
		var damageAfterBlocked = blockedDamage.Applied ? blockedDamage.FinalDamage : damageAfterCritical;
		var damageModifier = CalculateDamageModifier(options.DamageModifier, attackStatus.FinalStatus, damageAfterBlocked);
		var resultDamage = damageModifier.Applied ? damageModifier.FinalDamage : damageAfterBlocked;
		var attackResult = new WorldNpcSkillAttackResult(
			resultDamage,
			attackStatus.FinalStatus,
			options.HitType,
			ShieldChecked: !request.IgnoreShield);
		var additionalHits = CalculateAdditionalHits(options.AdditionalHits, attackStatus.FinalStatus, attackResult);
		var npcAiDamageModifier = CalculateNpcAiDamageModifier(options.NpcAiDamageModifier, attackResult, additionalHits.GeneratedHits);
		var finalAttackResult = npcAiDamageModifier.Applied
			? attackResult with { Damage = npcAiDamageModifier.PrimaryFinalDamage }
			: attackResult;
		var shieldObserver = CalculateShieldObserver(request.IgnoreShield, options.ShieldObserver, finalAttackResult);
		finalAttackResult = shieldObserver.Applied
			? finalAttackResult with
			{
				Damage = shieldObserver.FinalDamage,
				ShieldType = shieldObserver.ShieldType,
				ReflectedDamage = shieldObserver.ReflectedDamage,
				ReflectedSkillId = shieldObserver.ReflectedSkillId,
				ProtectedSkillId = shieldObserver.ProtectedSkillId,
				ProtectedDamage = shieldObserver.ProtectedDamage,
				ProtectorId = shieldObserver.ProtectorId,
				MpAbsorbed = shieldObserver.MpAbsorbed,
				MpShieldSkillId = shieldObserver.MpShieldSkillId,
				LaunchSubEffect = shieldObserver.LaunchSubEffect,
			}
			: finalAttackResult;
		var effectReserved = new WorldNpcSkillEffectReservedResult(
			options.EffectPosition,
			finalAttackResult.Damage,
			options.ResourceType,
			options.IsDamage,
			request.SendResult);
		return new WorldNpcSkillResultCalculationResult(
			inputDamage,
			finalAttackResult.Damage,
			options.RandomDamageType,
			random.Multiplier,
			random.Status,
			cannotMiss,
			canDodgeOrResist,
			request.ShouldApplyAttackerMovementModifier,
			request.IgnoreShield,
			request.SendResult,
			request.ShouldIncreaseByOneTimeBoost,
			request.UsesTemplateDamage,
			baseDamageMultiplier,
			criticalDamage,
			blockedDamage,
			attackStatus,
			damageModifier,
			npcAiDamageModifier,
			shieldObserver,
			finalAttackResult,
			additionalHits,
			effectReserved);
	}

	private static WorldNpcSkillAttackStatusCalculationResult CalculateAttackStatus(
		WorldNpcSkillAttackStatusCalculationOptions? options,
		WorldNpcSkillAttackStatus fallbackStatus)
	{
		return options?.Kind switch
		{
			null or WorldNpcSkillAttackStatusCalculationKind.NotRequested => WorldNpcSkillAttackStatusCalculationResult.NotRequested(fallbackStatus),
			WorldNpcSkillAttackStatusCalculationKind.Physical => CalculatePhysicalStatus(options),
			WorldNpcSkillAttackStatusCalculationKind.Magical => CalculateMagicalStatus(options),
			_ => throw new ArgumentOutOfRangeException(nameof(options), options.Kind, "Unhandled staged attack-status calculation kind."),
		};
	}

	private static WorldNpcSkillAttackStatusCalculationResult CalculatePhysicalStatus(WorldNpcSkillAttackStatusCalculationOptions options)
	{
		// Java parity: controllers/attack/AttackUtil.calculatePhysicalStatus.
		var status = WorldNpcSkillAttackStatus.NormalHit;
		var dodgeChecked = options.CannotMiss || !options.IsSkill;
		var blockChecked = options.CannotMiss;
		var parryChecked = options.CannotMiss;
		var dodgeMissing = dodgeChecked && options.DodgeResult == null;
		var blockMissing = blockChecked && options.BlockResult == null;
		var parryMissing = parryChecked && options.ParryResult == null;

		if (!options.CannotMiss)
		{
			if (!options.IsSkill)
			{
				if (options.DodgeResult == true)
					status = WorldNpcSkillAttackStatus.Dodge;
			}

			if (status == WorldNpcSkillAttackStatus.NormalHit && options.TargetIsPlayer && options.TargetHasShield)
			{
				blockChecked = true;
				blockMissing = options.BlockResult == null;
				if (options.BlockResult == true)
					status = WorldNpcSkillAttackStatus.Block;
			}

			if (status == WorldNpcSkillAttackStatus.NormalHit && options.TargetIsPlayer)
			{
				parryChecked = true;
				parryMissing = options.ParryResult == null;
				if (options.ParryResult == true)
					status = WorldNpcSkillAttackStatus.Parry;
			}
		}

		var baseStatus = status;
		var criticalChecked = true;
		var criticalMissing = options.CriticalResult == null;
		var criticalUpgraded = options.CriticalResult == true;
		if (criticalUpgraded)
			status = status.GetCriticalStatusFor();

		var offHandConverted = !options.IsMainHand;
		if (offHandConverted)
			status = status.GetOffHandStatus();

		return new WorldNpcSkillAttackStatusCalculationResult(
			WorldNpcSkillAttackStatusCalculationKind.Physical,
			WorldNpcSkillAttackStatus.NormalHit,
			baseStatus,
			status,
			options.IsMainHand,
			options.AccuracyModifier,
			options.CriticalProbability,
			options.IsSkill,
			options.CannotMiss,
			options.ApplyMagicalCritical,
			options.TargetIsPlayer,
			options.TargetHasShield,
			dodgeChecked,
			blockChecked,
			parryChecked,
			MagicalResistChecked: false,
			criticalChecked,
			dodgeMissing,
			blockMissing,
			parryMissing,
			MagicalResistInputMissing: false,
			criticalMissing,
			criticalUpgraded,
			offHandConverted,
			ProbeOnly: options.CannotMiss);
	}

	private static WorldNpcSkillAttackStatusCalculationResult CalculateMagicalStatus(WorldNpcSkillAttackStatusCalculationOptions options)
	{
		// Java parity: controllers/attack/AttackUtil.calculateMagicalStatus.
		var status = WorldNpcSkillAttackStatus.NormalHit;
		var magicalResistChecked = !options.IsSkill;
		var magicalResistMissing = magicalResistChecked && options.MagicalResistResult == null;
		var criticalChecked = false;
		var criticalMissing = false;
		var criticalUpgraded = false;

		if (magicalResistChecked && options.MagicalResistResult == true)
		{
			status = WorldNpcSkillAttackStatus.Resist;
		}
		else if (options.ApplyMagicalCritical)
		{
			criticalChecked = true;
			criticalMissing = options.CriticalResult == null;
			criticalUpgraded = options.CriticalResult == true;
			if (criticalUpgraded)
				status = WorldNpcSkillAttackStatus.Critical;
		}

		return new WorldNpcSkillAttackStatusCalculationResult(
			WorldNpcSkillAttackStatusCalculationKind.Magical,
			WorldNpcSkillAttackStatus.NormalHit,
			status.GetBaseStatus(),
			status,
			options.IsMainHand,
			options.AccuracyModifier,
			options.CriticalProbability,
			options.IsSkill,
			options.CannotMiss,
			options.ApplyMagicalCritical,
			options.TargetIsPlayer,
			options.TargetHasShield,
			DodgeChecked: false,
			BlockChecked: false,
			ParryChecked: false,
			magicalResistChecked,
			criticalChecked,
			DodgeInputMissing: false,
			BlockInputMissing: false,
			ParryInputMissing: false,
			magicalResistMissing,
			criticalMissing,
			criticalUpgraded,
			OffHandConverted: false,
			ProbeOnly: false);
	}

	private static RandomMultiplierResult CalculateRandomMultiplier(WorldNpcSkillResultCalculationOptions options)
	{
		// Java parity: controllers/attack/AttackUtil.randomizeDamage.
		return options.RandomDamageType switch
		{
			0 => new RandomMultiplierResult(1f, WorldNpcSkillResultCalculationStatus.Calculated),
			1 => CalculateRollBucket(options.RandomRoll, low: 0.5f, middle: 1.0f, high: 1.5f),
			2 => CalculateChanceBucket(options.RandomChanceRoll, success: 0.6f, failure: 2.0f),
			3 => CalculateRollBucket(options.RandomRoll, low: 0.9f, middle: 1.0f, high: 1.1f),
			6 => CalculateChanceBucket(options.RandomChanceRoll, success: 1.0f, failure: 2.0f),
			4 or 5 or 7 or 8 or 9 or 10 => new RandomMultiplierResult(1f, WorldNpcSkillResultCalculationStatus.Calculated),
			_ => throw new ArgumentOutOfRangeException(
				nameof(options),
				options.RandomDamageType,
				"Unhandled Java rnddmg type."),
		};
	}

	private static RandomMultiplierResult CalculateRollBucket(int? roll, float low, float middle, float high)
	{
		if (roll == null)
			return new RandomMultiplierResult(1f, WorldNpcSkillResultCalculationStatus.RandomRollMissing);

		var normalizedRoll = Math.Clamp(roll.Value, 0, 19);
		var multiplier = normalizedRoll <= 6
			? low
			: normalizedRoll <= 12
				? middle
				: high;
		return new RandomMultiplierResult(multiplier, WorldNpcSkillResultCalculationStatus.Calculated);
	}

	private static RandomMultiplierResult CalculateChanceBucket(double? chanceRoll, float success, float failure)
	{
		if (chanceRoll == null)
			return new RandomMultiplierResult(1f, WorldNpcSkillResultCalculationStatus.RandomChanceMissing);

		var multiplier = chanceRoll.Value < 70.0d ? success : failure;
		return new RandomMultiplierResult(multiplier, WorldNpcSkillResultCalculationStatus.Calculated);
	}

	private static WorldNpcSkillDamageModifierResult CalculateDamageModifier(
		WorldNpcSkillDamageModifierOptions? options,
		WorldNpcSkillAttackStatus status,
		int damage)
	{
		if (options == null)
			return WorldNpcSkillDamageModifierResult.NotRequested(damage, status);

		// Java parity: controllers/attack/AttackUtil.adjustDamageByStatModifiers.
		var baseStatus = status.GetBaseStatus();
		var skippedForStatus = baseStatus is WorldNpcSkillAttackStatus.Dodge or WorldNpcSkillAttackStatus.Resist;
		var mainMultiplier = 1f;
		var offMultiplier = 1f;
		var reduceRatio = 0f;
		var reduceMax = int.MaxValue;
		var blockInputMissing = false;

		if (skippedForStatus)
		{
			return WorldNpcSkillDamageModifierResult.Skipped(
				damage,
				status,
				baseStatus,
				options.Element,
				SkippedForCounterStatus: true);
		}

		if (baseStatus == WorldNpcSkillAttackStatus.Block)
		{
			if (options.TargetIsPlayer)
			{
				blockInputMissing = options.BlockReduceRatio == null || options.BlockReduceMax == null;
				if (!blockInputMissing)
				{
					reduceRatio = options.BlockReduceRatio!.Value;
					reduceMax = options.BlockReduceMax!.Value;
				}
			}
			else
			{
				reduceRatio = 10f;
			}
		}
		else if (baseStatus == WorldNpcSkillAttackStatus.Parry)
		{
			mainMultiplier *= 0.6f;
			offMultiplier *= 0.6f;
		}

		if (status.IsCritical())
		{
			mainMultiplier = 1.5f;
			if (options.Element == WorldNpcSkillDamageModifierElement.Physical && options.MainHandWeaponGroup != null)
			{
				mainMultiplier = options.MainHandWeaponGroup.Value.GetJavaCriticalMultiplier();
				if (options.OffHandWeaponGroup != null)
					offMultiplier = options.OffHandWeaponGroup.Value.GetJavaCriticalMultiplier();
			}

			if (options.TargetIsPlayer)
			{
				var fortitudeModifier = options.CriticalDamageReduce / 1000f;
				mainMultiplier -= fortitudeModifier;
				offMultiplier += fortitudeModifier;
			}
		}

		var defenseMissing = options.Defense == null;
		var pvpPveMissing = options.PvpPveMultiplier == null;
		if (defenseMissing || pvpPveMissing || blockInputMissing)
		{
			return WorldNpcSkillDamageModifierResult.Unresolved(
				damage,
				status,
				baseStatus,
				options.Element,
				mainMultiplier,
				offMultiplier,
				reduceRatio,
				reduceMax,
				defenseMissing,
				pvpPveMissing,
				blockInputMissing);
		}

		var exactDamage = damage - options.Defense!.Value / 10f;
		exactDamage *= mainMultiplier;
		exactDamage *= options.AttackerMovementMultiplier;
		var blockReduction = 0f;
		if (reduceRatio > 0)
		{
			blockReduction = exactDamage - exactDamage * reduceRatio;
			if (blockReduction > reduceMax)
				blockReduction = reduceMax;
			exactDamage -= blockReduction;
		}

		exactDamage *= options.PvpPveMultiplier!.Value;
		if (exactDamage < 1)
			exactDamage = 1;

		return WorldNpcSkillDamageModifierResult.AppliedResult(
			damage,
			status,
			baseStatus,
			options.Element,
			(int)exactDamage,
			exactDamage,
			mainMultiplier,
			offMultiplier,
			options.Defense.Value,
			options.AttackerMovementMultiplier,
			options.PvpPveMultiplier.Value,
			reduceRatio,
			reduceMax,
			blockReduction);
	}

	private static WorldNpcSkillAdditionalHitResult CalculateAdditionalHits(
		WorldNpcSkillAdditionalHitOptions? options,
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillAttackResult mainHandAttack)
	{
		if (options == null)
			return WorldNpcSkillAdditionalHitResult.NotRequested();

		// Java parity: controllers/attack/AttackUtil.calculateAdditionalHitCount/amplifyDamageByAdditionalHitCount.
		var skippedByStatus = status is WorldNpcSkillAttackStatus.Dodge or WorldNpcSkillAttackStatus.Resist;
		var eligible = options.AttackerIsPlayer && !skippedByStatus;
		if (!eligible)
			return WorldNpcSkillAdditionalHitResult.Skipped(options.AttackerIsPlayer, skippedByStatus);

		var mainRollMissing = options.HasMainHandWeapon && options.MainHandRoll == null;
		var offHandEligible = options.HasOffHandAttackResult && options.HasOffHandWeapon && !options.OffHandIsShield;
		var offHandRollMissing = offHandEligible && options.OffHandRoll == null;
		var offHandDamageMissing = offHandEligible && (options.OffHandRoll ?? 0) > 0 && options.OffHandDamage == null;
		if (mainRollMissing || offHandRollMissing || offHandDamageMissing)
		{
			return WorldNpcSkillAdditionalHitResult.Unresolved(
				options.AttackerIsPlayer,
				options.HasMainHandWeapon,
				offHandEligible,
				mainRollMissing,
				offHandRollMissing,
				offHandDamageMissing);
		}

		var mainHandCount = 0;
		if (options.HasMainHandWeapon)
		{
			var normalizedMainRoll = Math.Clamp(options.MainHandRoll!.Value, 0, options.MainHandWeaponHitCount);
			mainHandCount = normalizedMainRoll - 1;
		}

		var offHandCount = 0;
		if (offHandEligible)
		{
			var maxOffHandRoll = Math.Max(0, options.OffHandWeaponHitCount - 1);
			offHandCount = Math.Clamp(options.OffHandRoll!.Value, 0, maxOffHandRoll);
		}

		var generatedHits = new List<WorldNpcSkillAdditionalHitAttackResult>();
		var loopCount = mainHandCount + offHandCount;
		for (var i = 0; i < loopCount; i++)
		{
			if (i < mainHandCount)
			{
				if (mainHandAttack.Damage >= 10)
				{
					generatedHits.Add(new WorldNpcSkillAdditionalHitAttackResult(
						(int)(mainHandAttack.Damage * 0.1f),
						WorldNpcSkillAttackStatus.NormalHit,
						mainHandAttack.HitType,
						IsOffHand: false));
				}
			}
			else if (options.OffHandDamage is >= 10)
			{
				generatedHits.Add(new WorldNpcSkillAdditionalHitAttackResult(
					(int)(options.OffHandDamage.Value * 0.1f),
					WorldNpcSkillAttackStatus.OffHandNormalHit,
					options.OffHandHitType,
					IsOffHand: true));
			}
		}

		return WorldNpcSkillAdditionalHitResult.AppliedResult(
			options.AttackerIsPlayer,
			options.HasMainHandWeapon,
			offHandEligible,
			mainHandCount,
			offHandCount,
			loopCount,
			generatedHits);
	}

	private static WorldNpcSkillNpcAiDamageModifierResult CalculateNpcAiDamageModifier(
		WorldNpcSkillNpcAiDamageModifierOptions? options,
		WorldNpcSkillAttackResult primaryAttack,
		IReadOnlyList<WorldNpcSkillAdditionalHitAttackResult> additionalHits)
	{
		if (options == null)
			return WorldNpcSkillNpcAiDamageModifierResult.NotRequested(primaryAttack.Damage);

		// Java parity: controllers/attack/AttackUtil.modifyDamageByNpcAi.
		var hasNpcParticipant = options.AttackerIsNpc || options.AttackedIsNpc;
		if (!hasNpcParticipant)
			return WorldNpcSkillNpcAiDamageModifierResult.Skipped(primaryAttack.Damage, options.AttackerIsNpc, options.AttackedIsNpc);

		var attackerHookMissing = options.AttackerIsNpc && options.AttackerNpcOwnerDamageMultiplier == null;
		var attackedHookMissing = options.AttackedIsNpc && options.AttackedNpcDamageMultiplier == null;
		if (attackerHookMissing || attackedHookMissing)
		{
			return WorldNpcSkillNpcAiDamageModifierResult.Unresolved(
				primaryAttack.Damage,
				options.AttackerIsNpc,
				options.AttackedIsNpc,
				attackerHookMissing,
				attackedHookMissing);
		}

		var primaryExactDamage = ApplyNpcAiDamage(primaryAttack.Damage, options);
		var modifiedAdditionalHits = additionalHits
			.Select((hit, index) =>
			{
				var exactDamage = ApplyNpcAiDamage(hit.Damage, options);
				return new WorldNpcSkillNpcAiDamageModifierHitResult(
					index,
					hit.Damage,
					(int)exactDamage,
					exactDamage,
					hit.AttackStatus,
					hit.HitType,
					hit.IsOffHand);
			})
			.ToArray();

		return WorldNpcSkillNpcAiDamageModifierResult.AppliedResult(
			primaryAttack.Damage,
			(int)primaryExactDamage,
			primaryExactDamage,
			options.AttackerIsNpc,
			options.AttackedIsNpc,
			options.AttackerNpcOwnerDamageMultiplier,
			options.AttackedNpcDamageMultiplier,
			modifiedAdditionalHits);
	}

	private static float ApplyNpcAiDamage(int damage, WorldNpcSkillNpcAiDamageModifierOptions options)
	{
		var modifiedDamage = (float)damage;
		if (options.AttackerIsNpc)
			modifiedDamage *= options.AttackerNpcOwnerDamageMultiplier!.Value;
		if (options.AttackedIsNpc)
			modifiedDamage *= options.AttackedNpcDamageMultiplier!.Value;
		return modifiedDamage;
	}

	private static WorldNpcSkillBaseDamageMultiplierResult CalculateBaseDamageMultiplier(
		WorldNpcSkillBaseDamageMultiplierOptions? options,
		bool shouldIncreaseByOneTimeBoost,
		int damage)
	{
		if (options == null)
			return WorldNpcSkillBaseDamageMultiplierResult.NotRequested(damage);

		// Java parity: controllers/ObserveController.getBasePhysicalDamageMultiplier/getBaseMagicalDamageMultiplier.
		var skippedByOneTimeBoost = options.Kind == WorldNpcSkillBaseDamageMultiplierKind.Magical && !shouldIncreaseByOneTimeBoost;
		if (skippedByOneTimeBoost)
			return WorldNpcSkillBaseDamageMultiplierResult.Skipped(damage, options.Kind, shouldIncreaseByOneTimeBoost);

		if (!options.ObserverMultipliersKnown)
			return WorldNpcSkillBaseDamageMultiplierResult.Unresolved(damage, options.Kind, shouldIncreaseByOneTimeBoost, options.IsSkill);

		var observerMultipliers = options.ObserverMultipliers ?? Array.Empty<float>();
		var multiplier = 1f;
		foreach (var observerMultiplier in observerMultipliers)
			multiplier *= observerMultiplier;

		var exactDamage = damage * multiplier;
		return WorldNpcSkillBaseDamageMultiplierResult.AppliedResult(
			damage,
			(int)exactDamage,
			exactDamage,
			multiplier,
			options.Kind,
			shouldIncreaseByOneTimeBoost,
			options.IsSkill,
			observerMultipliers.Count);
	}

	private static WorldNpcSkillCriticalDamageResult CalculateSkillCriticalDamage(
		WorldNpcSkillCriticalDamageOptions? options,
		WorldNpcSkillAttackStatus status,
		int damage)
	{
		if (options == null)
			return WorldNpcSkillCriticalDamageResult.NotRequested(damage, status);

		// Java parity: controllers/attack/AttackUtil.calculateWeaponCritical.
		if (!status.IsCritical())
			return WorldNpcSkillCriticalDamageResult.FromSkippedStatus(damage, status);

		if (options.TargetIsPlayer && options.AppliesFortitudeStat && options.CriticalDamageReduce == null)
			return WorldNpcSkillCriticalDamageResult.Unresolved(damage, status, options);

		var coefficient = 1.5f;
		if (options.Element == WorldNpcSkillDamageModifierElement.Physical && options.WeaponGroup != null)
			coefficient = options.WeaponGroup.Value.GetJavaCriticalMultiplier();

		if (options.TargetIsPlayer && options.AppliesFortitudeStat)
		{
			var fortitudeModifier = options.CriticalDamageReduce!.Value / 1000f;
			coefficient = options.IsMainHand
				? coefficient - fortitudeModifier
				: coefficient + fortitudeModifier;
		}

		coefficient += options.CriticalAddDamage / 100f;
		var exactDamage = damage * coefficient;
		return WorldNpcSkillCriticalDamageResult.AppliedResult(
			damage,
			(int)exactDamage,
			exactDamage,
			status,
			options,
			coefficient);
	}

	private static WorldNpcSkillBlockedDamageResult CalculateSkillBlockedDamage(
		WorldNpcSkillBlockedDamageOptions? options,
		WorldNpcSkillAttackStatus status,
		int damage)
	{
		if (options == null)
			return WorldNpcSkillBlockedDamageResult.NotRequested(damage, status);

		// Java parity: controllers/attack/AttackUtil.calculateBlockedDamage.
		if (status.GetBaseStatus() != WorldNpcSkillAttackStatus.Block)
			return WorldNpcSkillBlockedDamageResult.FromSkippedStatus(damage, status);

		var shieldReduceMaxMissing = options.TargetIsPlayer && options.HasShield && options.ShieldReduceMax == null;
		if (options.DamageReduceStat == null || shieldReduceMaxMissing)
			return WorldNpcSkillBlockedDamageResult.Unresolved(damage, status, options, shieldReduceMaxMissing);

		var reduceValue = damage - damage * options.DamageReduceStat.Value / 100f;
		var cappedByShield = false;
		if (options.TargetIsPlayer && options.HasShield)
		{
			var reduceMax = options.ShieldReduceMax!.Value;
			if (reduceMax > 0 && reduceMax < reduceValue)
			{
				reduceValue = reduceMax;
				cappedByShield = true;
			}
		}

		var exactDamage = damage - reduceValue;
		return WorldNpcSkillBlockedDamageResult.AppliedResult(
			damage,
			(int)exactDamage,
			exactDamage,
			status,
			options,
			reduceValue,
			cappedByShield);
	}

	private static WorldNpcSkillShieldObserverResult CalculateShieldObserver(
		bool ignoreShield,
		WorldNpcSkillShieldObserverOptions? options,
		WorldNpcSkillAttackResult attack)
	{
		if (ignoreShield)
			return WorldNpcSkillShieldObserverResult.FromIgnoreShield(attack);

		if (options == null)
			return WorldNpcSkillShieldObserverResult.NotRequested(attack);

		// Java parity: controllers/ObserveController.checkShieldStatus and observer/AttackShieldObserver.checkShield.
		var baseStatus = attack.AttackStatus.GetBaseStatus();
		if (baseStatus is WorldNpcSkillAttackStatus.Dodge or WorldNpcSkillAttackStatus.Resist)
			return WorldNpcSkillShieldObserverResult.FromCounterStatus(attack);

		if (!options.ObserverOutputsKnown)
			return WorldNpcSkillShieldObserverResult.Unresolved(attack);

		var outputs = options.Outputs ?? Array.Empty<WorldNpcSkillShieldObserverOutput>();
		if (outputs.Count == 0)
			return WorldNpcSkillShieldObserverResult.CheckedWithoutMutation(attack);

		var damage = attack.Damage;
		var shieldType = attack.ShieldType;
		var reflectedDamage = attack.ReflectedDamage;
		var reflectedSkillId = attack.ReflectedSkillId;
		var protectedSkillId = attack.ProtectedSkillId;
		var protectedDamage = attack.ProtectedDamage;
		var protectorId = attack.ProtectorId;
		var mpAbsorbed = attack.MpAbsorbed;
		var mpShieldSkillId = attack.MpShieldSkillId;
		var launchSubEffect = attack.LaunchSubEffect;
		var outputResults = new List<WorldNpcSkillShieldObserverOutputResult>(outputs.Count);

		for (var i = 0; i < outputs.Count; i++)
		{
			var output = outputs[i];
			var beforeDamage = damage;
			shieldType |= output.ShieldType.GetJavaId();
			if (output.FinalDamage != null)
				damage = output.FinalDamage.Value;
			if (output.ReflectedDamage != null)
				reflectedDamage = output.ReflectedDamage.Value;
			if (output.ReflectedSkillId != null)
				reflectedSkillId = output.ReflectedSkillId.Value;
			if (output.ProtectedSkillId != null)
				protectedSkillId = output.ProtectedSkillId.Value;
			if (output.ProtectedDamage != null)
				protectedDamage = output.ProtectedDamage.Value;
			if (output.ProtectorId != null)
				protectorId = output.ProtectorId.Value;
			if (output.MpAbsorbed != null)
				mpAbsorbed = output.MpAbsorbed.Value;
			if (output.MpShieldSkillId != null)
				mpShieldSkillId = output.MpShieldSkillId.Value;
			if (output.LaunchSubEffect != null)
				launchSubEffect = output.LaunchSubEffect.Value;

			outputResults.Add(new WorldNpcSkillShieldObserverOutputResult(
				i,
				output.ShieldType,
				beforeDamage,
				damage,
				shieldType,
				output.EndsShieldEffect,
				output.SchedulesReflectedAttack,
				output.ForcesSkillReflection));
		}

		return WorldNpcSkillShieldObserverResult.AppliedResult(
			attack,
			damage,
			shieldType,
			reflectedDamage,
			reflectedSkillId,
			protectedSkillId,
			protectedDamage,
			protectorId,
			mpAbsorbed,
			mpShieldSkillId,
			launchSubEffect,
			outputResults);
	}

	private readonly record struct RandomMultiplierResult(float Multiplier, WorldNpcSkillResultCalculationStatus Status);
}

public sealed record WorldNpcSkillResultCalculationRequest(
	int InputDamage,
	bool ShouldApplyAttackerMovementModifier,
	bool IgnoreShield,
	bool SendResult,
	bool ShouldIncreaseByOneTimeBoost,
	bool UsesTemplateDamage,
	WorldNpcSkillResultCalculationOptions? Options = null);

public sealed record WorldNpcSkillResultCalculationOptions(
	int RandomDamageType = 0,
	int? RandomRoll = null,
	double? RandomChanceRoll = null,
	bool CannotMiss = false,
	WorldNpcSkillBaseDamageMultiplierOptions? BaseDamageMultiplier = null,
	WorldNpcSkillCriticalDamageOptions? CriticalDamage = null,
	WorldNpcSkillBlockedDamageOptions? BlockedDamage = null,
	WorldNpcSkillAttackStatusCalculationOptions? AttackStatusCalculation = null,
	WorldNpcSkillDamageModifierOptions? DamageModifier = null,
	WorldNpcSkillAdditionalHitOptions? AdditionalHits = null,
	WorldNpcSkillNpcAiDamageModifierOptions? NpcAiDamageModifier = null,
	WorldNpcSkillShieldObserverOptions? ShieldObserver = null,
	WorldNpcSkillAttackStatus AttackStatus = WorldNpcSkillAttackStatus.NormalHit,
	WorldNpcSkillHitType HitType = WorldNpcSkillHitType.PhysicalHit,
	int EffectPosition = 0,
	WorldNpcEffectResourceType ResourceType = WorldNpcEffectResourceType.Hp,
	bool IsDamage = true)
{
	public static WorldNpcSkillResultCalculationOptions Default { get; } = new();
}

public sealed record WorldNpcSkillResultCalculationResult(
	int InputDamage,
	int FinalDamage,
	int RandomDamageType,
	float RandomDamageMultiplier,
	WorldNpcSkillResultCalculationStatus Status,
	bool CannotMiss,
	bool CanDodgeOrResist,
	bool ShouldApplyAttackerMovementModifier,
	bool IgnoreShield,
	bool SendResult,
	bool ShouldIncreaseByOneTimeBoost,
	bool UsesTemplateDamage,
	WorldNpcSkillBaseDamageMultiplierResult BaseDamageMultiplier,
	WorldNpcSkillCriticalDamageResult CriticalDamage,
	WorldNpcSkillBlockedDamageResult BlockedDamage,
	WorldNpcSkillAttackStatusCalculationResult AttackStatusCalculation,
	WorldNpcSkillDamageModifierResult DamageModifier,
	WorldNpcSkillNpcAiDamageModifierResult NpcAiDamageModifier,
	WorldNpcSkillShieldObserverResult ShieldObserver,
	WorldNpcSkillAttackResult AttackResult,
	WorldNpcSkillAdditionalHitResult AdditionalHits,
	WorldNpcSkillEffectReservedResult EffectReserved);

public sealed record WorldNpcSkillAttackStatusCalculationOptions(
	WorldNpcSkillAttackStatusCalculationKind Kind,
	bool IsMainHand = true,
	int AccuracyModifier = 0,
	int CriticalProbability = 100,
	bool IsSkill = true,
	bool CannotMiss = false,
	bool ApplyMagicalCritical = true,
	bool TargetIsPlayer = false,
	bool TargetHasShield = false,
	bool? DodgeResult = null,
	bool? BlockResult = null,
	bool? ParryResult = null,
	bool? MagicalResistResult = null,
	bool? CriticalResult = null);

public sealed record WorldNpcSkillAttackStatusCalculationResult(
	WorldNpcSkillAttackStatusCalculationKind Kind,
	WorldNpcSkillAttackStatus InitialStatus,
	WorldNpcSkillAttackStatus BaseStatus,
	WorldNpcSkillAttackStatus FinalStatus,
	bool IsMainHand,
	int AccuracyModifier,
	int CriticalProbability,
	bool IsSkill,
	bool CannotMiss,
	bool ApplyMagicalCritical,
	bool TargetIsPlayer,
	bool TargetHasShield,
	bool DodgeChecked,
	bool BlockChecked,
	bool ParryChecked,
	bool MagicalResistChecked,
	bool CriticalChecked,
	bool DodgeInputMissing,
	bool BlockInputMissing,
	bool ParryInputMissing,
	bool MagicalResistInputMissing,
	bool CriticalInputMissing,
	bool CriticalUpgraded,
	bool OffHandConverted,
	bool ProbeOnly)
{
	public bool WasRequested => Kind != WorldNpcSkillAttackStatusCalculationKind.NotRequested;

	public bool HasUnresolvedProbabilityInputs =>
		DodgeInputMissing ||
		BlockInputMissing ||
		ParryInputMissing ||
		MagicalResistInputMissing ||
		CriticalInputMissing;

	public static WorldNpcSkillAttackStatusCalculationResult NotRequested(WorldNpcSkillAttackStatus fallbackStatus)
	{
		return new WorldNpcSkillAttackStatusCalculationResult(
			WorldNpcSkillAttackStatusCalculationKind.NotRequested,
			fallbackStatus,
			fallbackStatus.GetBaseStatus(),
			fallbackStatus,
			IsMainHand: true,
			AccuracyModifier: 0,
			CriticalProbability: 100,
			IsSkill: true,
			CannotMiss: false,
			ApplyMagicalCritical: true,
			TargetIsPlayer: false,
			TargetHasShield: false,
			DodgeChecked: false,
			BlockChecked: false,
			ParryChecked: false,
			MagicalResistChecked: false,
			CriticalChecked: false,
			DodgeInputMissing: false,
			BlockInputMissing: false,
			ParryInputMissing: false,
			MagicalResistInputMissing: false,
			CriticalInputMissing: false,
			CriticalUpgraded: false,
			OffHandConverted: false,
			ProbeOnly: false);
	}
}

public sealed record WorldNpcSkillDamageModifierOptions(
	WorldNpcSkillDamageModifierElement Element = WorldNpcSkillDamageModifierElement.Physical,
	float? Defense = null,
	float AttackerMovementMultiplier = 1f,
	float? PvpPveMultiplier = null,
	bool TargetIsPlayer = false,
	float? BlockReduceRatio = null,
	int? BlockReduceMax = null,
	WorldNpcSkillWeaponGroup? MainHandWeaponGroup = null,
	WorldNpcSkillWeaponGroup? OffHandWeaponGroup = null,
	int CriticalDamageReduce = 0);

public sealed record WorldNpcSkillBaseDamageMultiplierOptions(
	WorldNpcSkillBaseDamageMultiplierKind Kind,
	bool IsSkill = true,
	bool ObserverMultipliersKnown = true,
	IReadOnlyList<float>? ObserverMultipliers = null);

public sealed record WorldNpcSkillCriticalDamageOptions(
	WorldNpcSkillDamageModifierElement Element = WorldNpcSkillDamageModifierElement.Physical,
	WorldNpcSkillWeaponGroup? WeaponGroup = null,
	int CriticalAddDamage = 0,
	bool TargetIsPlayer = false,
	bool AppliesFortitudeStat = true,
	int? CriticalDamageReduce = null,
	bool IsMainHand = true);

public sealed record WorldNpcSkillCriticalDamageResult(
	bool WasRequested,
	bool Applied,
	bool SkippedByStatus,
	WorldNpcSkillAttackStatus AttackStatus,
	WorldNpcSkillDamageModifierElement Element,
	WorldNpcSkillWeaponGroup? WeaponGroup,
	int CriticalAddDamage,
	bool TargetIsPlayer,
	bool AppliesFortitudeStat,
	int? CriticalDamageReduce,
	bool IsMainHand,
	int OriginalDamage,
	int FinalDamage,
	float ExactFinalDamage,
	float Coefficient,
	bool CriticalDamageReduceInputMissing)
{
	public bool HasUnresolvedInputs => CriticalDamageReduceInputMissing;

	public static WorldNpcSkillCriticalDamageResult NotRequested(int damage, WorldNpcSkillAttackStatus status)
	{
		return Create(
			WasRequested: false,
			Applied: false,
			SkippedByStatus: false,
			status,
			WorldNpcSkillCriticalDamageOptionsDefaults.Default,
			damage,
			damage,
			damage,
			Coefficient: 1f,
			CriticalDamageReduceInputMissing: false);
	}

	public static WorldNpcSkillCriticalDamageResult FromSkippedStatus(int damage, WorldNpcSkillAttackStatus status)
	{
		return Create(
			WasRequested: true,
			Applied: false,
			SkippedByStatus: true,
			status,
			WorldNpcSkillCriticalDamageOptionsDefaults.Default,
			damage,
			damage,
			damage,
			Coefficient: 1f,
			CriticalDamageReduceInputMissing: false);
	}

	public static WorldNpcSkillCriticalDamageResult Unresolved(
		int damage,
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillCriticalDamageOptions options)
	{
		return Create(
			WasRequested: true,
			Applied: false,
			SkippedByStatus: false,
			status,
			options,
			damage,
			damage,
			damage,
			Coefficient: 1f,
			CriticalDamageReduceInputMissing: true);
	}

	public static WorldNpcSkillCriticalDamageResult AppliedResult(
		int originalDamage,
		int finalDamage,
		float exactFinalDamage,
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillCriticalDamageOptions options,
		float coefficient)
	{
		return Create(
			WasRequested: true,
			Applied: true,
			SkippedByStatus: false,
			status,
			options,
			originalDamage,
			finalDamage,
			exactFinalDamage,
			coefficient,
			CriticalDamageReduceInputMissing: false);
	}

	private static WorldNpcSkillCriticalDamageResult Create(
		bool WasRequested,
		bool Applied,
		bool SkippedByStatus,
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillCriticalDamageOptions options,
		int originalDamage,
		int finalDamage,
		float exactFinalDamage,
		float Coefficient,
		bool CriticalDamageReduceInputMissing)
	{
		return new WorldNpcSkillCriticalDamageResult(
			WasRequested,
			Applied,
			SkippedByStatus,
			status,
			options.Element,
			options.WeaponGroup,
			options.CriticalAddDamage,
			options.TargetIsPlayer,
			options.AppliesFortitudeStat,
			options.CriticalDamageReduce,
			options.IsMainHand,
			originalDamage,
			finalDamage,
			exactFinalDamage,
			Coefficient,
			CriticalDamageReduceInputMissing);
	}
}

internal static class WorldNpcSkillCriticalDamageOptionsDefaults
{
	public static WorldNpcSkillCriticalDamageOptions Default { get; } = new();
}

public sealed record WorldNpcSkillBlockedDamageOptions(
	bool TargetIsPlayer = false,
	bool HasShield = false,
	float? DamageReduceStat = null,
	int? ShieldReduceMax = null);

public sealed record WorldNpcSkillBlockedDamageResult(
	bool WasRequested,
	bool Applied,
	bool SkippedByStatus,
	WorldNpcSkillAttackStatus AttackStatus,
	int OriginalDamage,
	int FinalDamage,
	float ExactFinalDamage,
	bool TargetIsPlayer,
	bool HasShield,
	float? DamageReduceStat,
	int? ShieldReduceMax,
	float ReduceValue,
	bool CappedByShield,
	bool DamageReduceStatInputMissing,
	bool ShieldReduceMaxInputMissing)
{
	public bool HasUnresolvedInputs => DamageReduceStatInputMissing || ShieldReduceMaxInputMissing;

	public static WorldNpcSkillBlockedDamageResult NotRequested(int damage, WorldNpcSkillAttackStatus status)
	{
		return Create(
			WasRequested: false,
			Applied: false,
			SkippedByStatus: false,
			status,
			WorldNpcSkillBlockedDamageOptionsDefaults.Default,
			damage,
			damage,
			damage,
			ReduceValue: 0f,
			CappedByShield: false,
			DamageReduceStatInputMissing: false,
			ShieldReduceMaxInputMissing: false);
	}

	public static WorldNpcSkillBlockedDamageResult FromSkippedStatus(int damage, WorldNpcSkillAttackStatus status)
	{
		return Create(
			WasRequested: true,
			Applied: false,
			SkippedByStatus: true,
			status,
			WorldNpcSkillBlockedDamageOptionsDefaults.Default,
			damage,
			damage,
			damage,
			ReduceValue: 0f,
			CappedByShield: false,
			DamageReduceStatInputMissing: false,
			ShieldReduceMaxInputMissing: false);
	}

	public static WorldNpcSkillBlockedDamageResult Unresolved(
		int damage,
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillBlockedDamageOptions options,
		bool shieldReduceMaxMissing)
	{
		return Create(
			WasRequested: true,
			Applied: false,
			SkippedByStatus: false,
			status,
			options,
			damage,
			damage,
			damage,
			ReduceValue: 0f,
			CappedByShield: false,
			DamageReduceStatInputMissing: options.DamageReduceStat == null,
			ShieldReduceMaxInputMissing: shieldReduceMaxMissing);
	}

	public static WorldNpcSkillBlockedDamageResult AppliedResult(
		int originalDamage,
		int finalDamage,
		float exactFinalDamage,
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillBlockedDamageOptions options,
		float reduceValue,
		bool cappedByShield)
	{
		return Create(
			WasRequested: true,
			Applied: true,
			SkippedByStatus: false,
			status,
			options,
			originalDamage,
			finalDamage,
			exactFinalDamage,
			reduceValue,
			cappedByShield,
			DamageReduceStatInputMissing: false,
			ShieldReduceMaxInputMissing: false);
	}

	private static WorldNpcSkillBlockedDamageResult Create(
		bool WasRequested,
		bool Applied,
		bool SkippedByStatus,
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillBlockedDamageOptions options,
		int originalDamage,
		int finalDamage,
		float exactFinalDamage,
		float ReduceValue,
		bool CappedByShield,
		bool DamageReduceStatInputMissing,
		bool ShieldReduceMaxInputMissing)
	{
		return new WorldNpcSkillBlockedDamageResult(
			WasRequested,
			Applied,
			SkippedByStatus,
			status,
			originalDamage,
			finalDamage,
			exactFinalDamage,
			options.TargetIsPlayer,
			options.HasShield,
			options.DamageReduceStat,
			options.ShieldReduceMax,
			ReduceValue,
			CappedByShield,
			DamageReduceStatInputMissing,
			ShieldReduceMaxInputMissing);
	}
}

internal static class WorldNpcSkillBlockedDamageOptionsDefaults
{
	public static WorldNpcSkillBlockedDamageOptions Default { get; } = new();
}

public sealed record WorldNpcSkillBaseDamageMultiplierResult(
	bool WasRequested,
	bool Applied,
	bool SkippedByOneTimeBoost,
	WorldNpcSkillBaseDamageMultiplierKind Kind,
	bool ShouldIncreaseByOneTimeBoost,
	bool IsSkill,
	int OriginalDamage,
	int FinalDamage,
	float ExactFinalDamage,
	float Multiplier,
	int ObserverMultiplierCount,
	bool ObserverMultipliersInputMissing)
{
	public bool HasUnresolvedInputs => ObserverMultipliersInputMissing;

	public static WorldNpcSkillBaseDamageMultiplierResult NotRequested(int damage)
	{
		return new WorldNpcSkillBaseDamageMultiplierResult(
			WasRequested: false,
			Applied: false,
			SkippedByOneTimeBoost: false,
			Kind: WorldNpcSkillBaseDamageMultiplierKind.Physical,
			ShouldIncreaseByOneTimeBoost: true,
			IsSkill: true,
			OriginalDamage: damage,
			FinalDamage: damage,
			ExactFinalDamage: damage,
			Multiplier: 1f,
			ObserverMultiplierCount: 0,
			ObserverMultipliersInputMissing: false);
	}

	public static WorldNpcSkillBaseDamageMultiplierResult Skipped(
		int damage,
		WorldNpcSkillBaseDamageMultiplierKind kind,
		bool shouldIncreaseByOneTimeBoost)
	{
		return new WorldNpcSkillBaseDamageMultiplierResult(
			WasRequested: true,
			Applied: false,
			SkippedByOneTimeBoost: true,
			Kind: kind,
			ShouldIncreaseByOneTimeBoost: shouldIncreaseByOneTimeBoost,
			IsSkill: true,
			OriginalDamage: damage,
			FinalDamage: damage,
			ExactFinalDamage: damage,
			Multiplier: 1f,
			ObserverMultiplierCount: 0,
			ObserverMultipliersInputMissing: false);
	}

	public static WorldNpcSkillBaseDamageMultiplierResult Unresolved(
		int damage,
		WorldNpcSkillBaseDamageMultiplierKind kind,
		bool shouldIncreaseByOneTimeBoost,
		bool isSkill)
	{
		return new WorldNpcSkillBaseDamageMultiplierResult(
			WasRequested: true,
			Applied: false,
			SkippedByOneTimeBoost: false,
			Kind: kind,
			ShouldIncreaseByOneTimeBoost: shouldIncreaseByOneTimeBoost,
			IsSkill: isSkill,
			OriginalDamage: damage,
			FinalDamage: damage,
			ExactFinalDamage: damage,
			Multiplier: 1f,
			ObserverMultiplierCount: 0,
			ObserverMultipliersInputMissing: true);
	}

	public static WorldNpcSkillBaseDamageMultiplierResult AppliedResult(
		int originalDamage,
		int finalDamage,
		float exactFinalDamage,
		float multiplier,
		WorldNpcSkillBaseDamageMultiplierKind kind,
		bool shouldIncreaseByOneTimeBoost,
		bool isSkill,
		int observerMultiplierCount)
	{
		return new WorldNpcSkillBaseDamageMultiplierResult(
			WasRequested: true,
			Applied: true,
			SkippedByOneTimeBoost: false,
			Kind: kind,
			ShouldIncreaseByOneTimeBoost: shouldIncreaseByOneTimeBoost,
			IsSkill: isSkill,
			OriginalDamage: originalDamage,
			FinalDamage: finalDamage,
			ExactFinalDamage: exactFinalDamage,
			Multiplier: multiplier,
			ObserverMultiplierCount: observerMultiplierCount,
			ObserverMultipliersInputMissing: false);
	}
}

public sealed record WorldNpcSkillAdditionalHitOptions(
	bool AttackerIsPlayer = false,
	bool HasMainHandWeapon = false,
	int MainHandWeaponHitCount = 0,
	int? MainHandRoll = null,
	bool HasOffHandAttackResult = false,
	bool HasOffHandWeapon = false,
	bool OffHandIsShield = false,
	int OffHandWeaponHitCount = 0,
	int? OffHandRoll = null,
	int? OffHandDamage = null,
	WorldNpcSkillHitType OffHandHitType = WorldNpcSkillHitType.PhysicalHit);

public sealed record WorldNpcSkillAdditionalHitAttackResult(
	int Damage,
	WorldNpcSkillAttackStatus AttackStatus,
	WorldNpcSkillHitType HitType,
	bool IsOffHand);

public sealed record WorldNpcSkillAdditionalHitResult(
	bool WasRequested,
	bool Eligible,
	bool SkippedByStatus,
	bool AttackerIsPlayer,
	bool HasMainHandWeapon,
	bool HasOffHandAttack,
	int MainHandAdditionalHitCount,
	int OffHandAdditionalHitCount,
	int AmplificationLoopCount,
	IReadOnlyList<WorldNpcSkillAdditionalHitAttackResult> GeneratedHits,
	bool MainHandRollMissing,
	bool OffHandRollMissing,
	bool OffHandDamageMissing)
{
	public bool HasUnresolvedInputs => MainHandRollMissing || OffHandRollMissing || OffHandDamageMissing;

	public static WorldNpcSkillAdditionalHitResult NotRequested()
	{
		return new WorldNpcSkillAdditionalHitResult(
			WasRequested: false,
			Eligible: false,
			SkippedByStatus: false,
			AttackerIsPlayer: false,
			HasMainHandWeapon: false,
			HasOffHandAttack: false,
			MainHandAdditionalHitCount: 0,
			OffHandAdditionalHitCount: 0,
			AmplificationLoopCount: 0,
			GeneratedHits: Array.Empty<WorldNpcSkillAdditionalHitAttackResult>(),
			MainHandRollMissing: false,
			OffHandRollMissing: false,
			OffHandDamageMissing: false);
	}

	public static WorldNpcSkillAdditionalHitResult Skipped(bool attackerIsPlayer, bool skippedByStatus)
	{
		return new WorldNpcSkillAdditionalHitResult(
			WasRequested: true,
			Eligible: false,
			SkippedByStatus: skippedByStatus,
			AttackerIsPlayer: attackerIsPlayer,
			HasMainHandWeapon: false,
			HasOffHandAttack: false,
			MainHandAdditionalHitCount: 0,
			OffHandAdditionalHitCount: 0,
			AmplificationLoopCount: 0,
			GeneratedHits: Array.Empty<WorldNpcSkillAdditionalHitAttackResult>(),
			MainHandRollMissing: false,
			OffHandRollMissing: false,
			OffHandDamageMissing: false);
	}

	public static WorldNpcSkillAdditionalHitResult Unresolved(
		bool attackerIsPlayer,
		bool hasMainHandWeapon,
		bool hasOffHandAttack,
		bool mainHandRollMissing,
		bool offHandRollMissing,
		bool offHandDamageMissing)
	{
		return new WorldNpcSkillAdditionalHitResult(
			WasRequested: true,
			Eligible: true,
			SkippedByStatus: false,
			AttackerIsPlayer: attackerIsPlayer,
			HasMainHandWeapon: hasMainHandWeapon,
			HasOffHandAttack: hasOffHandAttack,
			MainHandAdditionalHitCount: 0,
			OffHandAdditionalHitCount: 0,
			AmplificationLoopCount: 0,
			GeneratedHits: Array.Empty<WorldNpcSkillAdditionalHitAttackResult>(),
			MainHandRollMissing: mainHandRollMissing,
			OffHandRollMissing: offHandRollMissing,
			OffHandDamageMissing: offHandDamageMissing);
	}

	public static WorldNpcSkillAdditionalHitResult AppliedResult(
		bool attackerIsPlayer,
		bool hasMainHandWeapon,
		bool hasOffHandAttack,
		int mainHandAdditionalHitCount,
		int offHandAdditionalHitCount,
		int amplificationLoopCount,
		IReadOnlyList<WorldNpcSkillAdditionalHitAttackResult> generatedHits)
	{
		return new WorldNpcSkillAdditionalHitResult(
			WasRequested: true,
			Eligible: true,
			SkippedByStatus: false,
			AttackerIsPlayer: attackerIsPlayer,
			HasMainHandWeapon: hasMainHandWeapon,
			HasOffHandAttack: hasOffHandAttack,
			MainHandAdditionalHitCount: mainHandAdditionalHitCount,
			OffHandAdditionalHitCount: offHandAdditionalHitCount,
			AmplificationLoopCount: amplificationLoopCount,
			GeneratedHits: generatedHits,
			MainHandRollMissing: false,
			OffHandRollMissing: false,
			OffHandDamageMissing: false);
	}
}

public sealed record WorldNpcSkillNpcAiDamageModifierOptions(
	bool AttackerIsNpc = false,
	bool AttackedIsNpc = false,
	float? AttackerNpcOwnerDamageMultiplier = null,
	float? AttackedNpcDamageMultiplier = null);

public sealed record WorldNpcSkillNpcAiDamageModifierHitResult(
	int Index,
	int OriginalDamage,
	int FinalDamage,
	float ExactFinalDamage,
	WorldNpcSkillAttackStatus AttackStatus,
	WorldNpcSkillHitType HitType,
	bool IsOffHand);

public sealed record WorldNpcSkillNpcAiDamageModifierResult(
	bool WasRequested,
	bool Applied,
	bool HasNpcParticipant,
	bool AttackerIsNpc,
	bool AttackedIsNpc,
	int PrimaryOriginalDamage,
	int PrimaryFinalDamage,
	float PrimaryExactFinalDamage,
	float? AttackerNpcOwnerDamageMultiplier,
	float? AttackedNpcDamageMultiplier,
	IReadOnlyList<WorldNpcSkillNpcAiDamageModifierHitResult> AdditionalHits,
	bool AttackerNpcOwnerHookMissing,
	bool AttackedNpcHookMissing)
{
	public bool HasUnresolvedInputs => AttackerNpcOwnerHookMissing || AttackedNpcHookMissing;

	public static WorldNpcSkillNpcAiDamageModifierResult NotRequested(int primaryDamage)
	{
		return new WorldNpcSkillNpcAiDamageModifierResult(
			WasRequested: false,
			Applied: false,
			HasNpcParticipant: false,
			AttackerIsNpc: false,
			AttackedIsNpc: false,
			PrimaryOriginalDamage: primaryDamage,
			PrimaryFinalDamage: primaryDamage,
			PrimaryExactFinalDamage: primaryDamage,
			AttackerNpcOwnerDamageMultiplier: null,
			AttackedNpcDamageMultiplier: null,
			AdditionalHits: Array.Empty<WorldNpcSkillNpcAiDamageModifierHitResult>(),
			AttackerNpcOwnerHookMissing: false,
			AttackedNpcHookMissing: false);
	}

	public static WorldNpcSkillNpcAiDamageModifierResult Skipped(int primaryDamage, bool attackerIsNpc, bool attackedIsNpc)
	{
		return new WorldNpcSkillNpcAiDamageModifierResult(
			WasRequested: true,
			Applied: false,
			HasNpcParticipant: false,
			AttackerIsNpc: attackerIsNpc,
			AttackedIsNpc: attackedIsNpc,
			PrimaryOriginalDamage: primaryDamage,
			PrimaryFinalDamage: primaryDamage,
			PrimaryExactFinalDamage: primaryDamage,
			AttackerNpcOwnerDamageMultiplier: null,
			AttackedNpcDamageMultiplier: null,
			AdditionalHits: Array.Empty<WorldNpcSkillNpcAiDamageModifierHitResult>(),
			AttackerNpcOwnerHookMissing: false,
			AttackedNpcHookMissing: false);
	}

	public static WorldNpcSkillNpcAiDamageModifierResult Unresolved(
		int primaryDamage,
		bool attackerIsNpc,
		bool attackedIsNpc,
		bool attackerNpcOwnerHookMissing,
		bool attackedNpcHookMissing)
	{
		return new WorldNpcSkillNpcAiDamageModifierResult(
			WasRequested: true,
			Applied: false,
			HasNpcParticipant: true,
			AttackerIsNpc: attackerIsNpc,
			AttackedIsNpc: attackedIsNpc,
			PrimaryOriginalDamage: primaryDamage,
			PrimaryFinalDamage: primaryDamage,
			PrimaryExactFinalDamage: primaryDamage,
			AttackerNpcOwnerDamageMultiplier: null,
			AttackedNpcDamageMultiplier: null,
			AdditionalHits: Array.Empty<WorldNpcSkillNpcAiDamageModifierHitResult>(),
			AttackerNpcOwnerHookMissing: attackerNpcOwnerHookMissing,
			AttackedNpcHookMissing: attackedNpcHookMissing);
	}

	public static WorldNpcSkillNpcAiDamageModifierResult AppliedResult(
		int primaryOriginalDamage,
		int primaryFinalDamage,
		float primaryExactFinalDamage,
		bool attackerIsNpc,
		bool attackedIsNpc,
		float? attackerNpcOwnerDamageMultiplier,
		float? attackedNpcDamageMultiplier,
		IReadOnlyList<WorldNpcSkillNpcAiDamageModifierHitResult> additionalHits)
	{
		return new WorldNpcSkillNpcAiDamageModifierResult(
			WasRequested: true,
			Applied: true,
			HasNpcParticipant: true,
			AttackerIsNpc: attackerIsNpc,
			AttackedIsNpc: attackedIsNpc,
			PrimaryOriginalDamage: primaryOriginalDamage,
			PrimaryFinalDamage: primaryFinalDamage,
			PrimaryExactFinalDamage: primaryExactFinalDamage,
			AttackerNpcOwnerDamageMultiplier: attackerNpcOwnerDamageMultiplier,
			AttackedNpcDamageMultiplier: attackedNpcDamageMultiplier,
			AdditionalHits: additionalHits,
			AttackerNpcOwnerHookMissing: false,
			AttackedNpcHookMissing: false);
	}
}

public sealed record WorldNpcSkillShieldObserverOptions(
	bool ObserverOutputsKnown = true,
	IReadOnlyList<WorldNpcSkillShieldObserverOutput>? Outputs = null);

public sealed record WorldNpcSkillShieldObserverOutput(
	WorldNpcSkillShieldType ShieldType,
	int? FinalDamage = null,
	int? ReflectedDamage = null,
	int? ReflectedSkillId = null,
	int? ProtectedSkillId = null,
	int? ProtectedDamage = null,
	int? ProtectorId = null,
	int? MpAbsorbed = null,
	int? MpShieldSkillId = null,
	bool? LaunchSubEffect = null,
	bool EndsShieldEffect = false,
	bool SchedulesReflectedAttack = false,
	bool ForcesSkillReflection = false);

public sealed record WorldNpcSkillShieldObserverOutputResult(
	int Index,
	WorldNpcSkillShieldType ShieldType,
	int DamageBefore,
	int DamageAfter,
	int ShieldTypeAfter,
	bool EndsShieldEffect,
	bool SchedulesReflectedAttack,
	bool ForcesSkillReflection);

public sealed record WorldNpcSkillShieldObserverResult(
	bool WasChecked,
	bool WasRequested,
	bool Applied,
	bool SkippedByIgnoreShield,
	bool SkippedByCounterStatus,
	bool ObserverOutputInputMissing,
	int OriginalDamage,
	int FinalDamage,
	int ShieldType,
	int ReflectedDamage,
	int ReflectedSkillId,
	int ProtectedSkillId,
	int ProtectedDamage,
	int ProtectorId,
	int MpAbsorbed,
	int MpShieldSkillId,
	bool LaunchSubEffect,
	IReadOnlyList<WorldNpcSkillShieldObserverOutputResult> Outputs)
{
	public bool HasUnresolvedInputs => ObserverOutputInputMissing;

	public static WorldNpcSkillShieldObserverResult NotRequested(WorldNpcSkillAttackResult attack)
	{
		return Create(
			WasChecked: true,
			WasRequested: false,
			Applied: false,
			SkippedByIgnoreShield: false,
			SkippedByCounterStatus: false,
			ObserverOutputInputMissing: false,
			attack,
			Outputs: Array.Empty<WorldNpcSkillShieldObserverOutputResult>());
	}

	public static WorldNpcSkillShieldObserverResult FromIgnoreShield(WorldNpcSkillAttackResult attack)
	{
		return Create(
			WasChecked: false,
			WasRequested: false,
			Applied: false,
			SkippedByIgnoreShield: true,
			SkippedByCounterStatus: false,
			ObserverOutputInputMissing: false,
			attack,
			Outputs: Array.Empty<WorldNpcSkillShieldObserverOutputResult>());
	}

	public static WorldNpcSkillShieldObserverResult FromCounterStatus(WorldNpcSkillAttackResult attack)
	{
		return Create(
			WasChecked: true,
			WasRequested: true,
			Applied: false,
			SkippedByIgnoreShield: false,
			SkippedByCounterStatus: true,
			ObserverOutputInputMissing: false,
			attack,
			Outputs: Array.Empty<WorldNpcSkillShieldObserverOutputResult>());
	}

	public static WorldNpcSkillShieldObserverResult Unresolved(WorldNpcSkillAttackResult attack)
	{
		return Create(
			WasChecked: true,
			WasRequested: true,
			Applied: false,
			SkippedByIgnoreShield: false,
			SkippedByCounterStatus: false,
			ObserverOutputInputMissing: true,
			attack,
			Outputs: Array.Empty<WorldNpcSkillShieldObserverOutputResult>());
	}

	public static WorldNpcSkillShieldObserverResult CheckedWithoutMutation(WorldNpcSkillAttackResult attack)
	{
		return Create(
			WasChecked: true,
			WasRequested: true,
			Applied: false,
			SkippedByIgnoreShield: false,
			SkippedByCounterStatus: false,
			ObserverOutputInputMissing: false,
			attack,
			Outputs: Array.Empty<WorldNpcSkillShieldObserverOutputResult>());
	}

	public static WorldNpcSkillShieldObserverResult AppliedResult(
		WorldNpcSkillAttackResult attack,
		int finalDamage,
		int shieldType,
		int reflectedDamage,
		int reflectedSkillId,
		int protectedSkillId,
		int protectedDamage,
		int protectorId,
		int mpAbsorbed,
		int mpShieldSkillId,
		bool launchSubEffect,
		IReadOnlyList<WorldNpcSkillShieldObserverOutputResult> outputs)
	{
		return new WorldNpcSkillShieldObserverResult(
			WasChecked: true,
			WasRequested: true,
			Applied: true,
			SkippedByIgnoreShield: false,
			SkippedByCounterStatus: false,
			ObserverOutputInputMissing: false,
			OriginalDamage: attack.Damage,
			FinalDamage: finalDamage,
			ShieldType: shieldType,
			ReflectedDamage: reflectedDamage,
			ReflectedSkillId: reflectedSkillId,
			ProtectedSkillId: protectedSkillId,
			ProtectedDamage: protectedDamage,
			ProtectorId: protectorId,
			MpAbsorbed: mpAbsorbed,
			MpShieldSkillId: mpShieldSkillId,
			LaunchSubEffect: launchSubEffect,
			Outputs: outputs);
	}

	private static WorldNpcSkillShieldObserverResult Create(
		bool WasChecked,
		bool WasRequested,
		bool Applied,
		bool SkippedByIgnoreShield,
		bool SkippedByCounterStatus,
		bool ObserverOutputInputMissing,
		WorldNpcSkillAttackResult attack,
		IReadOnlyList<WorldNpcSkillShieldObserverOutputResult> Outputs)
	{
		return new WorldNpcSkillShieldObserverResult(
			WasChecked,
			WasRequested,
			Applied,
			SkippedByIgnoreShield,
			SkippedByCounterStatus,
			ObserverOutputInputMissing,
			OriginalDamage: attack.Damage,
			FinalDamage: attack.Damage,
			ShieldType: attack.ShieldType,
			ReflectedDamage: attack.ReflectedDamage,
			ReflectedSkillId: attack.ReflectedSkillId,
			ProtectedSkillId: attack.ProtectedSkillId,
			ProtectedDamage: attack.ProtectedDamage,
			ProtectorId: attack.ProtectorId,
			MpAbsorbed: attack.MpAbsorbed,
			MpShieldSkillId: attack.MpShieldSkillId,
			LaunchSubEffect: attack.LaunchSubEffect,
			Outputs: Outputs);
	}
}

public sealed record WorldNpcSkillDamageModifierResult(
	bool WasRequested,
	bool Applied,
	bool SkippedForCounterStatus,
	WorldNpcSkillAttackStatus AttackStatus,
	WorldNpcSkillAttackStatus BaseStatus,
	WorldNpcSkillDamageModifierElement Element,
	int OriginalDamage,
	int FinalDamage,
	float ExactFinalDamage,
	float MainMultiplier,
	float OffMultiplier,
	float? Defense,
	float AttackerMovementMultiplier,
	float? PvpPveMultiplier,
	float BlockReduceRatio,
	int BlockReduceMax,
	float BlockReduction,
	bool DefenseInputMissing,
	bool PvpPveInputMissing,
	bool BlockReductionInputMissing)
{
	public bool HasUnresolvedInputs => DefenseInputMissing || PvpPveInputMissing || BlockReductionInputMissing;

	public static WorldNpcSkillDamageModifierResult NotRequested(int damage, WorldNpcSkillAttackStatus status)
	{
		return Skipped(
			damage,
			status,
			status.GetBaseStatus(),
			WorldNpcSkillDamageModifierElement.Physical,
			SkippedForCounterStatus: false,
			WasRequested: false);
	}

	public static WorldNpcSkillDamageModifierResult Skipped(
		int damage,
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillAttackStatus baseStatus,
		WorldNpcSkillDamageModifierElement element,
		bool SkippedForCounterStatus,
		bool WasRequested = true)
	{
		return new WorldNpcSkillDamageModifierResult(
			WasRequested: WasRequested,
			Applied: false,
			SkippedForCounterStatus: SkippedForCounterStatus,
			AttackStatus: status,
			BaseStatus: baseStatus,
			Element: element,
			OriginalDamage: damage,
			FinalDamage: damage,
			ExactFinalDamage: damage,
			MainMultiplier: 1f,
			OffMultiplier: 1f,
			Defense: null,
			AttackerMovementMultiplier: 1f,
			PvpPveMultiplier: null,
			BlockReduceRatio: 0f,
			BlockReduceMax: int.MaxValue,
			BlockReduction: 0f,
			DefenseInputMissing: false,
			PvpPveInputMissing: false,
			BlockReductionInputMissing: false);
	}

	public static WorldNpcSkillDamageModifierResult Unresolved(
		int damage,
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillAttackStatus baseStatus,
		WorldNpcSkillDamageModifierElement element,
		float mainMultiplier,
		float offMultiplier,
		float blockReduceRatio,
		int blockReduceMax,
		bool defenseInputMissing,
		bool pvpPveInputMissing,
		bool blockReductionInputMissing)
	{
		return new WorldNpcSkillDamageModifierResult(
			WasRequested: true,
			Applied: false,
			SkippedForCounterStatus: false,
			AttackStatus: status,
			BaseStatus: baseStatus,
			Element: element,
			OriginalDamage: damage,
			FinalDamage: damage,
			ExactFinalDamage: damage,
			MainMultiplier: mainMultiplier,
			OffMultiplier: offMultiplier,
			Defense: null,
			AttackerMovementMultiplier: 1f,
			PvpPveMultiplier: null,
			BlockReduceRatio: blockReduceRatio,
			BlockReduceMax: blockReduceMax,
			BlockReduction: 0f,
			DefenseInputMissing: defenseInputMissing,
			PvpPveInputMissing: pvpPveInputMissing,
			BlockReductionInputMissing: blockReductionInputMissing);
	}

	public static WorldNpcSkillDamageModifierResult AppliedResult(
		int originalDamage,
		WorldNpcSkillAttackStatus status,
		WorldNpcSkillAttackStatus baseStatus,
		WorldNpcSkillDamageModifierElement element,
		int finalDamage,
		float exactFinalDamage,
		float mainMultiplier,
		float offMultiplier,
		float defense,
		float attackerMovementMultiplier,
		float pvpPveMultiplier,
		float blockReduceRatio,
		int blockReduceMax,
		float blockReduction)
	{
		return new WorldNpcSkillDamageModifierResult(
			WasRequested: true,
			Applied: true,
			SkippedForCounterStatus: false,
			AttackStatus: status,
			BaseStatus: baseStatus,
			Element: element,
			OriginalDamage: originalDamage,
			FinalDamage: finalDamage,
			ExactFinalDamage: exactFinalDamage,
			MainMultiplier: mainMultiplier,
			OffMultiplier: offMultiplier,
			Defense: defense,
			AttackerMovementMultiplier: attackerMovementMultiplier,
			PvpPveMultiplier: pvpPveMultiplier,
			BlockReduceRatio: blockReduceRatio,
			BlockReduceMax: blockReduceMax,
			BlockReduction: blockReduction,
			DefenseInputMissing: false,
			PvpPveInputMissing: false,
			BlockReductionInputMissing: false);
	}
}

public sealed record WorldNpcSkillAttackResult(
	int Damage,
	WorldNpcSkillAttackStatus AttackStatus,
	WorldNpcSkillHitType HitType,
	bool ShieldChecked,
	int ShieldType = 0,
	int ReflectedDamage = 0,
	int ReflectedSkillId = 0,
	int ProtectedSkillId = 0,
	int ProtectedDamage = 0,
	int ProtectorId = 0,
	int MpAbsorbed = 0,
	int MpShieldSkillId = 0,
	bool LaunchSubEffect = true);

public sealed record WorldNpcSkillEffectReservedResult(
	int Position,
	int Value,
	WorldNpcEffectResourceType Type,
	bool IsDamage,
	bool Send)
{
	public int ValueToSend => IsDamage ? Value : -Value;
}

public enum WorldNpcSkillResultCalculationStatus
{
	Calculated,
	RandomRollMissing,
	RandomChanceMissing,
}

public enum WorldNpcSkillAttackStatusCalculationKind
{
	NotRequested,
	Physical,
	Magical,
}

public enum WorldNpcSkillBaseDamageMultiplierKind
{
	Physical,
	Magical,
}

public enum WorldNpcSkillDamageModifierElement
{
	Physical,
	Magical,
}

public enum WorldNpcSkillShieldType
{
	Convert = 0,
	Reflector = 1,
	Normal = 2,
	Unknown = 4,
	Protect = 8,
	MpShield = 16,
	SkillReflector = 32,
}

public static class WorldNpcSkillShieldTypeExtensions
{
	public static int GetJavaId(this WorldNpcSkillShieldType shieldType)
	{
		// Java parity: skillengine/model/ShieldType.getId.
		return (int)shieldType;
	}
}

public enum WorldNpcSkillWeaponGroup
{
	Dagger,
	Sword,
	Mace,
	Greatsword,
	Polearm,
	Staff,
	Bow,
	Other,
}

public static class WorldNpcSkillWeaponGroupExtensions
{
	public static float GetJavaCriticalMultiplier(this WorldNpcSkillWeaponGroup group)
	{
		// Java parity: controllers/attack/AttackUtil.getWeaponMultiplier.
		return group switch
		{
			WorldNpcSkillWeaponGroup.Dagger => 2.3f,
			WorldNpcSkillWeaponGroup.Sword => 2.2f,
			WorldNpcSkillWeaponGroup.Mace => 2f,
			WorldNpcSkillWeaponGroup.Greatsword or WorldNpcSkillWeaponGroup.Polearm => 1.8f,
			WorldNpcSkillWeaponGroup.Staff or WorldNpcSkillWeaponGroup.Bow => 1.7f,
			_ => 1.5f,
		};
	}
}

public enum WorldNpcSkillAttackStatus
{
	Dodge = 0,
	OffHandDodge = 1,
	Parry = 2,
	OffHandParry = 3,
	Block = 4,
	OffHandBlock = 5,
	Resist = 6,
	OffHandResist = 7,
	Buf = 8,
	OffHandBuf = 9,
	NormalHit = 10,
	OffHandNormalHit = 11,
	CriticalDodge = -64,
	CriticalParry = -62,
	CriticalBlock = -60,
	CriticalResist = -58,
	Critical = -54,
	OffHandCriticalDodge = -47,
	OffHandCriticalParry = -45,
	OffHandCriticalBlock = -43,
	OffHandCriticalResist = -41,
	OffHandCritical = -37,
}

public static class WorldNpcSkillAttackStatusExtensions
{
	public static int GetJavaId(this WorldNpcSkillAttackStatus status)
	{
		return (int)status;
	}

	public static bool IsCounterSkill(this WorldNpcSkillAttackStatus status)
	{
		// Java parity: controllers/attack/AttackStatus.isCounterSkill.
		return status
			is WorldNpcSkillAttackStatus.Dodge
			or WorldNpcSkillAttackStatus.OffHandDodge
			or WorldNpcSkillAttackStatus.Parry
			or WorldNpcSkillAttackStatus.OffHandParry
			or WorldNpcSkillAttackStatus.Block
			or WorldNpcSkillAttackStatus.OffHandBlock
			or WorldNpcSkillAttackStatus.Resist
			or WorldNpcSkillAttackStatus.OffHandResist
			or WorldNpcSkillAttackStatus.CriticalDodge
			or WorldNpcSkillAttackStatus.CriticalParry
			or WorldNpcSkillAttackStatus.CriticalBlock
			or WorldNpcSkillAttackStatus.CriticalResist
			or WorldNpcSkillAttackStatus.OffHandCriticalDodge
			or WorldNpcSkillAttackStatus.OffHandCriticalParry
			or WorldNpcSkillAttackStatus.OffHandCriticalBlock
			or WorldNpcSkillAttackStatus.OffHandCriticalResist;
	}

	public static bool IsCritical(this WorldNpcSkillAttackStatus status)
	{
		// Java parity: controllers/attack/AttackStatus.isCritical.
		return status
			is WorldNpcSkillAttackStatus.CriticalDodge
			or WorldNpcSkillAttackStatus.CriticalParry
			or WorldNpcSkillAttackStatus.CriticalBlock
			or WorldNpcSkillAttackStatus.CriticalResist
			or WorldNpcSkillAttackStatus.Critical
			or WorldNpcSkillAttackStatus.OffHandCriticalDodge
			or WorldNpcSkillAttackStatus.OffHandCriticalParry
			or WorldNpcSkillAttackStatus.OffHandCriticalBlock
			or WorldNpcSkillAttackStatus.OffHandCriticalResist
			or WorldNpcSkillAttackStatus.OffHandCritical;
	}

	public static WorldNpcSkillAttackStatus GetOffHandStatus(this WorldNpcSkillAttackStatus status)
	{
		// Java parity: controllers/attack/AttackStatus.getOffHandStats.
		return status switch
		{
			WorldNpcSkillAttackStatus.Dodge => WorldNpcSkillAttackStatus.OffHandDodge,
			WorldNpcSkillAttackStatus.Parry => WorldNpcSkillAttackStatus.OffHandParry,
			WorldNpcSkillAttackStatus.Block => WorldNpcSkillAttackStatus.OffHandBlock,
			WorldNpcSkillAttackStatus.Resist => WorldNpcSkillAttackStatus.OffHandResist,
			WorldNpcSkillAttackStatus.Buf => WorldNpcSkillAttackStatus.OffHandBuf,
			WorldNpcSkillAttackStatus.NormalHit => WorldNpcSkillAttackStatus.OffHandNormalHit,
			WorldNpcSkillAttackStatus.Critical => WorldNpcSkillAttackStatus.OffHandCritical,
			WorldNpcSkillAttackStatus.CriticalDodge => WorldNpcSkillAttackStatus.OffHandCriticalDodge,
			WorldNpcSkillAttackStatus.CriticalParry => WorldNpcSkillAttackStatus.OffHandCriticalParry,
			WorldNpcSkillAttackStatus.CriticalBlock => WorldNpcSkillAttackStatus.OffHandCriticalBlock,
			WorldNpcSkillAttackStatus.CriticalResist => WorldNpcSkillAttackStatus.OffHandCriticalResist,
			_ => throw new ArgumentOutOfRangeException(nameof(status), status, "Invalid Java main-hand attack status."),
		};
	}

	public static WorldNpcSkillAttackStatus GetBaseStatus(this WorldNpcSkillAttackStatus status)
	{
		// Java parity: controllers/attack/AttackStatus.getBaseStatus.
		return status switch
		{
			WorldNpcSkillAttackStatus.Dodge
				or WorldNpcSkillAttackStatus.CriticalDodge
				or WorldNpcSkillAttackStatus.OffHandDodge
				or WorldNpcSkillAttackStatus.OffHandCriticalDodge => WorldNpcSkillAttackStatus.Dodge,
			WorldNpcSkillAttackStatus.Resist
				or WorldNpcSkillAttackStatus.CriticalResist
				or WorldNpcSkillAttackStatus.OffHandResist
				or WorldNpcSkillAttackStatus.OffHandCriticalResist => WorldNpcSkillAttackStatus.Resist,
			WorldNpcSkillAttackStatus.Parry
				or WorldNpcSkillAttackStatus.CriticalParry
				or WorldNpcSkillAttackStatus.OffHandParry
				or WorldNpcSkillAttackStatus.OffHandCriticalParry => WorldNpcSkillAttackStatus.Parry,
			WorldNpcSkillAttackStatus.Block
				or WorldNpcSkillAttackStatus.CriticalBlock
				or WorldNpcSkillAttackStatus.OffHandBlock
				or WorldNpcSkillAttackStatus.OffHandCriticalBlock => WorldNpcSkillAttackStatus.Block,
			_ => status,
		};
	}

	public static WorldNpcSkillAttackStatus GetCriticalStatusFor(this WorldNpcSkillAttackStatus status)
	{
		// Java parity: controllers/attack/AttackStatus.getCriticalStatusFor.
		return status switch
		{
			WorldNpcSkillAttackStatus.Dodge => WorldNpcSkillAttackStatus.CriticalDodge,
			WorldNpcSkillAttackStatus.OffHandDodge => WorldNpcSkillAttackStatus.OffHandCriticalDodge,
			WorldNpcSkillAttackStatus.Parry => WorldNpcSkillAttackStatus.CriticalParry,
			WorldNpcSkillAttackStatus.OffHandParry => WorldNpcSkillAttackStatus.OffHandCriticalParry,
			WorldNpcSkillAttackStatus.Block => WorldNpcSkillAttackStatus.CriticalBlock,
			WorldNpcSkillAttackStatus.OffHandBlock => WorldNpcSkillAttackStatus.OffHandCriticalBlock,
			WorldNpcSkillAttackStatus.NormalHit => WorldNpcSkillAttackStatus.Critical,
			WorldNpcSkillAttackStatus.OffHandNormalHit => WorldNpcSkillAttackStatus.OffHandCritical,
			_ => status,
		};
	}
}

public enum WorldNpcSkillHitType
{
	EveryHit,
	NormalAttack,
	MagicalHit,
	PhysicalHit,
	Fear,
	Skill,
	BackAttack,
}

public enum WorldNpcEffectResourceType
{
	Hp = 0,
	Mp = 1,
	Fp = 2,
	Dp = 3,
}
