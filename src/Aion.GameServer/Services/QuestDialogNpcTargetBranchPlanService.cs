namespace Aion.GameServer.Services;

public enum QuestDialogNpcTargetBranchStatus
{
	SelfTargetBranch,
	UnknownDialogAction,
	UnknownTarget,
	TargetNotCreature,
	UnsupportedFunctionAction,
	InteractionNotAllowed,
	DispatchController,
}

public sealed record QuestDialogNpcTargetBranchInput(
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
	bool IsFunctionDialog = false,
	bool NpcSupportsAction = false,
	bool InteractionAllowed = true);

public sealed record QuestDialogNpcControllerDispatchDescriptor(
	int TargetObjectId,
	int DialogActionId,
	int LastPage,
	int QuestId,
	int ExtendedRewardIndex,
	string JavaSource,
	bool IsLive = false);

public sealed record QuestDialogNpcTargetBranchPlan(
	QuestDialogNpcTargetBranchStatus Status,
	string JavaSource,
	bool IsLive,
	QuestDialogNpcControllerDispatchDescriptor? Dispatch = null,
	string? AuditReason = null);

public static class QuestDialogNpcTargetBranchPlanService
{
	private const int Select1 = 1011;

	public static QuestDialogNpcTargetBranchPlan CreatePlan(QuestDialogNpcTargetBranchInput input)
	{
		// Java parity breadcrumb: network/aion/clientpackets/CM_DIALOG_SELECT.runImpl.
		// This planner models only the non-self target branch and does not dispatch AI,
		// DialogService, packets, audits, or live controller calls.
		if (!input.DialogActionKnown)
		{
			return NotPlanned(
				QuestDialogNpcTargetBranchStatus.UnknownDialogAction,
				"CM_DIALOG_SELECT.runImpl -> DialogAction.nameOf null warning");
		}

		if (input.TargetObjectId == 0 || input.TargetObjectId == input.PlayerObjectId)
		{
			return NotPlanned(
				QuestDialogNpcTargetBranchStatus.SelfTargetBranch,
				"CM_DIALOG_SELECT.runImpl self/player target branch");
		}

		if (!input.TargetExists)
		{
			return NotPlanned(
				QuestDialogNpcTargetBranchStatus.UnknownTarget,
				"player.getKnownList().getObject(targetObjectId)");
		}

		if (!input.TargetIsCreature)
		{
			return NotPlanned(
				QuestDialogNpcTargetBranchStatus.TargetNotCreature,
				"player.getKnownList().getObject(targetObjectId) instanceof Creature");
		}

		if (input.TargetIsNpc)
		{
			if (input.IsFunctionDialog && !input.NpcSupportsAction)
			{
				return NotPlanned(
					QuestDialogNpcTargetBranchStatus.UnsupportedFunctionAction,
					"DataManager.NPC_DATA.isFunctionDialog && !NpcTemplate.supportsAction",
					"tried to use unsupported dialog action");
			}

			if ((input.IsFunctionDialog || input.DialogActionId < Select1) && !input.InteractionAllowed)
			{
				return NotPlanned(
					QuestDialogNpcTargetBranchStatus.InteractionNotAllowed,
					"(isFunctionDialog || dialogActionId < SELECT1) && !DialogService.isInteractionAllowed",
					"tried to illegally use dialog action");
			}
		}

		return new QuestDialogNpcTargetBranchPlan(
			QuestDialogNpcTargetBranchStatus.DispatchController,
			"target.getController().onDialogSelect(dialogActionId, lastPage, player, questId, extendedRewardIndex)",
			IsLive: false,
			new QuestDialogNpcControllerDispatchDescriptor(
				input.TargetObjectId,
				input.DialogActionId,
				input.LastPage,
				input.QuestId,
				input.ExtendedRewardIndex,
				"CreatureController/NpcController.onDialogSelect",
				IsLive: false));
	}

	private static QuestDialogNpcTargetBranchPlan NotPlanned(
		QuestDialogNpcTargetBranchStatus status,
		string javaSource,
		string? auditReason = null)
	{
		return new QuestDialogNpcTargetBranchPlan(
			status,
			javaSource,
			IsLive: false,
			AuditReason: auditReason);
	}
}
