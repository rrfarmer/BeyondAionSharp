using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum QuestFinishOperationAction
{
	RewardMutationPlaceholder,
	RewardGroupCorrection,
	ItemRewardPlaceholder,
	NonItemRewardPlaceholder,
	ChallengeTaskCompletionPlaceholder,
	RemoveQuestWorkItemsPlaceholder,
	QuestStateMutation,
	QuestUpdatePacket,
	QuestCompletedCallback,
	NpcFactionCompletion,
	NearbyQuestRefresh,
	DeferredQuestPersistence,
	DeferredNpcFactionPersistence,
}

public sealed record QuestFinishOperationDescriptor(
	int Order,
	QuestFinishOperationAction Action,
	string JavaSource,
	bool IsLive,
	int? ItemId = null,
	long? Count = null);

public sealed record QuestFinishOperationPlan(
	QuestFinishStateMutationStatus Status,
	PlayerQuestState? QuestState,
	PlayerNpcFactionsSnapshot NpcFactions,
	IReadOnlyList<QuestFinishOperationDescriptor> Descriptors)
{
	public bool Applied => Status == QuestFinishStateMutationStatus.Applied;
}

public static class QuestFinishOperationPlanService
{
	public static QuestFinishOperationPlan CreatePlan(
		PlayerQuestState? questState,
		NearbyQuestTemplateSummary template,
		PlayerNpcFactionsSnapshot npcFactions,
		DateTimeOffset now,
		GameServerOptions options,
		QuestFinishRewardTemplateProjection? rewardProjection = null)
	{
		var guard = QuestFinishStateMutationService.ApplyRewardCompletion(questState, template, now, options);
		if (!guard.Applied)
		{
			return new QuestFinishOperationPlan(
				guard.Status,
				guard.QuestState,
				npcFactions,
				Array.Empty<QuestFinishOperationDescriptor>());
		}

		var descriptors = new List<QuestFinishOperationDescriptor>();
		var nextOrder = 1;
		var stateInput = questState!;

		if (rewardProjection is null)
		{
			// Java parity breadcrumb: QuestService.finishQuest computes/adds rewards and removes
			// work items before mutating QuestState. These are deliberately descriptors only.
			descriptors.Add(new(nextOrder++, QuestFinishOperationAction.RewardMutationPlaceholder, "QuestService.finishQuest rewards", IsLive: false));
			descriptors.Add(new(nextOrder++, QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder, "QuestService.removeQuestWorkItems", IsLive: false));
		}
		else
		{
			var rewardPlan = QuestFinishRewardPlanService.CreatePlan(stateInput, rewardProjection);
			stateInput = rewardPlan.QuestState;
			foreach (var rewardDescriptor in rewardPlan.Descriptors)
			{
				descriptors.Add(new QuestFinishOperationDescriptor(
					nextOrder++,
					MapRewardAction(rewardDescriptor.Action),
					rewardDescriptor.JavaSource,
					rewardDescriptor.IsLive,
					rewardDescriptor.ItemId,
					rewardDescriptor.Count));
			}
		}

		var mutation = QuestFinishStateMutationService.ApplyRewardCompletion(stateInput, template, now, options);
		descriptors.Add(new(nextOrder++, QuestFinishOperationAction.QuestStateMutation, "QuestState.setStatus/setQuestVar/setNextRepeatTime", IsLive: false));
		descriptors.Add(new(nextOrder++, QuestFinishOperationAction.QuestUpdatePacket, "SM_QUEST_ACTION(ActionType.UPDATE, qs)", IsLive: false));
		descriptors.Add(new(nextOrder++, QuestFinishOperationAction.QuestCompletedCallback, "QuestEngine.onQuestCompleted", IsLive: false));

		var plannedNpcFactions = npcFactions;
		if (template.NpcFactionId != 0)
		{
			var nextReset = NpcFactionDailyResetService.GetNextResetEpochSeconds(now, options);
			var factionCompletion = npcFactions.CompleteActiveQuest(template.IsMentorQuest, nextReset);
			plannedNpcFactions = factionCompletion.Snapshot;
			descriptors.Add(new QuestFinishOperationDescriptor(
				nextOrder++,
				QuestFinishOperationAction.NpcFactionCompletion,
				"NpcFactions.completeQuest",
				IsLive: false));
		}

		descriptors.Add(new QuestFinishOperationDescriptor(
			nextOrder++,
			QuestFinishOperationAction.NearbyQuestRefresh,
			"PlayerController.updateNearbyQuests",
			IsLive: false));
		descriptors.Add(new QuestFinishOperationDescriptor(
			nextOrder++,
			QuestFinishOperationAction.DeferredQuestPersistence,
			"PlayerService.storePlayer -> PlayerQuestListDAO.store",
			IsLive: false));
		if (template.NpcFactionId != 0)
		{
			descriptors.Add(new QuestFinishOperationDescriptor(
				nextOrder,
				QuestFinishOperationAction.DeferredNpcFactionPersistence,
				"PlayerService.storePlayer -> PlayerNpcFactionsDAO.storeNpcFactions",
				IsLive: false));
		}

		return new QuestFinishOperationPlan(
			mutation.Status,
			mutation.QuestState,
			plannedNpcFactions,
			descriptors);
	}

	private static QuestFinishOperationAction MapRewardAction(QuestFinishRewardOperationAction action)
	{
		return action switch
		{
			QuestFinishRewardOperationAction.RewardGroupCorrection => QuestFinishOperationAction.RewardGroupCorrection,
			QuestFinishRewardOperationAction.ItemRewardPlaceholder => QuestFinishOperationAction.ItemRewardPlaceholder,
			QuestFinishRewardOperationAction.NonItemRewardPlaceholder => QuestFinishOperationAction.NonItemRewardPlaceholder,
			QuestFinishRewardOperationAction.ChallengeTaskCompletionPlaceholder => QuestFinishOperationAction.ChallengeTaskCompletionPlaceholder,
			QuestFinishRewardOperationAction.RemoveQuestWorkItemsPlaceholder => QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder,
			_ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
		};
	}
}
