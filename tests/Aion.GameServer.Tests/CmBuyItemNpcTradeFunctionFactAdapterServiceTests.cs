using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemNpcTradeFunctionFactAdapterServiceTests
{
	[Fact]
	public void CreatePlan_DerivesCanSellFromTradeListAndBuyDialogAction()
	{
		var plan = CmBuyItemNpcTradeFunctionFactAdapterService.CreatePlan(
			Template(203060, functionDialogIds: [2]),
			CreateTradeLists(tradeLists: [new TradeListTemplateSummary(203060, [1])]));

		Assert.Equal(CmBuyItemNpcTradeFunctionFactAdapterStatus.Resolved, plan.Status);
		Assert.True(plan.NpcCanSell);
		Assert.True(plan.NpcCanBuy);
		Assert.False(plan.NpcCanPurchase);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_DerivesCanBuyFromSellDialogActionWithoutTradeList()
	{
		var plan = CmBuyItemNpcTradeFunctionFactAdapterService.CreatePlan(
			Template(203060, functionDialogIds: [3]),
			CreateTradeLists());

		Assert.False(plan.NpcCanSell);
		Assert.True(plan.NpcCanBuy);
		Assert.False(plan.NpcCanPurchase);
	}

	[Fact]
	public void CreatePlan_DerivesCanPurchaseFromPurchaseTemplateAndTradeSellListAction()
	{
		var plan = CmBuyItemNpcTradeFunctionFactAdapterService.CreatePlan(
			Template(203060, functionDialogIds: [103]),
			CreateTradeLists(purchaseLists: [new TradeListTemplateSummary(203060, [129])]));

		Assert.False(plan.NpcCanSell);
		Assert.False(plan.NpcCanBuy);
		Assert.True(plan.NpcCanPurchase);
	}

	[Fact]
	public void CreatePlan_RequiresBothTemplateFunctionAndTradeDataForSellAndPurchase()
	{
		var missingFunctions = CmBuyItemNpcTradeFunctionFactAdapterService.CreatePlan(
			Template(203060),
			CreateTradeLists(
				tradeLists: [new TradeListTemplateSummary(203060, [1])],
				purchaseLists: [new TradeListTemplateSummary(203060, [129])]));
		var missingTradeData = CmBuyItemNpcTradeFunctionFactAdapterService.CreatePlan(
			Template(203060, functionDialogIds: [2, 103]),
			CreateTradeLists());

		Assert.False(missingFunctions.NpcCanSell);
		Assert.False(missingFunctions.NpcCanBuy);
		Assert.False(missingFunctions.NpcCanPurchase);
		Assert.False(missingTradeData.NpcCanSell);
		Assert.False(missingTradeData.NpcCanBuy);
		Assert.False(missingTradeData.NpcCanPurchase);
	}

	private static NpcTemplateSummary Template(int npcId, IReadOnlyList<int>? functionDialogIds = null)
	{
		return new NpcTemplateSummary(
			npcId,
			$"Npc {npcId}",
			NameId: 0,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "NONE",
			Type: "NPC",
			FunctionDialogIds: functionDialogIds);
	}

	private static TradeListTable CreateTradeLists(
		IReadOnlyList<TradeListTemplateSummary>? tradeLists = null,
		IReadOnlyList<TradeListTemplateSummary>? purchaseLists = null)
	{
		return new TradeListTable(
			tradeLists ?? Array.Empty<TradeListTemplateSummary>(),
			Array.Empty<TradeListTemplateSummary>(),
			purchaseLists ?? Array.Empty<TradeListTemplateSummary>());
	}
}
