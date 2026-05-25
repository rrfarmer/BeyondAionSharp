using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class PvpInstanceApRewardService
{
	private const int MaxPlayersPerFaction = 6;
	private readonly GameServerRateOptions _rateOptions;

	public PvpInstanceApRewardService(GameServerOptions? options = null)
	{
		_rateOptions = options?.Rates ?? new GameServerRateOptions();
	}

	public PvpInstanceApRewardResult ApplyApReward(
		Player? player,
		int baseAp,
		int bonusAp,
		IReadOnlyList<float>? apDredgionRates = null,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: data/handlers/instance/dredgion/DredgionInstance.distributeRewards and
		// data/handlers/instance/pvp/BasicPvpInstance.distributeRewards call
		// AbyssPointsService.addAp(player, Rates.AP_DREDGION.calcResult(player, reward.getBaseAp() + reward.getBonusAp())).
		if (player == null)
			return PvpInstanceApRewardResult.MissingPlayer(baseAp, bonusAp);

		var totalAp = baseAp + bonusAp;
		var appliedAp = ApplyDredgionApRate(player.AccountMembership, totalAp, apDredgionRates ?? _rateOptions.ApDredgionRates);
		var previousAp = player.AbyssRank.Ap;
		var plan = AbyssPointsService.AddAp(player, appliedAp, abyssPointsOptions);
		return PvpInstanceApRewardResult.FromAbyssPointsPlan(plan, player.ObjectId, baseAp, bonusAp, totalAp, appliedAp, previousAp);
	}

	public static PvpInstanceApRewardBreakdown CalculateFactionApReward(
		int winnerApReward,
		int loserApReward,
		int drawApReward,
		int scorePoints,
		bool playerRaceWon,
		bool draw,
		int winnerBonusAp = 0)
	{
		// Java parity: DredgionInstance.doReward and BasicPvpInstance subclasses set base AP and bonus AP
		// before distributeRewards. Winners receive 2 * score / 6 bonus; losers and draws receive score / 6.
		var baseAp = playerRaceWon
			? winnerApReward + winnerBonusAp
			: draw ? drawApReward : loserApReward;
		var bonusAp = playerRaceWon
			? 2 * scorePoints / MaxPlayersPerFaction
			: scorePoints / MaxPlayersPerFaction;
		return new PvpInstanceApRewardBreakdown(baseAp, bonusAp);
	}

	public static int ApplyDredgionApRate(byte membershipLevel, int rewardAp, IReadOnlyList<float> apDredgionRates)
	{
		// Java parity: model/gameobjects/player/Rates.AP_DREDGION.calcResult.
		var result = (long)(rewardAp * SelectMembershipRate(membershipLevel, apDredgionRates));
		return JavaLongToIntOrOriginal(result, rewardAp);
	}

	private static float SelectMembershipRate(byte membershipLevel, IReadOnlyList<float> rates)
	{
		// Java parity: model/gameobjects/player/Rates.get returns 1 when the configured rate array is empty.
		if (rates.Count == 0)
			return 1f;

		return rates[Math.Min(rates.Count - 1, membershipLevel)];
	}

	private static int JavaLongToIntOrOriginal(long value, int original)
	{
		// Java parity: Rates.calcResult(int) returns the original value if Math.toIntExact overflows.
		if (value is < int.MinValue or > int.MaxValue)
			return original;
		return (int)value;
	}
}

public sealed record PvpInstanceApRewardBreakdown(int BaseAp, int BonusAp)
{
	public int TotalAp => BaseAp + BonusAp;
}

public sealed record PvpInstanceApRewardResult(
	PvpInstanceApRewardStatus Status,
	int ObjectId,
	int BaseAp,
	int BonusAp,
	int TotalAp,
	int AppliedAp,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static PvpInstanceApRewardResult MissingPlayer(int baseAp, int bonusAp)
	{
		return new PvpInstanceApRewardResult(
			PvpInstanceApRewardStatus.MissingPlayer,
			0,
			baseAp,
			bonusAp,
			baseAp + bonusAp,
			0,
			0,
			0);
	}

	public static PvpInstanceApRewardResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		int baseAp,
		int bonusAp,
		int totalAp,
		int appliedAp,
		int previousAp)
	{
		return new PvpInstanceApRewardResult(
			plan.Applied ? PvpInstanceApRewardStatus.Applied : PvpInstanceApRewardStatus.ApBoundarySkipped,
			objectId,
			baseAp,
			bonusAp,
			totalAp,
			appliedAp,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum PvpInstanceApRewardStatus
{
	Applied,
	MissingPlayer,
	ApBoundarySkipped,
}
