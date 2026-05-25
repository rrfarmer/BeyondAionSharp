namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerAbyssRank(
	int DailyAp,
	int WeeklyAp,
	int Ap,
	int DailyGp,
	int WeeklyGp,
	int Gp,
	int Rank,
	int DailyKill,
	int WeeklyKill,
	int AllKill,
	int MaxRank,
	int LastKill,
	int LastAp,
	int LastGp,
	int RankingListPosition)
{
	// Java parity: dao/AbyssRankDAO.loadAbyssRank default row when no abyss_rank record exists.
	public static PlayerAbyssRank Default()
	{
		return new PlayerAbyssRank(
			DailyAp: 0,
			WeeklyAp: 0,
			Ap: 0,
			DailyGp: 0,
			WeeklyGp: 0,
			Gp: 0,
			Rank: 1,
			DailyKill: 0,
			WeeklyKill: 0,
			AllKill: 0,
			MaxRank: 1,
			LastKill: 0,
			LastAp: 0,
			LastGp: 0,
			RankingListPosition: 0);
	}

	public PlayerAbyssRank AddAp(int additionalAp, bool enableApCap = false, long apCapValue = 1_000_000)
	{
		// Java parity: model/gameobjects/player/AbyssRank.addAp plus utils/stats/AbyssRankEnum.getRankForPoints.
		var dailyAp = additionalAp > 0 ? Math.Max(0, DailyAp + additionalAp) : DailyAp;
		var weeklyAp = additionalAp > 0 ? Math.Max(0, WeeklyAp + additionalAp) : WeeklyAp;
		var cappedCount = enableApCap && Ap + additionalAp > apCapValue
			? (int)(apCapValue - Ap)
			: additionalAp;
		var currentAp = Math.Max(0, Ap + cappedCount);
		var newRank = GetRankForPoints(currentAp, Gp);
		var rank = newRank.RequiredGp == 0 ? newRank.Id : Rank;
		return this with
		{
			DailyAp = dailyAp,
			WeeklyAp = weeklyAp,
			Ap = currentAp,
			Rank = rank,
			MaxRank = Math.Max(MaxRank, rank),
		};
	}

	public static string GetRankL10n(string race, int rankId)
	{
		// Java parity: utils/stats/AbyssRankEnum.getRankL10n(Race, int) plus ChatUtil.l10n.
		if (rankId is < 1 or > 18)
			throw new ArgumentOutOfRangeException(nameof(rankId), "The given abyss rank is outside the Java rank enum.");

		var rank9L10nId = string.Equals(race, "ELYOS", StringComparison.Ordinal) ? 901215 : 901233;
		return ToClientL10n(rank9L10nId + rankId - 1);
	}

	private static AbyssRankDefinition GetRankForPoints(int ap, int gp)
	{
		var rankForPoints = AbyssRanks[0];
		foreach (var rank in AbyssRanks)
		{
			if (rank.RequiredAp <= ap && rank.RequiredGp <= gp)
				rankForPoints = rank;
		}

		return rankForPoints;
	}

	private static string ToClientL10n(int l10nId)
	{
		var shifted = (l10nId << 1) | 1;
		return string.Concat("$", (char)(shifted & 0xffff), (char)((shifted >>> 16) & 0xffff));
	}

	private static readonly AbyssRankDefinition[] AbyssRanks =
	[
		new(1, 0, 0),
		new(2, 1200, 0),
		new(3, 4220, 0),
		new(4, 10990, 0),
		new(5, 23500, 0),
		new(6, 42780, 0),
		new(7, 69700, 0),
		new(8, 105600, 0),
		new(9, 150800, 0),
		new(10, 0, 1244),
		new(11, 0, 1368),
		new(12, 0, 1915),
		new(13, 0, 3064),
		new(14, 0, 5210),
		new(15, 0, 8335),
		new(16, 0, 10002),
		new(17, 0, 11503),
		new(18, 0, 12437),
	];

	private sealed record AbyssRankDefinition(int Id, int RequiredAp, int RequiredGp);
}
