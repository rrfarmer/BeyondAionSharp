using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum QuestFinishRewardGroupCorrectionStatus
{
	Unchanged,
	ClearedMissingRewards,
	ClampedOutOfRange,
	DefaultedFirstRewardGroup,
	IgnoredNonRewardState,
}

public enum QuestFinishRewardOperationAction
{
	RewardGroupCorrection,
	ItemRewardPlaceholder,
	NonItemRewardPlaceholder,
	ChallengeTaskCompletionPlaceholder,
	RemoveQuestWorkItemsPlaceholder,
}

public sealed record QuestFinishRewardWorkItem(int ItemId, long Count = 1);

public sealed record QuestFinishRewardTemplateProjection(
	int? RewardGroupCount = null,
	bool HasItemRewards = false,
	bool HasNonItemRewards = false,
	bool IsChallengeTask = false,
	IReadOnlyList<QuestFinishRewardWorkItem>? WorkItems = null)
{
	public IReadOnlyList<QuestFinishRewardWorkItem> WorkItems { get; init; } = WorkItems ?? [];
}

public sealed record QuestFinishRewardGroupCorrectionResult(
	PlayerQuestState QuestState,
	QuestFinishRewardGroupCorrectionStatus Status,
	int? OriginalRewardGroup);

public sealed record QuestFinishRewardOperationDescriptor(
	int Order,
	QuestFinishRewardOperationAction Action,
	string JavaSource,
	bool IsLive,
	int? ItemId = null,
	long? Count = null);

public sealed record QuestFinishRewardOperationPlan(
	PlayerQuestState QuestState,
	IReadOnlyList<QuestFinishRewardOperationDescriptor> Descriptors,
	QuestFinishRewardGroupCorrectionStatus CorrectionStatus,
	int? OriginalRewardGroup);

public static class QuestFinishRewardPlanService
{
	private const string RewardGroupJavaSource = "game-server/src/com/aionemu/gameserver/services/QuestService.java#validateAndFixRewardGroup";
	private const string RewardItemJavaSource = "game-server/src/com/aionemu/gameserver/services/QuestService.java#getRewardItems";
	private const string GiveRewardJavaSource = "game-server/src/com/aionemu/gameserver/services/QuestService.java#giveReward";
	private const string ChallengeTaskJavaSource = "game-server/src/com/aionemu/gameserver/services/QuestService.java#finishQuest";
	private const string WorkItemJavaSource = "game-server/src/com/aionemu/gameserver/services/QuestService.java#removeQuestWorkItems";

	public static QuestFinishRewardOperationPlan CreatePlan(
		PlayerQuestState questState,
		QuestFinishRewardTemplateProjection template)
	{
		ArgumentNullException.ThrowIfNull(questState);
		ArgumentNullException.ThrowIfNull(template);

		var correction = CorrectRewardGroup(questState, template.RewardGroupCount);
		var descriptors = new List<QuestFinishRewardOperationDescriptor>();
		if (correction.Status is QuestFinishRewardGroupCorrectionStatus.IgnoredNonRewardState)
		{
			return new QuestFinishRewardOperationPlan(
				correction.QuestState,
				descriptors,
				correction.Status,
				correction.OriginalRewardGroup);
		}

		var order = 1;

		if (correction.Status is not QuestFinishRewardGroupCorrectionStatus.Unchanged)
		{
			descriptors.Add(new QuestFinishRewardOperationDescriptor(
				order++,
				QuestFinishRewardOperationAction.RewardGroupCorrection,
				RewardGroupJavaSource,
				IsLive: false));
		}

		if (template.HasItemRewards)
		{
			descriptors.Add(new QuestFinishRewardOperationDescriptor(
				order++,
				QuestFinishRewardOperationAction.ItemRewardPlaceholder,
				RewardItemJavaSource,
				IsLive: false));
		}

		if (template.HasNonItemRewards)
		{
			descriptors.Add(new QuestFinishRewardOperationDescriptor(
				order++,
				QuestFinishRewardOperationAction.NonItemRewardPlaceholder,
				GiveRewardJavaSource,
				IsLive: false));
		}

		if (template.IsChallengeTask)
		{
			descriptors.Add(new QuestFinishRewardOperationDescriptor(
				order++,
				QuestFinishRewardOperationAction.ChallengeTaskCompletionPlaceholder,
				ChallengeTaskJavaSource,
				IsLive: false));
		}

		foreach (var workItem in template.WorkItems)
		{
			descriptors.Add(new QuestFinishRewardOperationDescriptor(
				order++,
				QuestFinishRewardOperationAction.RemoveQuestWorkItemsPlaceholder,
				WorkItemJavaSource,
				IsLive: false,
				ItemId: workItem.ItemId,
				Count: workItem.Count));
		}

		return new QuestFinishRewardOperationPlan(
			correction.QuestState,
			descriptors,
			correction.Status,
			correction.OriginalRewardGroup);
	}

	public static QuestFinishRewardGroupCorrectionResult CorrectRewardGroup(
		PlayerQuestState questState,
		int? rewardGroupCount)
	{
		ArgumentNullException.ThrowIfNull(questState);

		if (!string.Equals(questState.Status, "REWARD", StringComparison.Ordinal))
		{
			return new QuestFinishRewardGroupCorrectionResult(
				questState,
				QuestFinishRewardGroupCorrectionStatus.IgnoredNonRewardState,
				questState.RewardGroup);
		}

		if (questState.RewardGroup is { } rewardGroup)
		{
			if (rewardGroupCount is null)
			{
				return new QuestFinishRewardGroupCorrectionResult(
					questState with { RewardGroup = null },
					QuestFinishRewardGroupCorrectionStatus.ClearedMissingRewards,
					rewardGroup);
			}

			if (rewardGroup < 0 || rewardGroup >= rewardGroupCount.Value)
			{
				return new QuestFinishRewardGroupCorrectionResult(
					questState with { RewardGroup = rewardGroupCount.Value - 1 },
					QuestFinishRewardGroupCorrectionStatus.ClampedOutOfRange,
					rewardGroup);
			}
		}
		else if (rewardGroupCount is > 0)
		{
			return new QuestFinishRewardGroupCorrectionResult(
				questState with { RewardGroup = 0 },
				QuestFinishRewardGroupCorrectionStatus.DefaultedFirstRewardGroup,
				null);
		}

		return new QuestFinishRewardGroupCorrectionResult(
			questState,
			QuestFinishRewardGroupCorrectionStatus.Unchanged,
			questState.RewardGroup);
	}
}
