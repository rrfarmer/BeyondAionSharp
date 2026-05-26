using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed record QuestDialogNpcTargetBranchRuntimeSnapshot(
	int PlayerObjectId,
	int TargetObjectId,
	int DialogActionId,
	int LastPage,
	int QuestId,
	int ExtendedRewardIndex,
	bool DialogActionKnown = true,
	bool TargetExists = false,
	bool TargetIsCreature = false,
	bool TargetIsNpc = false,
	NpcTemplateSummary? TargetNpcTemplate = null,
	bool InteractionAllowed = true);

public sealed record QuestDialogNpcTargetBranchInputAssemblyPlan(
	QuestDialogNpcTargetBranchInput Input,
	QuestDialogNpcTargetBranchPlan BranchPlan,
	string JavaSource,
	bool IsLive = false);

public static class QuestDialogNpcTargetBranchInputAssemblyPlanService
{
	public static QuestDialogNpcTargetBranchInputAssemblyPlan CreatePlan(
		QuestDialogNpcTargetBranchRuntimeSnapshot snapshot,
		NpcTemplateTable npcTemplates)
	{
		ArgumentNullException.ThrowIfNull(npcTemplates);

		// Java parity breadcrumb: CM_DIALOG_SELECT.runImpl asks
		// DataManager.NPC_DATA.isFunctionDialog(dialogActionId) globally, then
		// checks the target NpcTemplate.supportsAction(dialogActionId) per NPC.
		// This is a non-live input adapter only; known-list, interaction checks,
		// audits, packets, and controller dispatch remain explicit dependencies.
		var input = new QuestDialogNpcTargetBranchInput(
			snapshot.PlayerObjectId,
			snapshot.TargetObjectId,
			snapshot.DialogActionId,
			snapshot.LastPage,
			snapshot.QuestId,
			snapshot.ExtendedRewardIndex,
			snapshot.DialogActionKnown,
			snapshot.TargetExists,
			snapshot.TargetIsCreature,
			snapshot.TargetIsNpc,
			npcTemplates.IsFunctionDialog(snapshot.DialogActionId),
			snapshot.TargetNpcTemplate?.SupportsDialogAction(snapshot.DialogActionId) == true,
			snapshot.InteractionAllowed);

		return new QuestDialogNpcTargetBranchInputAssemblyPlan(
			input,
			QuestDialogNpcTargetBranchPlanService.CreatePlan(input),
			"CM_DIALOG_SELECT.runImpl -> NpcData.isFunctionDialog + NpcTemplate.supportsAction",
			IsLive: false);
	}
}
