using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class QuestDialogNpcTargetBranchPlanServiceTests
{
	private const int PlayerObjectId = 42;
	private const int NpcObjectId = 9001;
	private const int BuyAction = 33;
	private const int Select1 = 1011;

	[Theory]
	[InlineData(0)]
	[InlineData(PlayerObjectId)]
	public void CreatePlan_ReturnsSelfTargetBranchBeforeKnownObjectLookup(int targetObjectId)
	{
		var plan = QuestDialogNpcTargetBranchPlanService.CreatePlan(
			CreateInput(
				targetObjectId,
				TargetExists: true,
				TargetIsCreature: true,
				TargetIsNpc: true,
				IsFunctionDialog: true,
				NpcSupportsAction: true));

		Assert.Equal(QuestDialogNpcTargetBranchStatus.SelfTargetBranch, plan.Status);
		Assert.Null(plan.Dispatch);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_RejectsUnknownDialogActionBeforeTargetBranching()
	{
		var plan = QuestDialogNpcTargetBranchPlanService.CreatePlan(
			CreateInput(
				NpcObjectId,
				DialogActionKnown: false,
				TargetExists: true,
				TargetIsCreature: true,
				TargetIsNpc: true,
				IsFunctionDialog: true,
				NpcSupportsAction: true));

		Assert.Equal(QuestDialogNpcTargetBranchStatus.UnknownDialogAction, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_ReturnsUnknownTargetWhenKnownListLookupMisses()
	{
		var plan = QuestDialogNpcTargetBranchPlanService.CreatePlan(CreateInput(NpcObjectId));

		Assert.Equal(QuestDialogNpcTargetBranchStatus.UnknownTarget, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_ReturnsTargetNotCreatureForKnownNonCreatureObjects()
	{
		var plan = QuestDialogNpcTargetBranchPlanService.CreatePlan(
			CreateInput(NpcObjectId, TargetExists: true));

		Assert.Equal(QuestDialogNpcTargetBranchStatus.TargetNotCreature, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_RejectsUnsupportedFunctionActionBeforeInteractionCheck()
	{
		var plan = QuestDialogNpcTargetBranchPlanService.CreatePlan(
			CreateInput(
				NpcObjectId,
				TargetExists: true,
				TargetIsCreature: true,
				TargetIsNpc: true,
				IsFunctionDialog: true,
				NpcSupportsAction: false,
				InteractionAllowed: false));

		Assert.Equal(QuestDialogNpcTargetBranchStatus.UnsupportedFunctionAction, plan.Status);
		Assert.Equal("tried to use unsupported dialog action", plan.AuditReason);
		Assert.Null(plan.Dispatch);
	}

	[Theory]
	[InlineData(BuyAction, true)]
	[InlineData(BuyAction, false)]
	[InlineData(Select1 - 1, false)]
	public void CreatePlan_RejectsInteractionOnlyForFunctionOrPreSelectNpcActions(
		int dialogActionId,
		bool isFunctionDialog)
	{
		var plan = QuestDialogNpcTargetBranchPlanService.CreatePlan(
			CreateInput(
				NpcObjectId,
				DialogActionId: dialogActionId,
				TargetExists: true,
				TargetIsCreature: true,
				TargetIsNpc: true,
				IsFunctionDialog: isFunctionDialog,
				NpcSupportsAction: true,
				InteractionAllowed: false));

		Assert.Equal(QuestDialogNpcTargetBranchStatus.InteractionNotAllowed, plan.Status);
		Assert.Equal("tried to illegally use dialog action", plan.AuditReason);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_AllowsSelectPageNpcActionsWithoutInteractionGuard()
	{
		var plan = QuestDialogNpcTargetBranchPlanService.CreatePlan(
			CreateInput(
				NpcObjectId,
				DialogActionId: Select1,
				TargetExists: true,
				TargetIsCreature: true,
				TargetIsNpc: true,
				IsFunctionDialog: false,
				NpcSupportsAction: false,
				InteractionAllowed: false));

		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.Status);
		Assert.NotNull(plan.Dispatch);
		Assert.Equal(NpcObjectId, plan.Dispatch.TargetObjectId);
		Assert.Equal(Select1, plan.Dispatch.DialogActionId);
	}

	[Fact]
	public void CreatePlan_DispatchesKnownNpcTargetAfterJavaGuards()
	{
		var plan = QuestDialogNpcTargetBranchPlanService.CreatePlan(
			CreateInput(
				NpcObjectId,
				TargetExists: true,
				TargetIsCreature: true,
				TargetIsNpc: true,
				IsFunctionDialog: true,
				NpcSupportsAction: true,
				InteractionAllowed: true));

		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.Status);
		var dispatch = Assert.IsType<QuestDialogNpcControllerDispatchDescriptor>(plan.Dispatch);
		Assert.False(dispatch.IsLive);
		Assert.Equal(NpcObjectId, dispatch.TargetObjectId);
		Assert.Equal(BuyAction, dispatch.DialogActionId);
		Assert.Equal(7, dispatch.LastPage);
		Assert.Equal(1001, dispatch.QuestId);
		Assert.Equal(3, dispatch.ExtendedRewardIndex);
	}

	[Fact]
	public void CreatePlan_DispatchesKnownNonNpcCreatureWithoutNpcGuards()
	{
		var plan = QuestDialogNpcTargetBranchPlanService.CreatePlan(
			CreateInput(
				targetObjectId: 2002,
				TargetExists: true,
				TargetIsCreature: true,
				TargetIsNpc: false,
				IsFunctionDialog: true,
				NpcSupportsAction: false,
				InteractionAllowed: false));

		Assert.Equal(QuestDialogNpcTargetBranchStatus.DispatchController, plan.Status);
		Assert.NotNull(plan.Dispatch);
		Assert.Equal(2002, plan.Dispatch.TargetObjectId);
	}

	private static QuestDialogNpcTargetBranchInput CreateInput(
		int targetObjectId,
		int DialogActionId = BuyAction,
		bool DialogActionKnown = true,
		bool TargetExists = false,
		bool TargetIsCreature = false,
		bool TargetIsNpc = false,
		bool IsFunctionDialog = false,
		bool NpcSupportsAction = false,
		bool InteractionAllowed = true)
	{
		return new QuestDialogNpcTargetBranchInput(
			PlayerObjectId,
			targetObjectId,
			DialogActionId,
			LastPage: 7,
			QuestId: 1001,
			ExtendedRewardIndex: 3,
			DialogActionKnown,
			TargetExists,
			TargetIsCreature,
			TargetIsNpc,
			IsFunctionDialog,
			NpcSupportsAction,
			InteractionAllowed);
	}
}
