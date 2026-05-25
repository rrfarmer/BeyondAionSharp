using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum QuestFinishStateMutationStatus
{
	Applied,
	MissingQuestState,
	NotRewardState,
	MissionAlreadyCompleted,
}

public sealed record QuestFinishStateMutationResult(
	QuestFinishStateMutationStatus Status,
	PlayerQuestState? QuestState)
{
	public bool Applied => Status == QuestFinishStateMutationStatus.Applied;
}

public static class QuestFinishStateMutationService
{
	public static QuestFinishStateMutationResult ApplyRewardCompletion(
		PlayerQuestState? questState,
		NearbyQuestTemplateSummary template,
		DateTimeOffset now,
		GameServerOptions options)
	{
		// Java parity breadcrumb: services/QuestService.finishQuest rejects missing
		// states and only completes quests currently in QuestStatus.REWARD.
		if (questState is null)
			return new QuestFinishStateMutationResult(QuestFinishStateMutationStatus.MissingQuestState, null);
		if (!string.Equals(questState.Status, "REWARD", StringComparison.Ordinal))
			return new QuestFinishStateMutationResult(QuestFinishStateMutationStatus.NotRewardState, questState);

		// Java parity breadcrumb: missions may not be completed more than once.
		if (string.Equals(template.QuestCategory, "MISSION", StringComparison.Ordinal)
			&& questState.CompleteCount != 0)
			return new QuestFinishStateMutationResult(QuestFinishStateMutationStatus.MissionAlreadyCompleted, questState);

		var nextRepeatTime = template.IsTimeBased
			? QuestRepeatDateService.CalculateNextRepeatTime(now, template.RepeatCycle, options)
			: questState.NextRepeatTime;

		// Java parity breadcrumb: QuestState.setStatus(COMPLETE) increments
		// completeCount and completeTime when transitioning from a non-complete
		// status; QuestState.setQuestVar(0) clears quest vars while leaving flags.
		var completedState = questState with
		{
			Status = "COMPLETE",
			QuestVars = 0,
			CompleteCount = questState.CompleteCount + 1,
			CompleteTime = now,
			NextRepeatTime = nextRepeatTime,
		};

		return new QuestFinishStateMutationResult(QuestFinishStateMutationStatus.Applied, completedState);
	}
}
