using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestDialogNpcTargetBranchInputAssemblyPlanServiceTests
{
	private const int PlayerObjectId = 42;
	private const int TargetObjectId = 9001;
	private const int FunctionDialogAction = 33;

	[Fact]
	public void CreatePlan_DerivesFunctionDialogFromGlobalNpcTemplateTable()
	{
		var targetTemplate = CreateTemplate(203001);
		var templates = new NpcTemplateTable([targetTemplate, CreateTemplate(203002, [FunctionDialogAction])]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(targetTemplate),
			templates);

		Assert.True(plan.Input.IsFunctionDialog);
		Assert.False(plan.Input.NpcSupportsAction);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.UnsupportedFunctionAction, plan.BranchPlan.Status);
		Assert.Null(plan.BranchPlan.Dispatch);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_DerivesNpcSupportFromTargetTemplate()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(targetTemplate),
			templates);

		Assert.True(plan.Input.IsFunctionDialog);
		Assert.True(plan.Input.NpcSupportsAction);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		Assert.NotNull(plan.BranchPlan.Dispatch);
	}

	[Fact]
	public void CreatePlan_KeepsInteractionAllowedAsExplicitDependency()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(targetTemplate, interactionAllowed: false),
			templates);

		Assert.True(plan.Input.IsFunctionDialog);
		Assert.True(plan.Input.NpcSupportsAction);
		Assert.False(plan.Input.InteractionAllowed);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.InteractionNotAllowed, plan.BranchPlan.Status);
	}

	[Fact]
	public void CreatePlan_UsesInteractionPlanWhenProvided()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				interactionAllowed: true,
				interactionInput: new NpcDialogInteractionAllowedInput(
					PlayerObjectId,
					SubDialogType: NpcSubDialogType.Level,
					SubDialogValue: 50,
					PlayerLevel: 49)),
			templates);

		Assert.NotNull(plan.InteractionPlan);
		Assert.False(plan.InteractionPlan.IsAllowed);
		Assert.False(plan.Input.InteractionAllowed);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.InteractionNotAllowed, plan.BranchPlan.Status);
		Assert.Equal("tried to illegally use dialog action", plan.BranchPlan.AuditReason);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_AllowsWhenInteractionPlanAllows()
	{
		var targetTemplate = CreateTemplate(203001, [FunctionDialogAction]);
		var templates = new NpcTemplateTable([targetTemplate]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			CreateSnapshot(
				targetTemplate,
				interactionAllowed: false,
				interactionInput: new NpcDialogInteractionAllowedInput(
					PlayerObjectId,
					SubDialogType: NpcSubDialogType.Level,
					SubDialogValue: 50,
					PlayerLevel: 50)),
			templates);

		Assert.NotNull(plan.InteractionPlan);
		Assert.True(plan.InteractionPlan.IsAllowed);
		Assert.True(plan.Input.InteractionAllowed);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
	}

	[Fact]
	public void CreatePlan_DoesNotApplyNpcSupportGuardToNonNpcCreatures()
	{
		var templates = new NpcTemplateTable([CreateTemplate(203001, [FunctionDialogAction])]);

		var plan = QuestDialogNpcTargetBranchInputAssemblyPlanService.CreatePlan(
			new QuestDialogNpcTargetBranchRuntimeSnapshot(
				PlayerObjectId,
				TargetObjectId,
				FunctionDialogAction,
				LastPage: 7,
				QuestId: 1001,
				ExtendedRewardIndex: 3,
				TargetExists: true,
				TargetIsCreature: true,
				TargetIsNpc: false,
				InteractionAllowed: false),
			templates);

		Assert.True(plan.Input.IsFunctionDialog);
		Assert.False(plan.Input.NpcSupportsAction);
		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.BranchPlan.Status);
		Assert.NotNull(plan.BranchPlan.Dispatch);
	}

	private static QuestDialogNpcTargetBranchRuntimeSnapshot CreateSnapshot(
		NpcTemplateSummary targetTemplate,
		bool interactionAllowed = true,
		NpcDialogInteractionAllowedInput? interactionInput = null)
	{
		return new QuestDialogNpcTargetBranchRuntimeSnapshot(
			PlayerObjectId,
			TargetObjectId,
			FunctionDialogAction,
			LastPage: 7,
			QuestId: 1001,
			ExtendedRewardIndex: 3,
			TargetExists: true,
			TargetIsCreature: true,
			TargetIsNpc: true,
			TargetNpcTemplate: targetTemplate,
			InteractionAllowed: interactionAllowed,
			InteractionInput: interactionInput);
	}

	private static NpcTemplateSummary CreateTemplate(int templateId, IReadOnlyList<int>? functionDialogIds = null)
	{
		return new NpcTemplateSummary(
			templateId,
			$"npc_{templateId}",
			NameId: 0,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "NONE",
			Type: "NPC",
			FunctionDialogIds: functionDialogIds);
	}
}
