namespace Aion.GameServer.Services;

public sealed record ElementalDefenseDenominatorPlan(
	bool IsTargetPlayer,
	int EffectorLevel,
	int TargetLevel,
	int Denominator,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record ElementalDamageReductionPlan(
	float InputDamage,
	int AdjustedDefense,
	int Denominator,
	float ReductionFactor,
	float ReducedDamage,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ElementalDamageReductionPlanService
{
	// Java parity: StatCapUtil.getElementalDefenseBaseValue() = 1300.
	public const int ElementalDefenseBase = 1300;

	public static ElementalDefenseDenominatorPlan CreateDenominatorPlan(
		bool isTargetPlayer,
		int effectorLevel,
		int targetLevel)
	{
		// Java parity: StatFunctions.getElementalDefenseDenominator.
		// Player target: 1300 + max(0, min(effectorLevel, targetLevel) - 50) * 10
		// NPC target: 1300
		int denominator;
		if (isTargetPlayer)
		{
			var level = Math.Min(effectorLevel, targetLevel);
			denominator = ElementalDefenseBase + Math.Max(0, level - 50) * 10;
		}
		else
		{
			denominator = ElementalDefenseBase;
		}

		return new ElementalDefenseDenominatorPlan(
			isTargetPlayer, effectorLevel, targetLevel, denominator,
			isTargetPlayer
				? $"StatFunctions.getElementalDefenseDenominator -> Player target -> 1300+max(0,min({effectorLevel},{targetLevel})-50)*10 = {denominator}"
				: $"StatFunctions.getElementalDefenseDenominator -> NPC target -> {ElementalDefenseBase}"
		);
	}

	public static ElementalDamageReductionPlan CreateReductionPlan(
		float damage,
		int adjustedDefense,
		int denominator)
	{
		// Java parity: StatFunctions.reduceDamageByElementalDefense.
		// return damage * (1f - adjustedDefense / elementalDenominator)
		var reductionFactor = 1f - adjustedDefense / (float)denominator;
		var reducedDamage = damage * reductionFactor;

		return new ElementalDamageReductionPlan(
			damage, adjustedDefense, denominator, reductionFactor, reducedDamage,
			$"StatFunctions.reduceDamageByElementalDefense -> {damage} * (1 - {adjustedDefense}/{denominator}) = {reducedDamage:F2}"
		);
	}
}
