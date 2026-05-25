using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class PvpApRewardService
{
	private readonly GameServerRateOptions _rateOptions;

	public PvpApRewardService(GameServerOptions? options = null)
	{
		_rateOptions = options?.Rates ?? new GameServerRateOptions();
	}

	public PvpMemberApRewardResult ApplyMemberApReward(
		Player? member,
		Player? victim,
		int maxRank,
		int maxLevel,
		float groupDamagePercentage,
		int eligibleMemberCount,
		bool underDailyKillLimit,
		float apWinMultiplier = 1f,
		int apBoostStat = 100,
		IReadOnlyList<float>? apPvpGainRates = null,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: services/PvpService.rewardPlayerTeam AP branch -> calculatePvpApGained,
		// per-member Math.round share, Rates.AP_PVP.calcResult, then AbyssPointsService.addAp(member, victim, memberApGain).
		if (member == null)
			return PvpMemberApRewardResult.MissingMember(victim?.ObjectId ?? 0, groupDamagePercentage, eligibleMemberCount);
		if (victim == null)
			return PvpMemberApRewardResult.MissingVictim(member.ObjectId, member.AbyssRank.Ap, groupDamagePercentage, eligibleMemberCount);
		if (eligibleMemberCount <= 0)
			return PvpMemberApRewardResult.NoEligibleMembers(member.ObjectId, victim.ObjectId, member.AbyssRank.Ap, groupDamagePercentage);

		var baseReward = CalculatePvpApGained(victim.AbyssRank.Rank, victim.Level, maxRank, maxLevel);
		var rewardPerMember = CalculateRewardPerMember(baseReward * apWinMultiplier, groupDamagePercentage, eligibleMemberCount);
		var memberApGain = CalculateMemberApGain(
			member.AccountMembership,
			rewardPerMember,
			underDailyKillLimit,
			apBoostStat,
			apPvpGainRates ?? _rateOptions.ApPvpGainRates);
		var previousAp = member.AbyssRank.Ap;
		var plan = AbyssPointsService.AddApFromObject(
			member,
			victim.ObjectId,
			sourceIsPlayer: true,
			sourceIsSiegeNpc: false,
			sourceSiegeNpcPeace: false,
			memberApGain,
			abyssPointsOptions);
		return PvpMemberApRewardResult.FromAbyssPointsPlan(
			plan,
			member.ObjectId,
			victim.ObjectId,
			baseReward,
			groupDamagePercentage,
			eligibleMemberCount,
			rewardPerMember,
			memberApGain,
			underDailyKillLimit,
			previousAp);
	}

	public PvpVictimApLossResult ApplyVictimApLoss(
		Player? victim,
		Player? winner,
		int apRelevantDamage,
		int totalDamage,
		IReadOnlyList<float>? apPvpLossRates = null,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: services/PvpService.doReward victim-loss branch -> calculatePvPApLost,
		// scale by AP-relevant damage fraction, then AbyssPointsService.addAp(victim, -apActuallyLost).
		if (victim == null)
			return PvpVictimApLossResult.MissingVictim(winner?.ObjectId ?? 0, apRelevantDamage, totalDamage);
		if (winner == null)
			return PvpVictimApLossResult.MissingWinner(victim.ObjectId, victim.AbyssRank.Ap, apRelevantDamage, totalDamage);
		if (totalDamage <= 0 || apRelevantDamage <= 0)
			return PvpVictimApLossResult.NoRelevantDamage(victim.ObjectId, winner.ObjectId, victim.AbyssRank.Ap, apRelevantDamage, totalDamage);

		var baseLoss = CalculatePvpApLost(victim.AbyssRank.Rank, victim.Level, winner.Level);
		var ratedLoss = ApplyPvpLossRate(victim.AccountMembership, baseLoss, apPvpLossRates ?? _rateOptions.ApPvpLossRates);
		var actualLoss = JavaIntDamageShare(ratedLoss, apRelevantDamage, totalDamage);
		if (actualLoss <= 0)
			return PvpVictimApLossResult.NoApLoss(victim.ObjectId, winner.ObjectId, victim.AbyssRank.Ap, baseLoss, ratedLoss, apRelevantDamage, totalDamage);

		var previousAp = victim.AbyssRank.Ap;
		var plan = AbyssPointsService.AddAp(victim, -actualLoss, abyssPointsOptions);
		return PvpVictimApLossResult.FromAbyssPointsPlan(
			plan,
			victim.ObjectId,
			winner.ObjectId,
			baseLoss,
			ratedLoss,
			actualLoss,
			apRelevantDamage,
			totalDamage,
			previousAp);
	}

	public static int CalculatePvpApGained(int defeatedRank, int defeatedLevel, int winnerAbyssRank, int maxLevel)
	{
		// Java parity: utils/stats/StatFunctions.calculatePvpApGained.
		var pointsGained = GetPointsGained(defeatedRank);
		var difference = maxLevel - defeatedLevel;

		if (difference > 4)
			pointsGained = JavaRound(pointsGained * 0.1f);
		else if (difference < -3)
			pointsGained = JavaRound(pointsGained * 1.3f);
		else
		{
			pointsGained = difference switch
			{
				3 => JavaRound(pointsGained * 0.85f),
				4 => JavaRound(pointsGained * 0.65f),
				-2 => JavaRound(pointsGained * 1.1f),
				-3 => JavaRound(pointsGained * 1.2f),
				_ => pointsGained,
			};
		}

		var abyssRankDifference = winnerAbyssRank - defeatedRank;
		if (winnerAbyssRank <= 7 && abyssRankDifference > 0)
		{
			var penaltyPercent = abyssRankDifference * 0.05f;
			pointsGained -= JavaRound(pointsGained * penaltyPercent);
		}

		return pointsGained;
	}

	public static int CalculatePvpApLost(int defeatedRank, int defeatedLevel, int winnerLevel)
	{
		// Java parity: utils/stats/StatFunctions.calculatePvPApLost.
		var pointsLost = GetPointsLost(defeatedRank);
		var difference = winnerLevel - defeatedLevel;

		if (difference >= 5)
			pointsLost = JavaRound(pointsLost * 0.1f);
		else if (difference == 4)
			pointsLost = JavaRound(pointsLost * 0.65f);
		else if (difference == 3)
			pointsLost = JavaRound(pointsLost * 0.85f);

		return pointsLost;
	}

	public static int CalculateRewardPerMember(float baseApReward, float groupDamagePercentage, int eligibleMemberCount)
	{
		if (eligibleMemberCount <= 0)
			return 0;
		return JavaRound(baseApReward * groupDamagePercentage / eligibleMemberCount);
	}

	public static int CalculateMemberApGain(
		byte membershipLevel,
		int rewardPerMember,
		bool underDailyKillLimit,
		int apBoostStat = 100,
		IReadOnlyList<float>? apPvpGainRates = null)
	{
		// Java starts memberApGain at 1, and only replaces it while below the daily kill cap and the rounded reward is positive.
		if (!underDailyKillLimit || rewardPerMember <= 0)
			return 1;

		return ApplyPvpGainRate(membershipLevel, rewardPerMember, apPvpGainRates ?? [1f, 2f], apBoostStat);
	}

	public static int ApplyPvpGainRate(byte membershipLevel, int ap, IReadOnlyList<float> apPvpGainRates, int apBoostStat = 100)
	{
		// Java parity: model/gameobjects/player/Rates.AP_PVP.calcResult.
		var statRate = apBoostStat / 100f;
		var result = (long)(ap * SelectMembershipRate(membershipLevel, apPvpGainRates) * statRate);
		return JavaLongToIntOrOriginal(result, ap);
	}

	public static int ApplyPvpLossRate(byte membershipLevel, int ap, IReadOnlyList<float> apPvpLossRates)
	{
		// Java parity: model/gameobjects/player/Rates.AP_PVP_LOST.calcResult.
		var result = (long)(ap * SelectMembershipRate(membershipLevel, apPvpLossRates));
		return JavaLongToIntOrOriginal(result, ap);
	}

	private static int GetPointsGained(int rank)
	{
		var index = Math.Clamp(rank, 1, AbyssRankApPoints.Length) - 1;
		return AbyssRankApPoints[index].PointsGained;
	}

	private static int GetPointsLost(int rank)
	{
		var index = Math.Clamp(rank, 1, AbyssRankApPoints.Length) - 1;
		return AbyssRankApPoints[index].PointsLost;
	}

	private static float SelectMembershipRate(byte membershipLevel, IReadOnlyList<float> rates)
	{
		// Java parity: model/gameobjects/player/Rates.get returns 1 when the configured rate array is empty.
		if (rates.Count == 0)
			return 1f;

		return rates[Math.Min(rates.Count - 1, membershipLevel)];
	}

	private static int JavaRound(float value)
	{
		if (float.IsNaN(value))
			return 0;
		if (value <= int.MinValue)
			return int.MinValue;
		if (value >= int.MaxValue)
			return int.MaxValue;
		return (int)MathF.Floor(value + 0.5f);
	}

	private static int JavaIntDamageShare(int value, int apRelevantDamage, int totalDamage)
	{
		return (int)((long)value * apRelevantDamage / totalDamage);
	}

	private static int JavaLongToIntOrOriginal(long value, int original)
	{
		// Java parity: Rates.calcResult(int) returns the original value if Math.toIntExact overflows.
		if (value is < int.MinValue or > int.MaxValue)
			return original;
		return (int)value;
	}

	private static readonly (int PointsGained, int PointsLost)[] AbyssRankApPoints =
	[
		(300, 90),
		(345, 103),
		(396, 118),
		(455, 136),
		(523, 156),
		(601, 180),
		(721, 216),
		(865, 259),
		(1038, 311),
		(1557, 467),
		(1868, 560),
		(2148, 644),
		(2470, 741),
		(3705, 1482),
		(4075, 1630),
		(4482, 1792),
		(4930, 1972),
		(5916, 2366),
	];
}

