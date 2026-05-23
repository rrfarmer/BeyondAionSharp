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
		var random = CalculateRandomMultiplier(options);
		var attackStatus = CalculateAttackStatus(options.AttackStatusCalculation, options.AttackStatus);
		var finalDamage = random.Status == WorldNpcSkillResultCalculationStatus.Calculated
			? (int)(inputDamage * random.Multiplier)
			: inputDamage;
		var normalizedFinalDamage = Math.Max(0, finalDamage);
		var damageModifier = CalculateDamageModifier(options.DamageModifier, attackStatus.FinalStatus, normalizedFinalDamage);
		var resultDamage = damageModifier.Applied ? damageModifier.FinalDamage : normalizedFinalDamage;
		var attackResult = new WorldNpcSkillAttackResult(
			resultDamage,
			attackStatus.FinalStatus,
			options.HitType,
			ShieldChecked: !request.IgnoreShield);
		var effectReserved = new WorldNpcSkillEffectReservedResult(
			options.EffectPosition,
			attackResult.Damage,
			options.ResourceType,
			options.IsDamage,
			request.SendResult);
		return new WorldNpcSkillResultCalculationResult(
			inputDamage,
			resultDamage,
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
			attackStatus,
			damageModifier,
			attackResult,
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
	WorldNpcSkillAttackStatusCalculationOptions? AttackStatusCalculation = null,
	WorldNpcSkillDamageModifierOptions? DamageModifier = null,
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
	WorldNpcSkillAttackStatusCalculationResult AttackStatusCalculation,
	WorldNpcSkillDamageModifierResult DamageModifier,
	WorldNpcSkillAttackResult AttackResult,
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

public enum WorldNpcSkillDamageModifierElement
{
	Physical,
	Magical,
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
