using Aion.GameServer.Dataholders;
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

	[Fact]
	public void CreatePlanFromTemplateSummary_UsesRealCanReportAndStaticRewardMetadataWithoutLiveSideEffects()
	{
		var template = new NearbyQuestTemplateSummary(
			1001,
			CanReport: true,
			RewardRepeatCount: 2,
			HasRewards: true,
			HasExtendedRewards: true,
			HasBonus: true,
			HasQuestWorkItems: true);

		var plan = QuestDialogAutoRewardGuardPlanService.CreatePlanFromTemplateSummary(
			new QuestDialogAutoRewardGuardTemplateInput(
				PlayerObjectId: 77,
				TargetObjectId: 0,
				DialogActionId: 108,
				QuestId: 1001,
				QuestTemplate: template));

		Assert.True(plan.Planned);
		Assert.False(plan.IsLive);
		Assert.Null(plan.MissingDependency);
		var metadata = Assert.IsType<QuestDialogAutoRewardGuardStaticMetadata>(plan.StaticMetadata);
		Assert.Equal(2, metadata.RewardRepeatCount);
		Assert.True(metadata.HasRewards);
		Assert.True(metadata.HasExtendedRewards);
		Assert.True(metadata.HasBonus);
		Assert.True(metadata.HasAnyRewardMetadata);
		Assert.True(metadata.HasQuestWorkItems);
	}

	[Fact]
	public void CreatePlanFromTemplateSummary_RejectsMissingAndNonReportableTemplatesInJavaOrder()
	{
		var missingTemplatePlan = QuestDialogAutoRewardGuardPlanService.CreatePlanFromTemplateSummary(
			new QuestDialogAutoRewardGuardTemplateInput(
				PlayerObjectId: 77,
				TargetObjectId: 0,
				DialogActionId: 8,
				QuestId: 1001,
				QuestTemplate: null));
		var nonReportablePlan = QuestDialogAutoRewardGuardPlanService.CreatePlanFromTemplateSummary(
			new QuestDialogAutoRewardGuardTemplateInput(
				PlayerObjectId: 77,
				TargetObjectId: 0,
				DialogActionId: 8,
				QuestId: 1001,
				QuestTemplate: new NearbyQuestTemplateSummary(1001, CanReport: false, HasRewards: true)));

		Assert.Equal(QuestDialogAutoRewardGuardStatus.MissingQuestTemplate, missingTemplatePlan.Status);
		Assert.Null(missingTemplatePlan.StaticMetadata);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.NotReportableQuest, nonReportablePlan.Status);
		Assert.False(nonReportablePlan.Planned);
		Assert.True(nonReportablePlan.StaticMetadata?.HasRewards);
	}

	[Fact]
	public void CreatePlanFromTemplateSummary_RejectsNonSelfTargetBeforeUsingStaticMetadata()
	{
		var plan = QuestDialogAutoRewardGuardPlanService.CreatePlanFromTemplateSummary(
			new QuestDialogAutoRewardGuardTemplateInput(
				PlayerObjectId: 77,
				TargetObjectId: 88,
				DialogActionId: 108,
				QuestId: 1001,
				QuestTemplate: new NearbyQuestTemplateSummary(
					1001,
					CanReport: true,
					HasRewards: true,
					HasQuestWorkItems: true)));

		Assert.False(plan.Planned);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.NonSelfTarget, plan.Status);
		Assert.Null(plan.StaticMetadata);
	}

	[Fact]
	public void CreatePlanFromTemplateSummary_AllowsNonSelfTargetWhenNpcBranchOptedIn()
	{
		var plan = QuestDialogAutoRewardGuardPlanService.CreatePlanFromTemplateSummary(
			new QuestDialogAutoRewardGuardTemplateInput(
				PlayerObjectId: 77,
				TargetObjectId: 88,
				DialogActionId: 108,
				QuestId: 1001,
				QuestTemplate: new NearbyQuestTemplateSummary(
					1001,
					CanReport: true,
					HasRewards: true,
					HasQuestWorkItems: true)),
			allowNpcTarget: true);

		Assert.True(plan.Planned);
		Assert.Equal(QuestDialogAutoRewardGuardStatus.Planned, plan.Status);
		Assert.True(plan.StaticMetadata?.HasRewards);
		Assert.True(plan.StaticMetadata?.HasQuestWorkItems);
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
