using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public enum QuestFinishSocketGuardedOperationCompositionStatus
{
	Composed,
	GuardRejected,
	InputNotReady,
}

public sealed record QuestFinishSocketGuardedOperationCompositionPlan(
	QuestFinishSocketGuardedOperationCompositionStatus Status,
	QuestFinishSocketGuardedInputAssemblyPlan GuardedInputPlan,
	QuestFinishSocketOperationCompositionPlan? OperationCompositionPlan = null);

public static class QuestFinishSocketGuardedOperationCompositionPlanService
{
	public static QuestFinishSocketGuardedOperationCompositionPlan CreatePlan(
		Player player,
		CmDialogSelect packet,
		QuestFinishRewardProjectionLookupTable rewardProjections,
		PlayerNpcFactionsSnapshot npcFactions,
		DateTimeOffset now,
		GameServerOptions options,
		NpcTemplateSummary? targetNpcTemplate = null,
		QuestCompletionCallbackPlan? callbackPlan = null,
		QuestPersistencePlan? questPersistencePlan = null,
		NpcFactionPersistencePlan? npcFactionPersistencePlan = null,
		QuestFinishRewardSideEffectContext? rewardSideEffectContext = null,
		QuestFinishBonusRewardInputAssemblyPlan? bonusRewardInputAssemblyPlan = null,
		QuestBonusRewardPlanningReport? bonusRewardPlanningReport = null)
	{
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(packet);
		ArgumentNullException.ThrowIfNull(rewardProjections);
		ArgumentNullException.ThrowIfNull(options);

		var guardedInputPlan = QuestFinishSocketGuardedInputAssemblyPlanService.CreatePlan(
			player,
			packet,
			rewardProjections,
			targetNpcTemplate);

		if (guardedInputPlan.Status != QuestFinishSocketGuardedInputAssemblyStatus.InputAssembled
			|| guardedInputPlan.InputPlan == null)
		{
			return new QuestFinishSocketGuardedOperationCompositionPlan(
				QuestFinishSocketGuardedOperationCompositionStatus.GuardRejected,
				guardedInputPlan);
		}

		// Java parity breadcrumb: CM_DIALOG_SELECT.runImpl reaches QuestService.finishQuest
		// only after the dialog auto-reward guard. This composes non-live operation
		// descriptors only; GameServerConnection still does not call this service.
		var operationCompositionPlan = QuestFinishSocketOperationCompositionPlanService.CreatePlan(
			guardedInputPlan.InputPlan,
			npcFactions,
			now,
			options,
			callbackPlan,
			questPersistencePlan,
			npcFactionPersistencePlan,
			rewardSideEffectContext,
			bonusRewardInputAssemblyPlan,
			bonusRewardPlanningReport);

		var status = operationCompositionPlan.Status == QuestFinishSocketOperationCompositionStatus.Composed
			? QuestFinishSocketGuardedOperationCompositionStatus.Composed
			: QuestFinishSocketGuardedOperationCompositionStatus.InputNotReady;

		return new QuestFinishSocketGuardedOperationCompositionPlan(
			status,
			guardedInputPlan,
			operationCompositionPlan);
	}
}
