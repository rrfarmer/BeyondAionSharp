using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class PvpArenaApRewardService
{
	private const float RankRewardRate = 0.7f;
	private const float ScoreRewardRate = 0.3f;
	private readonly GameServerRateOptions _rateOptions;

	public PvpArenaApRewardService(GameServerOptions? options = null)
	{
		_rateOptions = options?.Rates ?? new GameServerRateOptions();
	}

	public PvpArenaApRewardResult ApplyApReward(
		Player? player,
		PvpArenaApRewardItem reward,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: data/handlers/instance/pvparenas/PvPArenaInstance.reward
		// only calls AbyssPointsService.addAp when reward.getAp().getTotalCount() > 0.
		if (player == null)
			return PvpArenaApRewardResult.MissingPlayer(reward);
		if (reward.TotalCount <= 0)
			return PvpArenaApRewardResult.NoApReward(player.ObjectId, player.AbyssRank.Ap, reward);

		var previousAp = player.AbyssRank.Ap;
		var plan = AbyssPointsService.AddAp(player, reward.TotalCount, abyssPointsOptions);
		return PvpArenaApRewardResult.FromAbyssPointsPlan(plan, player.ObjectId, reward, previousAp);
	}

	public PvpArenaApRewardItem CalculateApReward(
		PvpArenaKind arenaKind,
		byte membershipLevel,
		int baseApPerPlayer,
		int baseAp,
		int playerCount,
		int scorePoints,
		int totalPoints,
		float rankRewardRate,
		IReadOnlyList<float>? rewardRates = null)
	{
		var configRate = SelectConfiguredRewardRate(arenaKind, membershipLevel, rewardRates);
		return CalculateApReward(baseApPerPlayer, baseAp, playerCount, scorePoints, totalPoints, rankRewardRate, configRate);
	}

	public static PvpArenaApRewardItem CalculateApReward(
		int baseApPerPlayer,
		int baseAp,
		int playerCount,
		int scorePoints,
		int totalPoints,
		float rankRewardRate,
		float configRate)
	{
		// Java parity: PvPArenaInstance.calculateRewards AP branch:
		// total AP = base-per-player AP * player count; 70% rank pool, 30% score pool;
		// rank reward uses subclass getRewardRate, score reward uses player score / total points.
		if (playerCount <= 0 || totalPoints <= 0)
			return new PvpArenaApRewardItem(baseAp, 0, 0);

		var totalAp = baseApPerPlayer * playerCount;
		var rankAp = JavaFloatToInt(totalAp * RankRewardRate);
		var scoreAp = JavaFloatToInt(totalAp * ScoreRewardRate);
		var scoreRate = scorePoints / (float)totalPoints;
		var rankRewardAp = JavaFloatToInt(rankAp * rankRewardRate * configRate);
		var scoreRewardAp = JavaFloatToInt(scoreAp * scoreRate * configRate);
		return new PvpArenaApRewardItem(baseAp, rankRewardAp, scoreRewardAp);
	}

	public static PvpArenaApRewardItem CalculateIndividualApReward(
		PvpArenaApRewardItem groupReward,
		int groupSize,
		float configRate)
	{
		// Java parity: PvPArenaInstance.calculateIndividualReward uses Math.round(count * configRate / group size).
		if (groupSize <= 0)
			return new PvpArenaApRewardItem(0, 0, 0);

		return new PvpArenaApRewardItem(
			JavaMathRound(groupReward.BaseCount * configRate / groupSize),
			JavaMathRound(groupReward.RankingCount * configRate / groupSize),
			JavaMathRound(groupReward.ScoreCount * configRate / groupSize));
	}

	public float SelectConfiguredRewardRate(
		PvpArenaKind arenaKind,
		byte membershipLevel,
		IReadOnlyList<float>? overrideRates = null)
	{
		// Java parity: Arena subclasses call Rates.get(player, RatesConfig.PVP_ARENA_*_REWARD_RATES).
		var rates = overrideRates ?? arenaKind switch
		{
			PvpArenaKind.Discipline => _rateOptions.PvpArenaDisciplineRewardRates,
			PvpArenaKind.Chaos => _rateOptions.PvpArenaChaosRewardRates,
			PvpArenaKind.Harmony => _rateOptions.PvpArenaHarmonyRewardRates,
			PvpArenaKind.Glory => _rateOptions.PvpArenaGloryRewardRates,
			_ => Array.Empty<float>(),
		};
		return SelectMembershipRate(membershipLevel, rates);
	}

	private static float SelectMembershipRate(byte membershipLevel, IReadOnlyList<float> rates)
	{
		// Java parity: model/gameobjects/player/Rates.get returns 1 when the configured rate array is empty.
		if (rates.Count == 0)
			return 1f;

		return rates[Math.Min(rates.Count - 1, membershipLevel)];
	}

	private static int JavaFloatToInt(float value)
	{
		if (float.IsNaN(value))
			return 0;
		if (value <= int.MinValue)
			return int.MinValue;
		if (value >= int.MaxValue)
			return int.MaxValue;
		return (int)value;
	}

	private static int JavaMathRound(float value)
	{
		if (float.IsNaN(value))
			return 0;
		if (value <= int.MinValue)
			return int.MinValue;
		if (value >= int.MaxValue)
			return int.MaxValue;
		return (int)MathF.Floor(value + 0.5f);
	}
}

public sealed record PvpArenaApRewardItem(int BaseCount, int RankingCount, int ScoreCount)
{
	public int TotalCount => BaseCount + RankingCount + ScoreCount;
}

public sealed record PvpArenaApRewardResult(
	PvpArenaApRewardStatus Status,
	int ObjectId,
	PvpArenaApRewardItem Reward,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static PvpArenaApRewardResult MissingPlayer(PvpArenaApRewardItem reward)
	{
		return new PvpArenaApRewardResult(
			PvpArenaApRewardStatus.MissingPlayer,
			0,
			reward,
			0,
			0);
	}

	public static PvpArenaApRewardResult NoApReward(int objectId, int currentAp, PvpArenaApRewardItem reward)
	{
		return new PvpArenaApRewardResult(
			PvpArenaApRewardStatus.NoApReward,
			objectId,
			reward,
			currentAp,
			currentAp);
	}

	public static PvpArenaApRewardResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		PvpArenaApRewardItem reward,
		int previousAp)
	{
		return new PvpArenaApRewardResult(
			plan.Applied ? PvpArenaApRewardStatus.Applied : PvpArenaApRewardStatus.ApBoundarySkipped,
			objectId,
			reward,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum PvpArenaApRewardStatus
{
	Applied,
	MissingPlayer,
	NoApReward,
	ApBoundarySkipped,
}

public enum PvpArenaKind
{
	Discipline,
	Chaos,
	Harmony,
	Glory,
}
