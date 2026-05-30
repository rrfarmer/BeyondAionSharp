namespace Aion.GameServer.Services;

public sealed record NpcBaseExpPlan(
	int MaxHp,
	string Rating,
	int RankOrdinal,
	float RatingMultiplier,
	float RankBonus,
	float TotalMultiplier,
	int BaseExp,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class NpcBaseExpPlanService
{
	// Java parity: utils/stats/StatFunctions.calculateBaseExp rating multipliers.
	public static float GetRatingMultiplier(string rating)
	{
		return rating.ToUpperInvariant() switch
		{
			"JUNK" => 2.0f,
			"NORMAL" => 2.2f,
			"ELITE" => 4.0f,
			"HERO" => 5.4f,
			"LEGENDARY" => 6.4f,
			_ => 2.2f,
		};
	}

	public static NpcBaseExpPlan CreatePlan(int maxHp, string rating, int rankOrdinal)
	{
		// Java parity: utils/stats/StatFunctions.calculateBaseExp.
		if (maxHp <= 0)
		{
			return new NpcBaseExpPlan(
				maxHp, rating, rankOrdinal,
				RatingMultiplier: 0f, RankBonus: 0f, TotalMultiplier: 0f, BaseExp: 0,
				"StatFunctions.calculateBaseExp -> maxHp <= 0 -> return 0"
			);
		}

		var ratingMult = GetRatingMultiplier(rating);
		var rankBonus = rankOrdinal * 0.2f;
		var totalMultiplier = ratingMult + rankBonus;
		var baseExp = (int)Math.Round(maxHp * totalMultiplier);

		return new NpcBaseExpPlan(
			maxHp, rating, rankOrdinal,
			ratingMult, rankBonus, totalMultiplier, baseExp,
			$"StatFunctions.calculateBaseExp -> maxHp={maxHp} * (rating({rating})={ratingMult} + rank({rankOrdinal})*0.2={rankBonus}) = {baseExp}"
		);
	}
}
