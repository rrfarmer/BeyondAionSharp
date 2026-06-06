using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public enum QuestFinishSocketInputAssemblyStatus
{
	Ready,
	NotQuestAutoRewardAction,
	MissingQuestState,
	QuestStateNotReward,
	MissingRewardProjection,
	ProjectionDiagnostics,
}

public sealed record QuestFinishSocketInputAssemblyPlan(
	QuestFinishSocketInputAssemblyStatus Status,
	PlayerQuestState? QuestState = null,
	NearbyQuestTemplateSummary? Template = null,
	QuestFinishRewardTemplateProjection? RewardProjection = null,
	QuestFinishRewardGroupCorrectionStatus? RewardGroupCorrectionStatus = null,
	IReadOnlyList<QuestFinishRewardProjectionLookupDiagnostic>? Diagnostics = null,
	QuestFinishRewardProjectionLookupStatus? RewardProjectionLookupStatus = null)
{
	public IReadOnlyList<QuestFinishRewardProjectionLookupDiagnostic> Diagnostics { get; } =
		Diagnostics ?? Array.Empty<QuestFinishRewardProjectionLookupDiagnostic>();
}

public static class QuestFinishSocketInputAssemblyPlanService
{
	private const int SelectedQuestAutoReward = 108;
	private const int SelectedQuestAutoReward1 = 110;
	private const int SelectedQuestAutoReward15 = 124;
	private const int SelectedQuestReward1 = 8;

	public static QuestFinishSocketInputAssemblyPlan CreatePlan(
		Player player,
		CmDialogSelect packet,
		QuestFinishRewardProjectionLookupTable rewardProjections,
		NpcTemplateSummary? targetNpcTemplate = null)
	{
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(packet);
		ArgumentNullException.ThrowIfNull(rewardProjections);

		if (!IsQuestAutoRewardAction(packet.DialogActionId))
		{
			return new QuestFinishSocketInputAssemblyPlan(
				QuestFinishSocketInputAssemblyStatus.NotQuestAutoRewardAction);
		}

		// Java parity breadcrumb: CM_DIALOG_SELECT.runImpl creates QuestEnv and calls
		// QuestService.finishQuest only for reportable auto-reward actions. This planner
		// assembles future inputs but intentionally performs no live finish execution.
		var questState = player.Quests.FirstOrDefault(quest => quest.QuestId == packet.QuestId);
		if (questState == null)
		{
			return new QuestFinishSocketInputAssemblyPlan(
				QuestFinishSocketInputAssemblyStatus.MissingQuestState);
		}

		if (!string.Equals(questState.Status, "REWARD", StringComparison.Ordinal))
		{
			return new QuestFinishSocketInputAssemblyPlan(
				QuestFinishSocketInputAssemblyStatus.QuestStateNotReward,
				questState);
		}

		var projectionEntry = rewardProjections.TryGetQuest(packet.QuestId, out var entry)
			? entry
			: null;
		var rewardGroupCount = projectionEntry?.RewardGroupProjections.Values.FirstOrDefault()?.RewardGroupCount;
		var correction = QuestFinishRewardPlanService.CorrectRewardGroup(questState, rewardGroupCount);
		var lookupPlan = QuestFinishRewardProjectionLookupPlanService.CreatePlan(
			new QuestFinishRewardProjectionLookupInput(
				packet.QuestId,
				NormalizeQuestRewardDialogAction(packet.DialogActionId),
				packet.ExtendedRewardIndex,
				correction.QuestState.CompleteCount,
				correction.QuestState.RewardGroup,
				player.PlayerClass,
				targetNpcTemplate?.TemplateId ?? 0,
				targetNpcTemplate != null),
			rewardProjections);
		if (lookupPlan.Status is not QuestFinishRewardProjectionLookupStatus.Found
			|| lookupPlan.Projection == null)
		{
			return new QuestFinishSocketInputAssemblyPlan(
				QuestFinishSocketInputAssemblyStatus.MissingRewardProjection,
				correction.QuestState,
				projectionEntry?.Template,
				RewardGroupCorrectionStatus: correction.Status,
				RewardProjectionLookupStatus: lookupPlan.Status);
		}

		if (lookupPlan.Diagnostics.Count != 0)
		{
			return new QuestFinishSocketInputAssemblyPlan(
				QuestFinishSocketInputAssemblyStatus.ProjectionDiagnostics,
				correction.QuestState,
				projectionEntry?.Template,
				lookupPlan.Projection,
				correction.Status,
				lookupPlan.Diagnostics);
		}

		return new QuestFinishSocketInputAssemblyPlan(
			QuestFinishSocketInputAssemblyStatus.Ready,
			correction.QuestState,
			projectionEntry?.Template,
			lookupPlan.Projection,
			correction.Status);
	}

	private static bool IsQuestAutoRewardAction(int dialogActionId)
	{
		return dialogActionId == SelectedQuestAutoReward
			|| (dialogActionId >= SelectedQuestAutoReward1 && dialogActionId <= SelectedQuestAutoReward15);
	}

	private static int NormalizeQuestRewardDialogAction(int dialogActionId)
	{
		// Java parity: DialogAction.SELECTED_QUEST_AUTO_REWARD1..15 mirror SELECTED_QUEST_REWARD1..15
		// for reportable quests, while QuestService.getRewardItems still indexes the selected reward list.
		if (dialogActionId >= SelectedQuestAutoReward1 && dialogActionId <= SelectedQuestAutoReward15)
			return SelectedQuestReward1 + dialogActionId - SelectedQuestAutoReward1;

		return dialogActionId;
	}
}
