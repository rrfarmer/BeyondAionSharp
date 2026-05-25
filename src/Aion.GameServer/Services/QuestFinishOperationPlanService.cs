using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum QuestFinishOperationAction
{
	RewardMutationPlaceholder,
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
	bool IsLive);

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
		GameServerOptions options)
	{
		var mutation = QuestFinishStateMutationService.ApplyRewardCompletion(questState, template, now, options);
		if (!mutation.Applied)
		{
			return new QuestFinishOperationPlan(
				mutation.Status,
				mutation.QuestState,
				npcFactions,
				Array.Empty<QuestFinishOperationDescriptor>());
		}

		var descriptors = new List<QuestFinishOperationDescriptor>
		{
			// Java parity breadcrumb: QuestService.finishQuest computes/adds rewards and removes
			// work items before mutating QuestState. These are deliberately descriptors only.
			new(1, QuestFinishOperationAction.RewardMutationPlaceholder, "QuestService.finishQuest rewards", IsLive: false),
			new(2, QuestFinishOperationAction.RemoveQuestWorkItemsPlaceholder, "QuestService.removeQuestWorkItems", IsLive: false),
			new(3, QuestFinishOperationAction.QuestStateMutation, "QuestState.setStatus/setQuestVar/setNextRepeatTime", IsLive: false),
			new(4, QuestFinishOperationAction.QuestUpdatePacket, "SM_QUEST_ACTION(ActionType.UPDATE, qs)", IsLive: false),
			new(5, QuestFinishOperationAction.QuestCompletedCallback, "QuestEngine.onQuestCompleted", IsLive: false),
		};

		var plannedNpcFactions = npcFactions;
		if (template.NpcFactionId != 0)
		{
			var nextReset = NpcFactionDailyResetService.GetNextResetEpochSeconds(now, options);
			var factionCompletion = npcFactions.CompleteActiveQuest(template.IsMentorQuest, nextReset);
			plannedNpcFactions = factionCompletion.Snapshot;
			descriptors.Add(new QuestFinishOperationDescriptor(
				6,
				QuestFinishOperationAction.NpcFactionCompletion,
				"NpcFactions.completeQuest",
				IsLive: false));
		}

		var nextOrder = descriptors.Count == 6 ? 7 : 6;
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
}
