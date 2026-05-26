using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public enum QuestFinishSocketGuardedInputAssemblyStatus
{
	GuardRejected,
	InputAssembled,
}

public sealed record QuestFinishSocketGuardedInputAssemblyPlan(
	QuestFinishSocketGuardedInputAssemblyStatus Status,
	QuestDialogAutoRewardGuardPlan GuardPlan,
	QuestFinishSocketInputAssemblyPlan? InputPlan = null);

public static class QuestFinishSocketGuardedInputAssemblyPlanService
{
	public static QuestFinishSocketGuardedInputAssemblyPlan CreatePlan(
		Player player,
		CmDialogSelect packet,
		QuestFinishRewardProjectionLookupTable rewardProjections,
		NpcTemplateSummary? targetNpcTemplate = null)
	{
		ArgumentNullException.ThrowIfNull(player);
		ArgumentNullException.ThrowIfNull(packet);
		ArgumentNullException.ThrowIfNull(rewardProjections);

		var template = rewardProjections.TryGetQuest(packet.QuestId, out var entry)
			? entry?.Template
			: null;
		var guardPlan = QuestDialogAutoRewardGuardPlanService.CreatePlanFromTemplateSummary(
			new QuestDialogAutoRewardGuardTemplateInput(
				player.ObjectId,
				packet.TargetObjectId,
				packet.DialogActionId,
				packet.QuestId,
				template));

		if (!guardPlan.Planned)
		{
			return new QuestFinishSocketGuardedInputAssemblyPlan(
				QuestFinishSocketGuardedInputAssemblyStatus.GuardRejected,
				guardPlan);
		}

		// Java parity breadcrumb: CM_DIALOG_SELECT.runImpl reaches QuestService.finishQuest
		// only after target/self, template existence, can_report, and auto-reward guards.
		// This still creates a non-live input plan only.
		var inputPlan = QuestFinishSocketInputAssemblyPlanService.CreatePlan(
			player,
			packet,
			rewardProjections,
			targetNpcTemplate);

		return new QuestFinishSocketGuardedInputAssemblyPlan(
			QuestFinishSocketGuardedInputAssemblyStatus.InputAssembled,
			guardPlan,
			inputPlan);
	}
}
