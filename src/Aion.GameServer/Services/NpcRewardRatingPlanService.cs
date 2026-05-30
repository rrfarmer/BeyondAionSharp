namespace Aion.GameServer.Services;

public sealed record NpcDpRewardPlan(
	int TargetLevel,
	int PlayerLevel,
	string NpcRating,
	int RatingMultiplier,
	int BaseDP,
	int XpPercentage,
	int BaseDpReward,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record NpcApRewardPlan(
	int PlayerLevel,
	int TargetLevel,
	string NpcRating,
	int ApNpcRate,
	int BaseApReward,
	bool IsAboveLevel10Gap,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class NpcRewardRatingPlanService
{
	// Java parity: utils/stats/StatFunctions.calculateRatingMultiplier(NpcRating).
	public static int GetDpRatingMultiplier(string npcRating)
	{
		return npcRating.ToUpperInvariant() switch
		{
			"JUNK" => 2,
			"NORMAL" => 2,
			"ELITE" => 3,
			"HERO" => 4,
			"LEGENDARY" => 5,
			_ => 1,
		};
	}

	// Java parity: utils/stats/StatFunctions.getApNpcRating(NpcRating).
	public static int GetApNpcRate(string npcRating)
	{
		return npcRating.ToUpperInvariant() switch
		{
			"JUNK" => 1,
			"NORMAL" => 2,
			"ELITE" => 4,
			"HERO" => 35,
			"LEGENDARY" => 2500,
			_ => 1,
		};
	}

	public static NpcDpRewardPlan CreateDpRewardPlan(int targetLevel, int playerLevel, string npcRating)
	{
		// Java parity: utils/stats/StatFunctions.calculateDPReward (base formula before Rates.DP_PVE).
		var ratingMult = GetDpRatingMultiplier(npcRating);
		var baseDP = targetLevel * ratingMult;
		var xpPercentage = XpRewardPlanService.XpRewardFrom(targetLevel - playerLevel);
		var baseDpReward = (int)Math.Floor(baseDP * xpPercentage / 100f);

		return new NpcDpRewardPlan(
			targetLevel, playerLevel, npcRating, ratingMult, baseDP, xpPercentage, baseDpReward,
			$"StatFunctions.calculateDPReward -> baseDP={targetLevel}*{ratingMult}={baseDP} * xp%({xpPercentage}/100) = {baseDpReward} [before DP_PVE rate]"
		);
	}

	public static NpcApRewardPlan CreateApRewardPlan(int playerLevel, int targetLevel, string npcRating)
	{
		// Java parity: utils/stats/StatFunctions.calculatePvEApGained (base formula before Rates.AP_PVE).
		if (playerLevel - targetLevel > 10)
		{
			return new NpcApRewardPlan(
				playerLevel, targetLevel, npcRating,
				ApNpcRate: 0, BaseApReward: 1, IsAboveLevel10Gap: true,
				$"StatFunctions.calculatePvEApGained -> playerLevel({playerLevel}) - targetLevel({targetLevel}) > 10 -> return 1"
			);
		}

		var apRate = GetApNpcRate(npcRating);
		var baseAp = (int)Math.Floor(15.0 * apRate);

		return new NpcApRewardPlan(
			playerLevel, targetLevel, npcRating, apRate, baseAp, IsAboveLevel10Gap: false,
			$"StatFunctions.calculatePvEApGained -> floor(15 * apRate({apRate})) = {baseAp} [before AP_PVE rate]"
		);
	}
}
