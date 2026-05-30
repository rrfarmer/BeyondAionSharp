namespace Aion.GameServer.Services;

public sealed record PvpXpRewardPlan(
	int WinnerMaxLevel,
	int DefeatedLevel,
	int WinnerAbyssRankId,
	int DefeatedAbyssRankId,
	int LevelDifference,
	int PointsAfterLevelAdjust,
	int AbyssRankPenalty,
	int FinalPoints,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record PvpDpRewardPlan(
	int DefeatedRankId,
	int WinnerMaxRankId,
	int DefeatedLevel,
	int WinnerLevel,
	int BasePoints,
	int AdjustedPoints,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PvpRewardPlanService
{
	// Java parity: utils/stats/StatFunctions.calculatePvpXpGained constants.
	public const int BaseXpPoints = 5000;

	// Java parity: utils/stats/StatFunctions.calculatePvpDpGained constants.
	public const int BaseDpPoints = 1064;
	public const int DpPerRank = 57;

	public static PvpXpRewardPlan CreateXpPlan(
		int winnerMaxLevel, int defeatedLevel,
		int winnerAbyssRankId, int defeatedAbyssRankId)
	{
		// Java parity: utils/stats/StatFunctions.calculatePvpXpGained.
		int points = BaseXpPoints;
		var diff = winnerMaxLevel - defeatedLevel;

		// Level penalty/bonus
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

		var afterLevel = points;

		// Abyss rank penalty
		var abyssRankDiff = winnerAbyssRankId - defeatedAbyssRankId;
		int penalty = 0;
		if (winnerAbyssRankId <= 7 && abyssRankDiff > 0)
		{
			var penaltyPercent = abyssRankDiff * 0.05f;
			penalty = (int)Math.Round(points * penaltyPercent);
			points -= penalty;
		}

		return new PvpXpRewardPlan(
			winnerMaxLevel, defeatedLevel, winnerAbyssRankId, defeatedAbyssRankId,
			diff, afterLevel, penalty, points,
			$"StatFunctions.calculatePvpXpGained -> base={BaseXpPoints} levelDiff={diff} abyssRankDiff={abyssRankDiff} -> {points}"
		);
	}

	public static PvpDpRewardPlan CreateDpPlan(
		int defeatedRankId, int winnerMaxRankId,
		int defeatedLevel, int winnerLevel)
	{
		// Java parity: utils/stats/StatFunctions.calculatePvpDpGained + adjustPvpDpGained.
		var basePoints = (defeatedRankId - winnerMaxRankId) * DpPerRank + BaseDpPoints;
		var adjusted = AdjustPvpDpGained(basePoints, defeatedLevel, winnerLevel);

		return new PvpDpRewardPlan(
			defeatedRankId, winnerMaxRankId, defeatedLevel, winnerLevel,
			basePoints, adjusted,
			$"StatFunctions.calculatePvpDpGained -> (rankDiff={defeatedRankId - winnerMaxRankId})*{DpPerRank}+{BaseDpPoints}={basePoints} -> adjusted={adjusted}"
		);
	}

	public static int AdjustPvpDpGained(int points, int defeatedLevel, int killerLevel)
	{
		// Java parity: utils/stats/StatFunctions.adjustPvpDpGained.
		var diff = killerLevel - defeatedLevel;
		if (diff >= 10)
			return 0;
		if (diff >= 0)
			return points - (int)(points * diff * 0.1);
		if (diff <= -10)
			return (int)(points * 1.1);
		return points + (int)(points * Math.Abs(diff) * 0.01);
	}
}
