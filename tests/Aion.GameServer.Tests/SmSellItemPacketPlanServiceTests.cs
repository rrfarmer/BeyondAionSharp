using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SmSellItemPacketPlanServiceTests
{
	[Fact]
	public void CreatePlan_UsesJavaPurchaseTemplateFields()
	{
		var plan = SmSellItemPacketPlanService.CreatePlan(
			new SmSellItemPacketPlanInput(
				TargetObjectId: 7001,
				PurchaseTemplate: new TradeListTemplateSummary(
					NpcId: 203060,
					GoodsListIds: [129, 130],
					NpcType: "ABYSS",
					BuyPriceRate: 175),
				NpcCanSell: true,
				NpcCanBuy: false,
				NpcCanPurchase: true));

		Assert.Equal(SmSellItemPacketPlanStatus.Ready, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Equal(7001, plan.TargetObjectId);
		Assert.Equal(2, plan.TradeNpcTypeIndex);
		Assert.Equal(175, plan.BuyPriceRate);
		Assert.True(plan.ShowBuyTab);
		Assert.True(plan.ShowSellTab);
		Assert.Equal([129, 130], plan.TradeTabIds);
		Assert.Contains("SM_SELL_ITEM", plan.JavaSource, StringComparison.Ordinal);
		Assert.Equal(
			["targetObjectId", "tradeNpcType.index", "buyPriceRate", "showBuyTab", "showSellTab", "tradeTabCount", "tradeTabId", "tradeTabId"],
			plan.JavaWriteOrder.Select(field => field.Name).ToArray());
	}

	[Fact]
	public void CreatePlan_UsesVendorSellModifierWhenPurchaseTemplateMissing()
	{
		var plan = SmSellItemPacketPlanService.CreatePlan(
			new SmSellItemPacketPlanInput(
				TargetObjectId: 7001,
				PurchaseTemplate: null,
				NpcCanSell: false,
				NpcCanBuy: true,
				NpcCanPurchase: false,
				PriceOptions: new GameServerPriceOptions { VendorSellModifier = 22 }));

		Assert.Equal(SmSellItemPacketPlanStatus.Ready, plan.Status);
		Assert.Equal(1, plan.TradeNpcTypeIndex);
		Assert.Equal(22, plan.BuyPriceRate);
		Assert.False(plan.ShowBuyTab);
		Assert.True(plan.ShowSellTab);
		Assert.Empty(plan.TradeTabIds);
	}

	[Fact]
	public void CreatePlan_ReportsUnknownTradeNpcType()
	{
		var plan = SmSellItemPacketPlanService.CreatePlan(
			new SmSellItemPacketPlanInput(
				TargetObjectId: 7001,
				PurchaseTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "ALIEN", BuyPriceRate: 100),
				NpcCanSell: true,
				NpcCanBuy: true,
				NpcCanPurchase: false));

		Assert.Equal(SmSellItemPacketPlanStatus.UnknownTradeNpcType, plan.Status);
		Assert.Equal(0, plan.TradeNpcTypeIndex);
	}
}
