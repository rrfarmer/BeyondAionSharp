using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class NpcDialogLimitedItemFactAdapterServiceTests
{
	[Fact]
	public void CreatePlan_CollectsLimitedItemsFromNpcTradeTabsLikeJavaStartup()
	{
		var plan = NpcDialogLimitedItemFactAdapterService.CreatePlan(
			new NpcDialogLimitedItemFactAdapterInput(
				NpcId: 203060,
				PlayerObjectId: 42,
				PlayerBuyCountsByItemId: new Dictionary<int, int> { [186000001] = 2 }),
			CreateTradeLists(
				new TradeListTemplateSummary(203060, [129, 130, 131])),
			CreateGoodsLists(
				new GoodsListSummary(
					129,
					SalesTime: "0 0 9 ? * MON",
					Items:
					[
						new GoodsListItemSummary(110100010),
						new GoodsListItemSummary(186000001, SellLimit: 5, BuyLimit: 3),
					]),
				new GoodsListSummary(
					130,
					SalesTime: "0 0 10 ? * TUE",
					Items:
					[
						new GoodsListItemSummary(186000002, SellLimit: 7),
						new GoodsListItemSummary(186000003, BuyLimit: 4),
						new GoodsListItemSummary(186000004, SellLimit: 9, BuyLimit: 1),
					])));

		Assert.False(plan.IsLive);
		Assert.Equal([131], plan.MissingGoodsListIds);
		Assert.Equal(
			[
				new NpcDialogLimitedItemFact(
					ItemId: 186000001,
					SellLimit: 5,
					BuyLimit: 3,
					PlayerBuyCount: 2,
					SalesTime: "0 0 9 ? * MON"),
				new NpcDialogLimitedItemFact(
					ItemId: 186000004,
					SellLimit: 9,
					BuyLimit: 1,
					PlayerBuyCount: 0,
					SalesTime: "0 0 10 ? * TUE"),
			],
			plan.LimitedItems);
		Assert.Equal(
			[
				new SmTradeListLimitedItemSummary(ItemId: 186000001, BuyCount: 2, SellLimit: 5),
				new SmTradeListLimitedItemSummary(ItemId: 186000004, BuyCount: 0, SellLimit: 9),
			],
			plan.PacketItems);
	}

	[Fact]
	public void CreatePlan_ReturnsEmptyWhenNpcHasNoTradeList()
	{
		var plan = NpcDialogLimitedItemFactAdapterService.CreatePlan(
			new NpcDialogLimitedItemFactAdapterInput(NpcId: 203061, PlayerObjectId: 42),
			CreateTradeLists(new TradeListTemplateSummary(203060, [129])),
			CreateGoodsLists(new GoodsListSummary(129)));

		Assert.Empty(plan.LimitedItems);
		Assert.Empty(plan.MissingGoodsListIds);
	}

	private static TradeListTable CreateTradeLists(params TradeListTemplateSummary[] tradeLists)
	{
		return new TradeListTable(tradeLists, Array.Empty<TradeListTemplateSummary>(), Array.Empty<TradeListTemplateSummary>());
	}

	private static GoodsListTable CreateGoodsLists(params GoodsListSummary[] goodsLists)
	{
		return new GoodsListTable(goodsLists, Array.Empty<GoodsListSummary>(), Array.Empty<GoodsListSummary>());
	}
}
