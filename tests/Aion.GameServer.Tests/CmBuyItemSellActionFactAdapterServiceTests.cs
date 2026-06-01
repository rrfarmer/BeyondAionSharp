using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemSellActionFactAdapterServiceTests
{
	[Fact]
	public void CreatePlan_SkipsBeforeLookupWhenNpcCannotBuyOrPurchase()
	{
		var plan = CmBuyItemSellActionFactAdapterService.CreatePlan(
			new CmBuyItemSellActionFactAdapterInput(
				NpcId: 203060,
				NpcCanBuy: false,
				NpcCanPurchase: false),
			CreateTradeLists(purchaseLists: [new TradeListTemplateSummary(203060, [129], NpcType: "ABYSS")]));

		Assert.Equal(CmBuyItemSellActionFactAdapterPlanStatus.SkippedNpcCannotBuyOrPurchase, plan.Status);
		Assert.Equal([CmBuyItemSellActionFactAdapterStep.CheckNpcCanBuyOrPurchase], plan.Steps);
		Assert.Null(plan.PurchaseTemplate);
		Assert.False(plan.DispatchesAbyssApSell);
		Assert.False(plan.ShouldHydrateSellToShopPlan);
		Assert.False(plan.ShouldHydrateSellForApToShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_ClassifiesAbyssPurchaseTemplateForApSell()
	{
		var purchaseTemplate = new TradeListTemplateSummary(203060, [129], NpcType: "ABYSS", BuyPriceRate: 35);

		var plan = CmBuyItemSellActionFactAdapterService.CreatePlan(
			new CmBuyItemSellActionFactAdapterInput(
				NpcId: 203060,
				NpcCanBuy: false,
				NpcCanPurchase: true),
			CreateTradeLists(purchaseLists: [purchaseTemplate]));

		Assert.Equal(CmBuyItemSellActionFactAdapterPlanStatus.ClassifiedSellForApToShop, plan.Status);
		Assert.Equal(
			[
				CmBuyItemSellActionFactAdapterStep.CheckNpcCanBuyOrPurchase,
				CmBuyItemSellActionFactAdapterStep.LookupPurchaseTemplate,
				CmBuyItemSellActionFactAdapterStep.ClassifyPurchaseTemplateType,
			],
			plan.Steps);
		Assert.Same(purchaseTemplate, plan.PurchaseTemplate);
		Assert.True(plan.DispatchesAbyssApSell);
		Assert.False(plan.ShouldHydrateSellToShopPlan);
		Assert.True(plan.ShouldHydrateSellForApToShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsLive);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("NORMAL")]
	public void CreatePlan_ClassifiesMissingOrNormalPurchaseTemplateAsNormalSell(string? npcType)
	{
		var purchaseLists = npcType is null
			? Array.Empty<TradeListTemplateSummary>()
			: [new TradeListTemplateSummary(203060, [129], NpcType: npcType)];

		var plan = CmBuyItemSellActionFactAdapterService.CreatePlan(
			new CmBuyItemSellActionFactAdapterInput(
				NpcId: 203060,
				NpcCanBuy: true,
				NpcCanPurchase: false),
			CreateTradeLists(purchaseLists: purchaseLists));

		Assert.Equal(CmBuyItemSellActionFactAdapterPlanStatus.ClassifiedNormalSellToShop, plan.Status);
		Assert.False(plan.DispatchesAbyssApSell);
		Assert.True(plan.ShouldHydrateSellToShopPlan);
		Assert.False(plan.ShouldHydrateSellForApToShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsLive);
		Assert.Contains(CmBuyItemSellActionFactAdapterStep.LookupPurchaseTemplate, plan.Steps);
		Assert.Contains(CmBuyItemSellActionFactAdapterStep.ClassifyPurchaseTemplateType, plan.Steps);
	}

	private static TradeListTable CreateTradeLists(
		IReadOnlyList<TradeListTemplateSummary>? purchaseLists = null)
	{
		return new TradeListTable(
			Array.Empty<TradeListTemplateSummary>(),
			Array.Empty<TradeListTemplateSummary>(),
			purchaseLists ?? Array.Empty<TradeListTemplateSummary>());
	}
}
