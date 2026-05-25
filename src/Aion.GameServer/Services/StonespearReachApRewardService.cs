using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class StonespearReachApRewardService
{
	public StonespearReachApRewardResult ApplyFinalApReward(
		Player? player,
		int points,
		bool bossKilled,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: data/handlers/instance/StonespearReachInstance.checkRank and reward.
		// AP is set only for S-rank completion when the guardian general path passes bossKilled=true.
		// Full Stonespear final rewards also include GP/items and LegionDominion finalization; this planner covers AP only.
		var rank = CalculateFinalRank(points);
		var finalAp = CalculateFinalAp(points, bossKilled);
		if (player == null)
			return StonespearReachApRewardResult.MissingPlayer(points, bossKilled, rank, finalAp);
		if (finalAp <= 0)
			return StonespearReachApRewardResult.NoApReward(player.ObjectId, points, bossKilled, rank, player.AbyssRank.Ap);

		var previousAp = player.AbyssRank.Ap;
		var plan = AbyssPointsService.AddAp(player, finalAp, abyssPointsOptions);
		return StonespearReachApRewardResult.FromAbyssPointsPlan(
			plan,
			player.ObjectId,
			points,
			bossKilled,
			rank,
			finalAp,
			previousAp);
	}

	public static int CalculateFinalRank(int points)
	{
		// Java parity: StonespearReachInstance.checkRank uses strict greater-than thresholds.
		if (points > 67_000)
			return 1;
		if (points > 41_000)
			return 2;
		if (points > 25_000)
			return 3;
		if (points > 8_800)
			return 5;
		if (points > 0)
			return 6;
		return 8;
	}

	public static int CalculateFinalAp(int points, bool bossKilled)
	{
		// Java parity: only S-rank boss-killed path sets reward.setFinalAP(points / 10).
		return points > 67_000 && bossKilled ? points / 10 : 0;
	}
}

public sealed record StonespearReachApRewardResult(
	StonespearReachApRewardStatus Status,
	int ObjectId,
	int Points,
	bool BossKilled,
	int Rank,
	int FinalAp,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static StonespearReachApRewardResult MissingPlayer(int points, bool bossKilled, int rank, int finalAp)
	{
		return new StonespearReachApRewardResult(
			StonespearReachApRewardStatus.MissingPlayer,
			0,
			points,
			bossKilled,
			rank,
			finalAp,
			0,
			0);
	}

	public static StonespearReachApRewardResult NoApReward(
		int objectId,
		int points,
		bool bossKilled,
		int rank,
		int currentAp)
	{
		return new StonespearReachApRewardResult(
			StonespearReachApRewardStatus.NoApReward,
			objectId,
			points,
			bossKilled,
			rank,
			0,
			currentAp,
			currentAp);
	}

	public static StonespearReachApRewardResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		int points,
		bool bossKilled,
		int rank,
		int finalAp,
		int previousAp)
	{
		return new StonespearReachApRewardResult(
			plan.Applied ? StonespearReachApRewardStatus.Applied : StonespearReachApRewardStatus.ApBoundarySkipped,
			objectId,
			points,
			bossKilled,
			rank,
			finalAp,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum StonespearReachApRewardStatus
{
	Applied,
	MissingPlayer,
	NoApReward,
	ApBoundarySkipped,
}
