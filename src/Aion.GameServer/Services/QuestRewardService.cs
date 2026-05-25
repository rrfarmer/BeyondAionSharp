using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class QuestRewardService
{
	private readonly GameServerRateOptions _rateOptions;
	private readonly WorldNpcResourceStatsService _resourceStats;

	public QuestRewardService(WorldNpcResourceStatsService resourceStats, GameServerOptions? options = null)
	{
		_resourceStats = resourceStats;
		_rateOptions = options?.Rates ?? new GameServerRateOptions();
	}

	public async ValueTask<QuestDpRewardResult> ApplyDpRewardAsync(
		Player? player,
		int rewardDp,
		int? maxDp = null)
	{
		// Java parity: services/QuestService.giveReward -> if (rewards.getDp() != 0) player.getCommonData().addDp(rewards.getDp()).
		if (player == null)
			return QuestDpRewardResult.MissingPlayer(rewardDp);
		if (rewardDp == 0)
			return QuestDpRewardResult.NoDpReward(player.ObjectId, player.Dp);

		var previousDp = player.Dp;
		var change = await _resourceStats.AddPlayerDpAsync(player, rewardDp, maxDp);
		return QuestDpRewardResult.FromDpChange(change, rewardDp, previousDp);
	}

	public QuestApRewardResult ApplyApReward(
		Player? player,
		int rewardAp,
		bool isNonCountQuest = false,
		IReadOnlyList<float>? apQuestRates = null,
		AbyssPointsAddOptions? abyssPointsOptions = null)
	{
		// Java parity: services/QuestService.giveReward -> rewards.getAp(),
		// Rates.AP_QUEST for non-NON_COUNT quests, then AbyssPointsService.addAp(player, ap).
		if (player == null)
			return QuestApRewardResult.MissingPlayer(rewardAp, isNonCountQuest);
		if (rewardAp == 0)
			return QuestApRewardResult.NoApReward(player.ObjectId, player.AbyssRank.Ap, isNonCountQuest);

		var appliedRewardAp = isNonCountQuest
			? rewardAp
			: ApplyQuestApRate(player.AccountMembership, rewardAp, apQuestRates ?? _rateOptions.ApQuestRates);
		var previousAp = player.AbyssRank.Ap;
		var plan = AbyssPointsService.AddAp(player, appliedRewardAp, abyssPointsOptions);
		return QuestApRewardResult.FromAbyssPointsPlan(
			plan,
			player.ObjectId,
			rewardAp,
			appliedRewardAp,
			isNonCountQuest,
			previousAp);
	}

	public static int ApplyQuestApRate(byte membershipLevel, int rewardAp, IReadOnlyList<float> apQuestRates)
	{
		// Java parity: model/gameobjects/player/Rates.AP_QUEST.calcResult.
		var result = (long)(rewardAp * SelectMembershipRate(membershipLevel, apQuestRates));
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

public sealed record QuestDpRewardResult(
	QuestDpRewardStatus Status,
	int ObjectId,
	int RewardDp,
	int PreviousDp,
	int CurrentDp,
	WorldNpcResourceChangeResult? Change = null)
{
	public static QuestDpRewardResult MissingPlayer(int rewardDp)
	{
		return new QuestDpRewardResult(
			QuestDpRewardStatus.MissingPlayer,
			0,
			rewardDp,
			0,
			0);
	}

	public static QuestDpRewardResult NoDpReward(int objectId, int currentDp)
	{
		return new QuestDpRewardResult(
			QuestDpRewardStatus.NoDpReward,
			objectId,
			0,
			currentDp,
			currentDp);
	}

	public static QuestDpRewardResult FromDpChange(
		WorldNpcResourceChangeResult change,
		int rewardDp,
		int previousDp)
	{
		var status = change.Status is WorldNpcResourceChangeStatus.StartingClass
			or WorldNpcResourceChangeStatus.MissingTarget
			or WorldNpcResourceChangeStatus.MissingMaxResource
			? QuestDpRewardStatus.DpBoundarySkipped
			: QuestDpRewardStatus.Applied;
		return new QuestDpRewardResult(
			status,
			change.ObjectId,
			rewardDp,
			previousDp,
			change.CurrentValue,
			change);
	}
}

public enum QuestDpRewardStatus
{
	Applied,
	MissingPlayer,
	NoDpReward,
	DpBoundarySkipped,
}

public sealed record QuestApRewardResult(
	QuestApRewardStatus Status,
	int ObjectId,
	int RewardAp,
	int AppliedRewardAp,
	bool IsNonCountQuest,
	int PreviousAp,
	int CurrentAp,
	AbyssPointsAddPlan? AbyssPointsPlan = null)
{
	public static QuestApRewardResult MissingPlayer(int rewardAp, bool isNonCountQuest)
	{
		return new QuestApRewardResult(
			QuestApRewardStatus.MissingPlayer,
			0,
			rewardAp,
			0,
			isNonCountQuest,
			0,
			0);
	}

	public static QuestApRewardResult NoApReward(int objectId, int currentAp, bool isNonCountQuest)
	{
		return new QuestApRewardResult(
			QuestApRewardStatus.NoApReward,
			objectId,
			0,
			0,
			isNonCountQuest,
			currentAp,
			currentAp);
	}

	public static QuestApRewardResult FromAbyssPointsPlan(
		AbyssPointsAddPlan plan,
		int objectId,
		int rewardAp,
		int appliedRewardAp,
		bool isNonCountQuest,
		int previousAp)
	{
		return new QuestApRewardResult(
			plan.Applied ? QuestApRewardStatus.Applied : QuestApRewardStatus.ApBoundarySkipped,
			objectId,
			rewardAp,
			appliedRewardAp,
			isNonCountQuest,
			previousAp,
			plan.UpdatedRank?.Ap ?? previousAp,
			plan);
	}
}

public enum QuestApRewardStatus
{
	Applied,
	MissingPlayer,
	NoApReward,
	ApBoundarySkipped,
}
