namespace Aion.GameServer.Services;

public sealed record HateCalculationPlan(
	int Value,
	int BoostHate,
	int Result,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record PvpApRewardPlan(
	int WinnerMaxLevel,
	int DefeatedLevel,
	int WinnerAbyssRankId,
	int DefeatedAbyssRankId,
	int DefeatedPointsGained,
	int FinalPoints,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class NpcStatFormulaService
{
	public static HateCalculationPlan CreateHatePlan(int value, int boostHate)
	{
		// Java parity: utils/stats/StatFunctions.calculateHate.
		// return (int) ((long) value * (1000 + boostHate) / 1000);
		var result = (int)((long)value * (1000 + boostHate) / 1000);
		return new HateCalculationPlan(
			value, boostHate, result,
			$"StatFunctions.calculateHate -> (long){value} * (1000 + {boostHate}) / 1000 = {result}"
		);
	}

	public static PvpApRewardPlan CreatePvpApPlan(
		int winnerMaxLevel, int defeatedLevel,
		int winnerAbyssRankId, int defeatedAbyssRankId,
		int defeatedPointsGained)
	{
		// Java parity: utils/stats/StatFunctions.calculatePvpApGained.
		// Same level and abyss rank penalty algorithm as calculatePvpXpGained,
		// but starts with defeated.getAbyssRank().getRank().getPointsGained() instead of 5000.
		int points = defeatedPointsGained;
		var diff = winnerMaxLevel - defeatedLevel;

		if (diff > 4)
			points = (int)Math.Round(points * 0.1f);
		else if (diff < -3)
			points = (int)Math.Round(points * 1.3f);
		else
			points = diff switch
			{
				3 => (int)Math.Round(points * 0.85f),
				4 => (int)Math.Round(points * 0.65f),
				-2 => (int)Math.Round(points * 1.1f),
				-3 => (int)Math.Round(points * 1.2f),
				_ => points,
			};

		var abyssRankDiff = winnerAbyssRankId - defeatedAbyssRankId;
		if (winnerAbyssRankId <= 7 && abyssRankDiff > 0)
		{
			var penaltyPercent = abyssRankDiff * 0.05f;
			points -= (int)Math.Round(points * penaltyPercent);
		}

		return new PvpApRewardPlan(
			winnerMaxLevel, defeatedLevel, winnerAbyssRankId, defeatedAbyssRankId,
			defeatedPointsGained, points,
			$"StatFunctions.calculatePvpApGained -> base={defeatedPointsGained} levelDiff={diff} abyssRankDiff={abyssRankDiff} -> {points}"
		);
	}
}
