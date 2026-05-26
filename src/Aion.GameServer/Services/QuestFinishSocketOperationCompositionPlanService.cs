using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum QuestFinishSocketOperationCompositionStatus
{
	Composed,
	InputNotReady,
}

public sealed record QuestFinishSocketOperationCompositionPlan(
	QuestFinishSocketOperationCompositionStatus Status,
	QuestFinishSocketInputAssemblyPlan InputPlan,
	QuestFinishOperationPlan? OperationPlan = null);

public static class QuestFinishSocketOperationCompositionPlanService
{
	public static QuestFinishSocketOperationCompositionPlan CreatePlan(
		QuestFinishSocketInputAssemblyPlan inputPlan,
		PlayerNpcFactionsSnapshot npcFactions,
		DateTimeOffset now,
		GameServerOptions options,
		QuestCompletionCallbackPlan? callbackPlan = null,
		QuestPersistencePlan? questPersistencePlan = null,
		NpcFactionPersistencePlan? npcFactionPersistencePlan = null,
		QuestFinishRewardSideEffectContext? rewardSideEffectContext = null,
		QuestFinishBonusRewardInputAssemblyPlan? bonusRewardInputAssemblyPlan = null,
		QuestBonusRewardPlanningReport? bonusRewardPlanningReport = null)
	{
		ArgumentNullException.ThrowIfNull(inputPlan);
		ArgumentNullException.ThrowIfNull(options);

		if (inputPlan is not
			{
				Status: QuestFinishSocketInputAssemblyStatus.Ready,
				QuestState: not null,
				Template: not null,
				RewardProjection: not null,
			})
		{
			return new QuestFinishSocketOperationCompositionPlan(
				QuestFinishSocketOperationCompositionStatus.InputNotReady,
				inputPlan);
		}

		// Java parity breadcrumb: CM_DIALOG_SELECT.runImpl -> QuestService.finishQuest.
		// This composer only creates the existing non-live operation plan and performs no
		// GameServerConnection routing, packet send, inventory mutation, or persistence.
		var operationPlan = QuestFinishOperationPlanService.CreatePlan(
			inputPlan.QuestState,
			inputPlan.Template,
			npcFactions,
			now,
			options,
			inputPlan.RewardProjection,
			callbackPlan,
			questPersistencePlan,
			npcFactionPersistencePlan,
			rewardSideEffectContext,
			bonusRewardInputAssemblyPlan,
			bonusRewardPlanningReport);

		return new QuestFinishSocketOperationCompositionPlan(
			QuestFinishSocketOperationCompositionStatus.Composed,
			inputPlan,
			operationPlan);
	}
}