public sealed record PvpMemberApRewardResult(
	PvpMemberApRewardStatus Status,
	int ObjectId,
	int VictimObjectId,
	int BaseRewardAp,
	float GroupDamagePercentage,
	int EligibleMemberCount,
	int RewardPerMember,
	int MemberApGain,
	bool UnderDailyKillLimit,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static PvpMemberApRewardResult MissingMember(
		int victimObjectId,
		float groupDamagePercentage,
		int eligibleMemberCount)
	{
		return new PvpMemberApRewardResult(
			PvpMemberApRewardStatus.MissingMember,
			0,
			victimObjectId,
			0,
			groupDamagePercentage,
			eligibleMemberCount,
			0,
			0,
			UnderDailyKillLimit: false,
			PreviousAp: 0,
			CurrentAp: 0);
	}

	public static PvpMemberApRewardResult MissingVictim(
		int objectId,
		int currentAp,
		float groupDamagePercentage,
		int eligibleMemberCount)
	{
		return new PvpMemberApRewardResult(
			PvpMemberApRewardStatus.MissingVictim,
			objectId,
			0,
			0,
			groupDamagePercentage,
			eligibleMemberCount,
			0,
			0,
			UnderDailyKillLimit: false,
			PreviousAp: currentAp,
			CurrentAp: currentAp);
	}

	public static PvpMemberApRewardResult NoEligibleMembers(
		int objectId,
		int victimObjectId,
		int currentAp,
		float groupDamagePercentage)
	{
		return new PvpMemberApRewardResult(
			PvpMemberApRewardStatus.NoEligibleMembers,
			objectId,
			victimObjectId,
			0,
			groupDamagePercentage,
			0,
			0,
			0,
			UnderDailyKillLimit: false,
			PreviousAp: currentAp,
			CurrentAp: currentAp);
	}

	public static PvpMemberApRewardResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		int victimObjectId,
		int baseRewardAp,
		float groupDamagePercentage,
		int eligibleMemberCount,
		int rewardPerMember,
		int memberApGain,
		bool underDailyKillLimit,
		int previousAp)
	{
		return new PvpMemberApRewardResult(
			plan.Applied ? PvpMemberApRewardStatus.Applied : PvpMemberApRewardStatus.ApBoundarySkipped,
			objectId,
			victimObjectId,
			baseRewardAp,
			groupDamagePercentage,
			eligibleMemberCount,
			rewardPerMember,
			memberApGain,
			underDailyKillLimit,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum PvpMemberApRewardStatus
{
	Applied,
	MissingMember,
	MissingVictim,
	NoEligibleMembers,
	ApBoundarySkipped,
}

public sealed record PvpVictimApLossResult(
	PvpVictimApLossStatus Status,
	int ObjectId,
	int WinnerObjectId,
	int BaseLossAp,
	int RatedLossAp,
	int ActualLossAp,
	int ApRelevantDamage,
	int TotalDamage,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static PvpVictimApLossResult MissingVictim(int winnerObjectId, int apRelevantDamage, int totalDamage)
	{
		return new PvpVictimApLossResult(
			PvpVictimApLossStatus.MissingVictim,
			0,
			winnerObjectId,
			0,
			0,
			0,
			apRelevantDamage,
			totalDamage,
			0,
			0);
	}

	public static PvpVictimApLossResult MissingWinner(int objectId, int currentAp, int apRelevantDamage, int totalDamage)
	{
		return new PvpVictimApLossResult(
			PvpVictimApLossStatus.MissingWinner,
			objectId,
			0,
			0,
			0,
			0,
			apRelevantDamage,
			totalDamage,
			currentAp,
			currentAp);
	}

	public static PvpVictimApLossResult NoRelevantDamage(
		int objectId,
		int winnerObjectId,
		int currentAp,
		int apRelevantDamage,
		int totalDamage)
	{
		return new PvpVictimApLossResult(
			PvpVictimApLossStatus.NoRelevantDamage,
			objectId,
			winnerObjectId,
			0,
			0,
			0,
			apRelevantDamage,
			totalDamage,
			currentAp,
			currentAp);
	}

	public static PvpVictimApLossResult NoApLoss(
		int objectId,
		int winnerObjectId,
		int currentAp,
		int baseLossAp,
		int ratedLossAp,
		int apRelevantDamage,
		int totalDamage)
	{
		return new PvpVictimApLossResult(
			PvpVictimApLossStatus.NoApLoss,
			objectId,
			winnerObjectId,
			baseLossAp,
			ratedLossAp,
			0,
			apRelevantDamage,
			totalDamage,
			currentAp,
			currentAp);
	}

	public static PvpVictimApLossResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		int winnerObjectId,
		int baseLossAp,
		int ratedLossAp,
		int actualLossAp,
		int apRelevantDamage,
		int totalDamage,
		int previousAp)
	{
		return new PvpVictimApLossResult(
			plan.Applied ? PvpVictimApLossStatus.Applied : PvpVictimApLossStatus.ApBoundarySkipped,
			objectId,
			winnerObjectId,
			baseLossAp,
			ratedLossAp,
			actualLossAp,
			apRelevantDamage,
			totalDamage,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum PvpVictimApLossStatus
{
	Applied,
	MissingVictim,
	MissingWinner,
	NoRelevantDamage,
	NoApLoss,
	ApBoundarySkipped,
}
