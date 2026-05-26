using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestDialogAutoRewardGuardPlanServiceTests
{
	[Theory]
	[InlineData(0, 108)]
	[InlineData(77, 110)]
	[InlineData(77, 124)]
	public void CreatePlan_PlansNonLiveQuestFinishIntentForSelfOrPlayerTargetAutoReward(
		int targetObjectId,
		int dialogActionId)
	{
		var input = new QuestDialogAutoRewardGuardInput(
			PlayerObjectId: 77,
			TargetObjectId: targetObjectId,
			DialogActionId: dialogActionId,
			QuestId: 1001,
			QuestTemplateExists: true,
			QuestTemplateCanReport: true);

		var plan = QuestDialogAutoRewardGuardPlanService.CreatePlan(input);

		Assert.True(plan.Planned);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.Planned, plan.Status);
		Assert.Equal(1001, plan.QuestId);
		Assert.Equal(dialogActionId, plan.DialogActionId);
		Assert.False(plan.IsLive);
		Assert.Contains("CM_DIALOG_SELECT.runImpl", plan.JavaSource, StringComparison.Ordinal);
		Assert.Contains("QuestService.finishQuest", plan.JavaSource, StringComparison.Ordinal);
		Assert.Null(plan.MissingDependency);
	}

	[Theory]
	[InlineData(109)]
	[InlineData(8)]
	[InlineData(23)]
	[InlineData(125)]
	public void CreatePlan_RejectsDialogActionsOutsideJavaAutoRewardSwitch(int dialogActionId)
	{
		var plan = QuestDialogAutoRewardGuardPlanService.CreatePlan(
			new QuestDialogAutoRewardGuardInput(
				PlayerObjectId: 77,
				TargetObjectId: 0,
				DialogActionId: dialogActionId,
				QuestId: 1001,
				QuestTemplateExists: true,
				QuestTemplateCanReport: true));

		Assert.False(plan.Planned);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.NotAutoRewardDialogAction, plan.Status);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_RejectsNonSelfTargetBeforeQuestTemplateLookupEquivalent()
	{
		var plan = QuestDialogAutoRewardGuardPlanService.CreatePlan(
			new QuestDialogAutoRewardGuardInput(
				PlayerObjectId: 77,
				TargetObjectId: 88,
				DialogActionId: 108,
				QuestId: 1001,
				QuestTemplateExists: false,
				QuestTemplateCanReport: false));

		Assert.False(plan.Planned);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.NonSelfTarget, plan.Status);
	}

	[Fact]
	public void CreatePlan_RejectsMissingQuestTemplateBeforeReportableAndActionChecks()
	{
		var plan = QuestDialogAutoRewardGuardPlanService.CreatePlan(
			new QuestDialogAutoRewardGuardInput(
				PlayerObjectId: 77,
				TargetObjectId: 0,
				DialogActionId: 8,
				QuestId: 1001,
				QuestTemplateExists: false,
				QuestTemplateCanReport: false));

		Assert.False(plan.Planned);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.MissingQuestTemplate, plan.Status);
	}

	[Fact]
	public void CreatePlan_RejectsNonReportableQuestBeforeAutoRewardActionCheck()
	{
		var plan = QuestDialogAutoRewardGuardPlanService.CreatePlan(
			new QuestDialogAutoRewardGuardInput(
				PlayerObjectId: 77,
				TargetObjectId: 0,
				DialogActionId: 8,
				QuestId: 1001,
				QuestTemplateExists: true,
				QuestTemplateCanReport: false));

		Assert.False(plan.Planned);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.NotReportableQuest, plan.Status);
	}

	[Theory]
	[InlineData(108, true)]
	[InlineData(109, false)]
	[InlineData(110, true)]
	[InlineData(124, true)]
	[InlineData(125, false)]
	public void IsAutoRewardDialogAction_MatchesJavaSwitchConstants(int dialogActionId, bool expected)
	{
		Assert.Equal(expected, QuestDialogAutoRewardGuardPlanService.IsAutoRewardDialogAction(dialogActionId));
	}
}
