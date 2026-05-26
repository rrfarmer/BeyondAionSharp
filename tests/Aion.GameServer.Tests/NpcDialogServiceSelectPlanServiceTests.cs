using Aion.GameServer.Dataholders;
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
		var packetPlan = SmTradeListPacketPlanService.CreatePlan(
			new SmTradeListPacketPlanInput(
				TargetObjectId: 9001,
				PlayerObjectId: 42,
				TradeList: new TradeListTemplateSummary(203060, [129], SellPriceRate: 80),
				GoodsLists: CreateGoodsLists(new GoodsListSummary(129)),
				BuyPriceModifier: 100));
		var plan = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(
				CreateFallback(dialogActionId: 2),
				HasTradeList: true,
				HasSellableTradeGoods: true,
				VendorBuyModifier: 125,
				TradeSellPriceRate: 80,
				TradeListPacketPlan: packetPlan));

		Assert.Equal(NpcDialogServiceSelectStatus.BuyTradeList, plan.Status);
		var descriptor = Assert.Single(plan.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.TradeListPacket, descriptor.Kind);
		Assert.Equal(100, descriptor.PriceModifier);
		Assert.Same(packetPlan, descriptor.TradeListPacketPlan);
		Assert.NotNull(descriptor.TradeListPacketPlan);
		Assert.False(descriptor.TradeListPacketPlan.IsLive);
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
		var packetPlan = SmTradeInListPacketPlanService.CreatePlan(
			new SmTradeInListPacketPlanInput(
				TargetObjectId: 9001,
				TradeInList: new TradeListTemplateSummary(205315, [39])));
		var available = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(
				CreateFallback(dialogActionId: 78),
				HasTradeInList: true,
				TradeInListPacketPlan: packetPlan));
		var unavailable = NpcDialogServiceSelectPlanService.CreatePlan(
			new NpcDialogServiceSelectInput(CreateFallback(dialogActionId: 78), HasTradeInList: false));

		Assert.Equal(NpcDialogServiceSelectStatus.TradeInList, available.Status);
		var descriptor = Assert.Single(available.Descriptors);
		Assert.Equal(NpcDialogServiceDescriptorKind.TradeInListPacket, descriptor.Kind);
		Assert.Equal(100, descriptor.PriceModifier);
		Assert.Same(packetPlan, descriptor.TradeInListPacketPlan);
		Assert.False(descriptor.TradeInListPacketPlan!.IsLive);
		Assert.Equal(NpcDialogServiceSelectStatus.TradeInUnavailable, unavailable.Status);
		Assert.Equal(NpcDialogServiceDescriptorKind.SystemMessageDoesNotSellItem, Assert.Single(unavailable.Descriptors).Kind);
		Assert.Null(unavailable.Descriptors[0].TradeInListPacketPlan);
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

	private static GoodsListTable CreateGoodsLists(params GoodsListSummary[] goodsLists)
	{
		return new GoodsListTable(goodsLists, Array.Empty<GoodsListSummary>(), Array.Empty<GoodsListSummary>());
	}
}
