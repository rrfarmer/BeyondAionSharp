using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class QuestRewardService
{
	private readonly WorldNpcResourceStatsService _resourceStats;

	public QuestRewardService(WorldNpcResourceStatsService resourceStats)
	{
		_resourceStats = resourceStats;
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
