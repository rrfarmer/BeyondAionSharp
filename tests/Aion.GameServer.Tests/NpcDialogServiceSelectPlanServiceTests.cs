using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcDialogServiceSelectPlanServiceTests
{
	[Fact]
	public void CreatePlan_RoutesQuestIdThroughQuestEngineOrNextPage()
	{
		var plan = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(CreateFallback(dialogActionId: 1011, questId: 2001, extendedRewardIndex: 4)));

		Assert.Equal(NpcDialogServiceSelectStatus.QuestEngineOrNextPage, plan.Status);
		Assert.True(plan.CallsQuestEngine);
		Assert.True(plan.SendsDialogWindow);
		Assert.Collection(
			plan.Descriptors,
			descriptor =>
			{
				Assert.Equal(NpcDialogServiceDescriptorKind.QuestEngineDialog, descriptor.Kind);
				Assert.Equal(2001, descriptor.QuestId);
				Assert.Equal(4, descriptor.ExtendedRewardIndex);
			},
			descriptor => Assert.Equal(NpcDialogServiceDescriptorKind.DialogWindowNextPage, descriptor.Kind));
		Assert.False(plan.IsLive);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(59)]
	public void CreatePlan_RoutesUseObjectAndExchangeCoinThroughQuestEngineEvenWithoutQuestId(int dialogActionId)
	{
		var plan = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(CreateFallback(dialogActionId: dialogActionId)));

		Assert.Equal(NpcDialogServiceSelectStatus.QuestEngineOrNextPage, plan.Status);
		Assert.True(plan.CallsQuestEngine);
		Assert.Equal(NpcDialogServiceDescriptorKind.QuestEngineDialog, plan.Descriptors[0].Kind);
	}

	[Fact]
	public void CreatePlan_PlansBuyTradeListWhenTradeListAndSellableGoodsExist()
	{
		var plan = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(
				CreateFallback(dialogActionId: 2),
				HasTradeList: true,
				HasSellableTradeGoods: true,
				VendorBuyModifier: 125,
				TradeSellPriceRate: 80));

		Assert.Equal(NpcDialogServiceSelectStatus.BuyTradeList, plan.Status);
		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.TradeListPacket, descriptor.Kind);
		Assert.Equal(100, descriptor.PriceModifier);
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	public void CreatePlan_PlansBuyUnavailableWhenTradeListOrSellableGoodsAreMissing(
		bool hasTradeList,
		bool hasSellableTradeGoods)
	{
		var plan = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(
				CreateFallback(dialogActionId: 2),
				HasTradeList: hasTradeList,
				HasSellableTradeGoods: hasSellableTradeGoods));

		Assert.Equal(NpcDialogServiceSelectStatus.BuyUnavailable, plan.Status);
		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.SystemMessageDoesNotSellItem, descriptor.Kind);
	}

	[Fact]
	public void CreatePlan_PlansDialogWindowOnlyWhenNpcSupportsAction()
	{
		var supported = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(CreateFallback(dialogActionId: 33), NpcSupportsAction: true));
		var unsupported = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(CreateFallback(dialogActionId: 33), NpcSupportsAction: false));

		Assert.Equal(NpcDialogServiceSelectStatus.DialogWindow, supported.Status);
		Assert.True(supported.SendsDialogWindow);
		Assert.Equal(NpcDialogServiceDescriptorKind.DialogWindowFromAction, Assert.Single(supported.Descriptors).Kind);
		Assert.Equal(NpcDialogServiceSelectStatus.UnsupportedDialogWindowAction, unsupported.Status);
		Assert.Empty(unsupported.Descriptors);
		Assert.Equal("Java sendDialogWindow silently skips unsupported action", unsupported.AuditReason);
	}

	[Theory]
	[InlineData(35, NpcDialogServiceDescriptorKind.ExperienceRecoveryRequest)]
	[InlineData(47, NpcDialogServiceDescriptorKind.CubeExpansion)]
	[InlineData(48, NpcDialogServiceDescriptorKind.WarehouseExpansion)]
	[InlineData(76, NpcDialogServiceDescriptorKind.ItemChargeRequest)]
	[InlineData(95, NpcDialogServiceDescriptorKind.ItemChargeRequest)]
	[InlineData(96, NpcDialogServiceDescriptorKind.StudioRecreate)]
	public void CreatePlan_PlansKnownServiceDispatchActions(int dialogActionId, NpcDialogServiceDescriptorKind expectedKind)
	{
		var plan = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(CreateFallback(dialogActionId: dialogActionId)));

		Assert.Equal(NpcDialogServiceSelectStatus.ServiceDispatch, plan.Status);
		Assert.Equal(expectedKind, Assert.Single(plan.Descriptors).Kind);
		Assert.False(plan.CallsQuestEngine);
		Assert.False(plan.SendsDialogWindow);
	}

	[Theory]
	[InlineData(3)]
	[InlineData(103)]
	public void CreatePlan_PlansSellItemWindowForSellActions(int dialogActionId)
	{
		var plan = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(CreateFallback(dialogActionId: dialogActionId)));

		Assert.Equal(NpcDialogServiceSelectStatus.SellItemWindow, plan.Status);
		Assert.Equal(NpcDialogServiceDescriptorKind.SellItemPacket, Assert.Single(plan.Descriptors).Kind);
	}

	[Fact]
	public void CreatePlan_PlansTradeInFromExplicitTradeInListAvailability()
	{
		var available = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(CreateFallback(dialogActionId: 78), HasTradeInList: true));
		var unavailable = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(CreateFallback(dialogActionId: 78), HasTradeInList: false));

		Assert.Equal(NpcDialogServiceSelectStatus.TradeInList, available.Status);
		Assert.Equal(NpcDialogServiceDescriptorKind.TradeInListPacket, Assert.Single(available.Descriptors).Kind);
		Assert.Equal(100, available.Descriptors[0].PriceModifier);
		Assert.Equal(NpcDialogServiceSelectStatus.TradeInUnavailable, unavailable.Status);
		Assert.Equal(NpcDialogServiceDescriptorKind.SystemMessageDoesNotSellItem, Assert.Single(unavailable.Descriptors).Kind);
	}

	[Fact]
	public void CreatePlan_DefaultQuestIdZeroActionFallsBackToNextPageDescriptor()
	{
		var plan = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(CreateFallback(dialogActionId: 9999)));

		Assert.Equal(NpcDialogServiceSelectStatus.QuestEngineOrNextPage, plan.Status);
		Assert.True(plan.CallsQuestEngine);
		Assert.Equal(2, plan.Descriptors.Count);
		Assert.Equal(NpcDialogServiceDescriptorKind.DialogWindowNextPage, plan.Descriptors[1].Kind);
	}

	private static NpcDialogServiceFallbackDescriptor CreateFallback(
		int dialogActionId,
		int targetObjectId = 9001,
		int questId = 0,
		int extendedRewardIndex = 0)
	{
		return new NpcDialogServiceFallbackDescriptor(
			targetObjectId,
			dialogActionId,
			questId,
			extendedRewardIndex,
			"DialogService.onDialogSelect(dialogActionId, player, getOwner(), questId, extendedRewardIndex)",
			IsLive: false);
	}
}
