namespace Aion.GameServer.Services;

public sealed record EffectiveDropChancePlan(
	float BaseChance,
	bool IsDynamic,
	float RankModifier,
	float RatingModifier,
	float DynamicChance,
	float? ReductionDropRate,
	float BoostDropRate,
	float EffectiveChance,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class DropChanceFormulaService
{
	// Java parity: services/drop/DropRegistrationService.getRankModifier.
	public static float GetNpcRankModifier(string rank)
	{
		return rank.ToUpperInvariant() switch
		{
			"NOVICE" => 0.9f,
			"DISCIPLINED" => 1.0f,
			"SEASONED" => 1.05f,
			"EXPERT" => 1.1f,
			"VETERAN" => 1.15f,
			"MASTER" => 1.2f,
			_ => 1.0f,
		};
	}

	// Java parity: services/drop/DropRegistrationService.getRatingModifier.
	public static float GetNpcRatingModifier(string rating)
	{
		return rating.ToUpperInvariant() switch
		{
			"JUNK" => 0.5f,
			"NORMAL" => 1.0f,
			"ELITE" => 1.3f,
			"HERO" => 1.8f,
			"LEGENDARY" => 2.0f,
			_ => 1.0f,
		};
	}

	// Java parity: model/drop/DropModifiers.calculateDropChance.
	public static float CalculateDropChance(
		float chance,
		bool allowReductionDropRate,
		float? reductionDropRate,
		float boostDropRate)
	{
		if (allowReductionDropRate && reductionDropRate.HasValue)
			chance *= reductionDropRate.Value;
		return chance * boostDropRate;
	}

	// Java parity: services/drop/DropRegistrationService.calculateEffectiveChance.
	public static EffectiveDropChancePlan CreateEffectiveChancePlan(
		float baseChance,
		bool isDynamic,
		string npcRank,
		string npcRating,
		bool allowLevelBasedReduction,
		float? reductionDropRate,
		float boostDropRate)
	{
		var rankMod = GetNpcRankModifier(npcRank);
		var ratingMod = GetNpcRatingModifier(npcRating);

		var dynamicChance = isDynamic ? baseChance * rankMod * ratingMod : baseChance;
		var effective = CalculateDropChance(dynamicChance, allowLevelBasedReduction, reductionDropRate, boostDropRate);

		return new EffectiveDropChancePlan(
			baseChance, isDynamic, rankMod, ratingMod, dynamicChance,
			reductionDropRate, boostDropRate, effective,
			$"DropRegistrationService.calculateEffectiveChance -> base={baseChance} dynamic={isDynamic} rankMod={rankMod} ratingMod={ratingMod} -> {effective}"
		);
	}
}
