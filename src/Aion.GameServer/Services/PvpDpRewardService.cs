using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class PvpDpRewardService
{
	private const int BaseDp = 1064;
	private const int DpPerRank = 57;

	private readonly WorldNpcResourceStatsService _resourceStats;

	public PvpDpRewardService(WorldNpcResourceStatsService resourceStats)
	{
		_resourceStats = resourceStats;
	}

	public async ValueTask<PvpDpRewardResult> ApplyMemberDpRewardAsync(
		Player? member,
		Player? victim,
		int maxRank,
		int maxLevel,
		float groupDamagePercentage,
		int eligibleMemberCount,
		bool underDailyKillLimit,
		int? maxDp = null,
		float dpPvpRate = 1f)
	{
		// Java parity: services/PvpService.doReward member loop -> calculatePvpDpGained, per-member round, adjust/rate, addDp.
		if (member == null)
			return PvpDpRewardResult.MissingMember(victim?.ObjectId ?? 0, groupDamagePercentage, eligibleMemberCount);
		if (victim == null)
			return PvpDpRewardResult.MissingVictim(member.ObjectId, member.Dp, groupDamagePercentage, eligibleMemberCount);
		if (eligibleMemberCount <= 0)
			return PvpDpRewardResult.NoEligibleMembers(member.ObjectId, victim.ObjectId, member.Dp, groupDamagePercentage);

		var baseRewardDp = CalculatePvpDpGained(victim.Level, victim.AbyssRank.Rank, maxRank, maxLevel);
		var rewardPerMember = CalculateRewardPerMember(baseRewardDp, groupDamagePercentage, eligibleMemberCount);
		var memberDpGain = CalculateMemberDpGain(
			rewardPerMember,
			victim.Level,
			member.Level,
			underDailyKillLimit,
			dpPvpRate);
		var previousDp = member.Dp;
		var change = await _resourceStats.AddPlayerDpAsync(member, memberDpGain, maxDp);
		return PvpDpRewardResult.FromDpChange(
			change,
			victim.ObjectId,
			baseRewardDp,
			groupDamagePercentage,
			eligibleMemberCount,
			rewardPerMember,
			memberDpGain,
			underDailyKillLimit,
			previousDp);
	}

	public static int CalculatePvpDpGained(int victimLevel, int victimRank, int maxRank, int maxLevel)
	{
		// Java parity: utils/stats/StatFunctions.calculatePvpDpGained.
		var pointsGained = (victimRank - maxRank) * DpPerRank + BaseDp;
		return AdjustPvpDpGained(pointsGained, victimLevel, maxLevel);
	}

	public static int CalculateRewardPerMember(int baseRewardDp, float groupDamagePercentage, int eligibleMemberCount)
	{
		if (eligibleMemberCount <= 0)
			return 0;
		return JavaRound(baseRewardDp * groupDamagePercentage / eligibleMemberCount);
	}

	public static int CalculateMemberDpGain(
		int rewardPerMember,
		int victimLevel,
		int memberLevel,
		bool underDailyKillLimit,
		float dpPvpRate = 1f)
	{
		// Java starts memberDpGain at 1, and only replaces it while below the daily kill cap and the rounded reward is positive.
		if (!underDailyKillLimit || rewardPerMember <= 0)
			return 1;

		var adjusted = AdjustPvpDpGained(rewardPerMember, victimLevel, memberLevel);
		return ApplyDpPvpRate(adjusted, dpPvpRate);
	}

	public static int AdjustPvpDpGained(int points, int defeatedLevel, int killerLevel)
	{
		// Java parity: utils/stats/StatFunctions.adjustPvpDpGained.
		var pointsGained = points;
		var difference = killerLevel - defeatedLevel;
		if (difference >= 10)
			pointsGained = 0;
		else if (difference >= 0)
			pointsGained = JavaFloatToInt(pointsGained - pointsGained * difference * 0.1f);
		else if (difference <= -10)
			pointsGained = JavaFloatToInt(pointsGained * 1.1f);
		else
			pointsGained = JavaFloatToInt(pointsGained + pointsGained * Math.Abs(difference) * 0.01f);
		return pointsGained;
	}

	public static int ApplyDpPvpRate(int points, float dpPvpRate)
	{
		// Java parity: model/gameobjects/player/Rates.DP_PVP calcResult casts the rate result back to int.
		return JavaFloatToInt(points * dpPvpRate);
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
}

public sealed record PvpDpRewardResult(
	PvpDpRewardStatus Status,
	int ObjectId,
	int VictimObjectId,
	int BaseRewardDp,
	float GroupDamagePercentage,
	int EligibleMemberCount,
	int RewardPerMember,
	int MemberDpGain,
	bool UnderDailyKillLimit,
	int PreviousDp,
	int CurrentDp,
	WorldNpcResourceChangeResult? Change = null)
{
	public static PvpDpRewardResult MissingMember(
		int victimObjectId,
		float groupDamagePercentage,
		int eligibleMemberCount)
	{
		return new PvpDpRewardResult(
			PvpDpRewardStatus.MissingMember,
			0,
			victimObjectId,
			0,
			groupDamagePercentage,
			eligibleMemberCount,
			0,
			0,
			UnderDailyKillLimit: false,
			PreviousDp: 0,
			CurrentDp: 0);
	}

	public static PvpDpRewardResult MissingVictim(
		int objectId,
		int currentDp,
		float groupDamagePercentage,
		int eligibleMemberCount)
	{
		return new PvpDpRewardResult(
			PvpDpRewardStatus.MissingVictim,
			objectId,
			0,
			0,
			groupDamagePercentage,
			eligibleMemberCount,
			0,
			0,
			UnderDailyKillLimit: false,
			PreviousDp: currentDp,
			CurrentDp: currentDp);
	}

	public static PvpDpRewardResult NoEligibleMembers(
		int objectId,
		int victimObjectId,
		int currentDp,
		float groupDamagePercentage)
	{
		return new PvpDpRewardResult(
			PvpDpRewardStatus.NoEligibleMembers,
			objectId,
			victimObjectId,
			0,
			groupDamagePercentage,
			0,
			0,
			0,
			UnderDailyKillLimit: false,
			PreviousDp: currentDp,
			CurrentDp: currentDp);
	}

	public static PvpDpRewardResult FromDpChange(
		WorldNpcResourceChangeResult change,
		int victimObjectId,
		int baseRewardDp,
		float groupDamagePercentage,
		int eligibleMemberCount,
		int rewardPerMember,
		int memberDpGain,
		bool underDailyKillLimit,
		int previousDp)
	{
		var status = change.Status is WorldNpcResourceChangeStatus.StartingClass
			or WorldNpcResourceChangeStatus.MissingTarget
			or WorldNpcResourceChangeStatus.MissingMaxResource
			? PvpDpRewardStatus.DpBoundarySkipped
			: PvpDpRewardStatus.Applied;
		return new PvpDpRewardResult(
			status,
			change.ObjectId,
			victimObjectId,
			baseRewardDp,
			groupDamagePercentage,
			eligibleMemberCount,
			rewardPerMember,
			memberDpGain,
			underDailyKillLimit,
			previousDp,
			change.CurrentValue,
			change);
	}
}

public enum PvpDpRewardStatus
{
	Applied,
	MissingMember,
	MissingVictim,
	NoEligibleMembers,
	DpBoundarySkipped,
}
