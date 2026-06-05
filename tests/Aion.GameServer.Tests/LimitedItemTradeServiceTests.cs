using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class LimitedItemTradeServiceTests
{
	[Fact]
	public void BuyItem_UpdatesPlayerBuyCountAndSellLimitLikeJava()
	{
		var service = LimitedItemTradeService.Create(
			CreateBuyTradeLists(new TradeListTemplateSummary(700001, [501])),
			CreateBuyGoodsLists(new GoodsListSummary(
				501,
				SalesTime: "0 0 9 ? * MON",
				Items: [new GoodsListItemSummary(1001, SellLimit: 3, BuyLimit: 2)])));

		Assert.True(service.CanBuy(700001, 1001, playerObjectId: 1001, count: 1));

		var mutation = service.BuyItem(700001, 1001, playerObjectId: 1001, count: 1);

		Assert.NotNull(mutation);
		Assert.Equal((1001, 1, 2), (mutation!.ItemId, mutation.PlayerBuyCount, mutation.SellLimit));
		var fact = Assert.Single(service.GetLimitedItemFacts(700001, playerObjectId: 1001));
		Assert.Equal(new NpcDialogLimitedItemFact(1001, SellLimit: 2, BuyLimit: 2, PlayerBuyCount: 1, SalesTime: "0 0 9 ? * MON"), fact);
		Assert.True(service.CanBuy(700001, 1001, playerObjectId: 1001, count: 1));
		Assert.False(service.CanBuy(700001, 1001, playerObjectId: 1001, count: 2));
	}

	[Fact]
	public void BuyItem_NoOpsForNonLimitedItemLikeJavaMissingLimitedItem()
	{
		var service = LimitedItemTradeService.Create(
			CreateBuyTradeLists(new TradeListTemplateSummary(700001, [501])),
			CreateBuyGoodsLists(new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])));

		Assert.True(service.CanBuy(700001, 1001, playerObjectId: 1001, count: 99));
		Assert.Null(service.BuyItem(700001, 1001, playerObjectId: 1001, count: 99));
		Assert.Empty(service.GetLimitedItemFacts(700001, playerObjectId: 1001));
	}

	private static TradeListTable CreateBuyTradeLists(params TradeListTemplateSummary[] tradeLists)
	{
		return new TradeListTable(
			tradeLists,
			Array.Empty<TradeListTemplateSummary>(),
			Array.Empty<TradeListTemplateSummary>());
	}

	private static GoodsListTable CreateBuyGoodsLists(params GoodsListSummary[] tradeLists)
	{
		return new GoodsListTable(
			tradeLists,
			Array.Empty<GoodsListSummary>(),
			Array.Empty<GoodsListSummary>());
	}
}
