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
		return new WorldNpcSkillResultCalculationResult(
			inputDamage,
			Math.Max(0, finalDamage),
			options.RandomDamageType,
			random.Multiplier,
			random.Status,
			options.CannotMiss,
			canDodgeOrResist,
			request.ShouldApplyAttackerMovementModifier,
			request.IgnoreShield,
			request.SendResult,
			request.ShouldIncreaseByOneTimeBoost,
			request.UsesTemplateDamage);
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
	bool CannotMiss = false)
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
	bool UsesTemplateDamage);

public enum WorldNpcSkillResultCalculationStatus
{
	Calculated,
	RandomRollMissing,
	RandomChanceMissing,
}
