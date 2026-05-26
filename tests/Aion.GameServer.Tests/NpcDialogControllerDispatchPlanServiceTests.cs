using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcDialogControllerDispatchPlanServiceTests
{
	[Fact]
	public void CreatePlan_ReturnsNoOpForKnownNonNpcCreatureController()
	{
		var plan = NpcDialogControllerDispatchPlanService.CreatePlan(
			new NpcDialogControllerDispatchInput(CreateDispatch(), TargetIsNpc: false));

		Assert.Equal(NpcDialogControllerDispatchStatus.CreatureControllerNoOp, plan.Status);
		Assert.False(plan.CallsNpcAi);
		Assert.False(plan.CallsDialogService);
		Assert.Null(plan.DialogServiceFallback);
		Assert.False(plan.IsLive);
		Assert.Contains("CreatureController.onDialogSelect", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_ReturnsBeforeAiWhenNpcIsOutsideTalkRange()
	{
		var plan = NpcDialogControllerDispatchPlanService.CreatePlan(
			new NpcDialogControllerDispatchInput(
				CreateDispatch(),
				TargetIsNpc: true,
				IsInTalkRange: false,
				NpcAiHandledDialogSelect: false));

		Assert.Equal(NpcDialogControllerDispatchStatus.OutOfTalkRange, plan.Status);
		Assert.False(plan.CallsNpcAi);
		Assert.False(plan.CallsDialogService);
		Assert.Null(plan.DialogServiceFallback);
		Assert.Contains("PositionUtil.isInTalkRange", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_StopsAfterAiWhenNpcAiHandlesDialogSelect()
	{
		var plan = NpcDialogControllerDispatchPlanService.CreatePlan(
			new NpcDialogControllerDispatchInput(
				CreateDispatch(),
				TargetIsNpc: true,
				IsInTalkRange: true,
				NpcAiHandledDialogSelect: true));

		Assert.Equal(NpcDialogControllerDispatchStatus.AiHandled, plan.Status);
		Assert.True(plan.CallsNpcAi);
		Assert.False(plan.CallsDialogService);
		Assert.Null(plan.DialogServiceFallback);
		Assert.Contains("returned true", plan.JavaSource);
	}

	[Fact]
	public void CreatePlan_FallsBackToDialogServiceWhenNpcAiDoesNotHandleDialogSelect()
	{
		var dispatch = CreateDispatch(
			targetObjectId: 71004,
			dialogActionId: 33,
			lastPage: 9,
			questId: 2001,
			extendedRewardIndex: 4);

		var plan = NpcDialogControllerDispatchPlanService.CreatePlan(
			new NpcDialogControllerDispatchInput(
				dispatch,
				TargetIsNpc: true,
				IsInTalkRange: true,
				NpcAiHandledDialogSelect: false));

		Assert.Equal(NpcDialogControllerDispatchStatus.DialogServiceFallback, plan.Status);
		Assert.True(plan.CallsNpcAi);
		Assert.True(plan.CallsDialogService);
		var fallback = Assert.IsType<NpcDialogServiceFallbackDescriptor>(plan.DialogServiceFallback);
		Assert.False(fallback.IsLive);
		Assert.Equal(71004, fallback.TargetObjectId);
		Assert.Equal(33, fallback.DialogActionId);
		Assert.Equal(2001, fallback.QuestId);
		Assert.Equal(4, fallback.ExtendedRewardIndex);
		Assert.DoesNotContain("lastPage", fallback.JavaSource);
		Assert.Null(plan.DialogServicePlan);
	}

	[Fact]
	public void CreatePlan_PreservesOriginalControllerDispatchDescriptor()
	{
		var dispatch = CreateDispatch(targetObjectId: 81011, dialogActionId: 1011, lastPage: 12);

		var plan = NpcDialogControllerDispatchPlanService.CreatePlan(
			new NpcDialogControllerDispatchInput(dispatch, TargetIsNpc: true));

		Assert.Same(dispatch, plan.Dispatch);
		Assert.Equal(12, plan.Dispatch.LastPage);
		Assert.Equal(1011, plan.Dispatch.DialogActionId);
	}

	[Fact]
	public void CreatePlan_ComposesDialogServicePlanOnlyWhenAiFallsBack()
	{
		var plan = NpcDialogControllerDispatchPlanService.CreatePlan(
			new NpcDialogControllerDispatchInput(
				CreateDispatch(dialogActionId: 2, questId: 0),
				TargetIsNpc: true,
				IsInTalkRange: true,
				NpcAiHandledDialogSelect: false,
				DialogServiceFacts: new NpcDialogServiceSelectFacts(
					HasTradeList: true,
					HasSellableTradeGoods: true,
					VendorBuyModifier: 120,
					TradeSellPriceRate: 75)));

		Assert.Equal(NpcDialogControllerDispatchStatus.DialogServiceFallback, plan.Status);
		var servicePlan = Assert.IsType<NpcDialogServiceSelectPlan>(plan.DialogServicePlan);
		Assert.Equal(NpcDialogServiceSelectStatus.BuyTradeList, servicePlan.Status);
		var descriptor = Assert.Single(servicePlan.Descriptors);
		Assert.Equal(9001, descriptor.TargetObjectId);
		Assert.Equal(2, descriptor.DialogActionId);
		Assert.Equal(90, descriptor.PriceModifier);
	}

	[Theory]
	[InlineData(false, false, NpcDialogControllerDispatchStatus.OutOfTalkRange)]
	[InlineData(true, true, NpcDialogControllerDispatchStatus.AiHandled)]
	public void CreatePlan_DoesNotComposeDialogServicePlanForShortCircuitedControllerBranches(
		bool isInTalkRange,
		bool npcAiHandledDialogSelect,
		NpcDialogControllerDispatchStatus expectedStatus)
	{
		var plan = NpcDialogControllerDispatchPlanService.CreatePlan(
			new NpcDialogControllerDispatchInput(
				CreateDispatch(dialogActionId: 2, questId: 0),
				TargetIsNpc: true,
				IsInTalkRange: isInTalkRange,
				NpcAiHandledDialogSelect: npcAiHandledDialogSelect,
				DialogServiceFacts: new NpcDialogServiceSelectFacts(
					HasTradeList: true,
					HasSellableTradeGoods: true)));

		Assert.Equal(expectedStatus, plan.Status);
		Assert.Null(plan.DialogServiceFallback);
		Assert.Null(plan.DialogServicePlan);
	}

	private static QuestDialogNpcControllerDispatchDescriptor CreateDispatch(
		int targetObjectId = 9001,
		int dialogActionId = 33,
		int lastPage = 7,
		int questId = 1001,
		int extendedRewardIndex = 3)
	{
		return new QuestDialogNpcControllerDispatchDescriptor(
			targetObjectId,
			dialogActionId,
			lastPage,
			questId,
			extendedRewardIndex,
			"CreatureController/NpcController.onDialogSelect",
			IsLive: false);
	}
}
