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
}
