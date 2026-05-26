namespace Aion.GameServer.Services;

public static class QuestDialogAutoRewardGuardPlanService
{
	private const int SelectedQuestAutoReward = 108;
	private const int SelectedQuestAutoReward1 = 110;
	private const int SelectedQuestAutoReward15 = 124;

	public static QuestDialogAutoRewardGuardPlan CreatePlan(QuestDialogAutoRewardGuardInput input)
	{
		// Java parity breadcrumb: network/aion/clientpackets/CM_DIALOG_SELECT.runImpl handles
		// self/player-target reportable quest auto rewards before NPC controller dialog dispatch.
		if (input.TargetObjectId != 0 && input.TargetObjectId != input.PlayerObjectId)
			return QuestDialogAutoRewardGuardPlan.NotPlanned(QuestDialogAutoRewardGuardStatus.NonSelfTarget, input);
		if (!input.QuestTemplateExists)
			return QuestDialogAutoRewardGuardPlan.NotPlanned(QuestDialogAutoRewardGuardStatus.MissingQuestTemplate, input);
		if (!input.QuestTemplateCanReport)
			return QuestDialogAutoRewardGuardPlan.NotPlanned(QuestDialogAutoRewardGuardStatus.NotReportableQuest, input);
		if (!IsAutoRewardDialogAction(input.DialogActionId))
			return QuestDialogAutoRewardGuardPlan.NotPlanned(QuestDialogAutoRewardGuardStatus.NotAutoRewardDialogAction, input);

		return QuestDialogAutoRewardGuardPlan.CreatePlanned(input);
	}

	public static bool IsAutoRewardDialogAction(int dialogActionId)
	{
		return dialogActionId == SelectedQuestAutoReward
			|| dialogActionId is >= SelectedQuestAutoReward1 and <= SelectedQuestAutoReward15;
	}
}

public sealed record QuestDialogAutoRewardGuardInput(
	int PlayerObjectId,
	int TargetObjectId,
	int DialogActionId,
	int QuestId,
	bool QuestTemplateExists,
	bool QuestTemplateCanReport);

public sealed record QuestDialogAutoRewardGuardPlan(
	QuestDialogAutoRewardGuardStatus Status,
	int QuestId,
	int DialogActionId,
	string JavaSource,
	bool IsLive,
	string? MissingDependency = null)
{
	public bool Planned => Status == QuestDialogAutoRewardGuardStatus.Planned;

	public static QuestDialogAutoRewardGuardPlan CreatePlanned(QuestDialogAutoRewardGuardInput input)
	{
		return new QuestDialogAutoRewardGuardPlan(
			QuestDialogAutoRewardGuardStatus.Planned,
			input.QuestId,
			input.DialogActionId,
			"CM_DIALOG_SELECT.runImpl -> QuestService.finishQuest(new QuestEnv(null, player, questId, dialogActionId))",
			IsLive: false);
	}

	public static QuestDialogAutoRewardGuardPlan NotPlanned(
		QuestDialogAutoRewardGuardStatus status,
		QuestDialogAutoRewardGuardInput input)
	{
		return new QuestDialogAutoRewardGuardPlan(
			status,
			input.QuestId,
			input.DialogActionId,
			"CM_DIALOG_SELECT.runImpl self/reportable auto-reward guard",
			IsLive: false,
			MissingDependency: status.ToString());
	}
}

public enum QuestDialogAutoRewardGuardStatus
{
	Planned,
	NonSelfTarget,
	MissingQuestTemplate,
	NotReportableQuest,
	NotAutoRewardDialogAction
}
