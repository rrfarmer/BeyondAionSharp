namespace Aion.GameServer.Services;

public sealed class WorldNpcSkillResultCalculationService
{
	public WorldNpcSkillResultCalculationResult Calculate(WorldNpcSkillResultCalculationRequest request)
	{
		// Java parity: controllers/attack/AttackUtil.calculateSkillResult stages the EffectReserved damage result.
		var options = request.Options ?? WorldNpcSkillResultCalculationOptions.Default;
		var inputDamage = Math.Max(0, request.InputDamage);
		var canDodgeOrResist = !options.CannotMiss;
		var random = CalculateRandomMultiplier(options);
		var finalDamage = random.Status == WorldNpcSkillResultCalculationStatus.Calculated
			? (int)(inputDamage * random.Multiplier)
			: inputDamage;
		var normalizedFinalDamage = Math.Max(0, finalDamage);
		var attackResult = new WorldNpcSkillAttackResult(
			normalizedFinalDamage,
			options.AttackStatus,
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
			normalizedFinalDamage,
			options.RandomDamageType,
			random.Multiplier,
			random.Status,
			options.CannotMiss,
			canDodgeOrResist,
			request.ShouldApplyAttackerMovementModifier,
			request.IgnoreShield,
			request.SendResult,
			request.ShouldIncreaseByOneTimeBoost,
			request.UsesTemplateDamage,
			attackResult,
			effectReserved);
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
	WorldNpcSkillAttackResult AttackResult,
	WorldNpcSkillEffectReservedResult EffectReserved);

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
