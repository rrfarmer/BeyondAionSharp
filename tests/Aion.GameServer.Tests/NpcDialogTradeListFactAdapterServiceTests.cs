using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcDialogTradeListFactAdapterServiceTests
{
	[Fact]
	public void CreatePlan_ReportsNoTradeGoodsWhenNpcHasNoTradeList()
	{
		var plan = NpcDialogTradeListFactAdapterService.CreatePlan(
			new NpcDialogTradeListFactAdapterInput(NpcId: 203060),
			CreateTradeLists(),
			CreateGoodsLists(new GoodsListSummary(129)));

		Assert.False(plan.Facts.HasTradeList);
		Assert.False(plan.Facts.HasSellableTradeGoods);
		Assert.Equal(100, plan.Facts.TradeSellPriceRate);
		Assert.Empty(plan.MissingGoodsListIds);
	}

	[Fact]
	public void CreatePlan_ReportsMissingGoodsListsWithoutMarkingSellable()
	{
		var plan = NpcDialogTradeListFactAdapterService.CreatePlan(
			new NpcDialogTradeListFactAdapterInput(NpcId: 203060),
			CreateTradeLists(tradeLists: [new TradeListTemplateSummary(203060, [129, 130])]),
			CreateGoodsLists(new GoodsListSummary(129, LegionLevel: 5)));

		Assert.True(plan.Facts.HasTradeList);
		Assert.False(plan.Facts.HasSellableTradeGoods);
		Assert.Equal([130], plan.MissingGoodsListIds);
		Assert.Equal([129], plan.RestrictedGoodsListIds);
	}

	[Fact]
	public void CreatePlan_MarksBuySellableWhenAnyGoodsListPassesLegionLevel()
	{
		var plan = NpcDialogTradeListFactAdapterService.CreatePlan(
			new NpcDialogTradeListFactAdapterInput(NpcId: 203060, PlayerLegionLevel: 3, VendorBuyModifier: 125),
			CreateTradeLists(tradeLists: [new TradeListTemplateSummary(203060, [129, 130], SellPriceRate: 80)]),
			CreateGoodsLists(new GoodsListSummary(129, LegionLevel: 4), new GoodsListSummary(130, LegionLevel: 3)));

		Assert.True(plan.Facts.HasTradeList);
		Assert.True(plan.Facts.HasSellableTradeGoods);
		Assert.Equal(125, plan.Facts.VendorBuyModifier);
		Assert.Equal(80, plan.Facts.TradeSellPriceRate);
		Assert.Equal([129], plan.RestrictedGoodsListIds);
	}

	[Fact]
	public void CreatePlan_TradeInAvailabilityDoesNotRequireGoodsInList()
	{
		var plan = NpcDialogTradeListFactAdapterService.CreatePlan(
			new NpcDialogTradeListFactAdapterInput(NpcId: 205315),
			CreateTradeLists(tradeInLists: [new TradeListTemplateSummary(205315, [39])]),
			CreateGoodsLists());

		Assert.True(plan.Facts.HasTradeInList);
		Assert.NotNull(plan.TradeInList);
		Assert.False(plan.Facts.HasTradeList);
		Assert.False(plan.Facts.HasSellableTradeGoods);
	}

	private static TradeListTable CreateTradeLists(
		IReadOnlyList<TradeListTemplateSummary>? tradeLists = null,
		IReadOnlyList<TradeListTemplateSummary>? tradeInLists = null,
		IReadOnlyList<TradeListTemplateSummary>? purchaseLists = null)
	{
		return new TradeListTable(
			tradeLists ?? Array.Empty<TradeListTemplateSummary>(),
			tradeInLists ?? Array.Empty<TradeListTemplateSummary>(),
			purchaseLists ?? Array.Empty<TradeListTemplateSummary>());
	}

	private static GoodsListTable CreateGoodsLists(params GoodsListSummary[] goodsLists)
	{
		return new GoodsListTable(goodsLists, Array.Empty<GoodsListSummary>(), Array.Empty<GoodsListSummary>());
	}
}

