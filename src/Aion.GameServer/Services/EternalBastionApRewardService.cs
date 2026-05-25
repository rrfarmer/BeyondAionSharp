using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class EternalBastionApRewardService
{
	public EternalBastionApRewardResult ApplyFinalApReward(
		Player? player,
		int points,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: data/handlers/instance/EternalBastionInstance.endInstance and distributeRewards.
		// The instance derives rank from points, stores final AP for ranks 1-5, then always calls
		// AbyssPointsService.addAp(player, instanceReward.getFinalAp()) for every player inside.
		var rank = CalculateFinalRank(points);
		var finalAp = GetFinalAp(rank);
		if (player == null)
			return EternalBastionApRewardResult.MissingPlayer(points, rank, finalAp);

		var previousAp = player.AbyssRank.Ap;
		var plan = AbyssPointsService.AddAp(player, finalAp, abyssPointsOptions);
		return EternalBastionApRewardResult.FromAbyssPointsPlan(
			plan,
			player.ObjectId,
			points,
			rank,
			finalAp,
			previousAp);
	}

	public static int CalculateFinalRank(int points)
	{
		// Java parity: EternalBastionInstance.getFinalRank.
		if (points >= 90_000)
			return 1;
		if (points >= 82_000)
			return 2;
		if (points >= 60_000)
			return 3;
		if (points >= 30_000)
			return 4;
		if (points >= 5_000)
			return 5;
		return 8;
	}

	public static int GetFinalAp(int rank)
	{
		// Java parity: EternalBastionInstance.endInstance sets final AP only for ranks 1-5.
		return rank switch
		{
			1 => 35_000,
			2 => 25_000,
			3 => 15_000,
			4 => 11_000,
			5 => 7_000,
			_ => 0,
		};
	}
}

public sealed record EternalBastionApRewardResult(
	EternalBastionApRewardStatus Status,
	int ObjectId,
	int Points,
	int Rank,
	int FinalAp,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static EternalBastionApRewardResult MissingPlayer(int points, int rank, int finalAp)
	{
		return new EternalBastionApRewardResult(
			EternalBastionApRewardStatus.MissingPlayer,
			0,
			points,
			rank,
			finalAp,
			0,
			0);
	}

	public static EternalBastionApRewardResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		int points,
		int rank,
		int finalAp,
		int previousAp)
	{
		return new EternalBastionApRewardResult(
			plan.Applied ? EternalBastionApRewardStatus.Applied : EternalBastionApRewardStatus.ApBoundarySkipped,
			objectId,
			points,
			rank,
			finalAp,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum EternalBastionApRewardStatus
{
	Applied,
	MissingPlayer,
	ApBoundarySkipped,
}
