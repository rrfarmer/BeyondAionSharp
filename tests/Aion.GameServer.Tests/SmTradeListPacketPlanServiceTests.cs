using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SmTradeListPacketPlanServiceTests
{
	[Fact]
	public void CreatePlan_FiltersTradeTabsAndModelsJavaWriteOrder()
	{
		var plan = SmTradeListPacketPlanService.CreatePlan(
			new SmTradeListPacketPlanInput(
				TargetObjectId: 9001,
				PlayerObjectId: 42,
				TradeList: new TradeListTemplateSummary(
					203060,
					[129, 130, 131],
					NpcType: "ABYSS"),
				GoodsLists: CreateGoodsLists(
					new GoodsListSummary(129, LegionLevel: 0),
					new GoodsListSummary(130, LegionLevel: 3)),
				PlayerLegionLevel: 0,
				NpcCanSell: true,
				NpcCanBuy: false,
				BuyPriceModifier: 125,
				LimitedItems:
				[
					new SmTradeListLimitedItemSummary(186000001, BuyCount: 2, SellLimit: 5),
				]));

		Assert.Equal(SmTradeListPacketPlanStatus.Ready, plan.Status);
		Assert.Equal(2, plan.TradeNpcTypeIndex);
		Assert.Equal([129], plan.TradeTabIds);
		Assert.Equal([131], plan.MissingGoodsListIds);
		Assert.Equal([130], plan.RestrictedGoodsListIds);
		Assert.True(plan.ShowBuyTab);
		Assert.False(plan.ShowSellTab);
		Assert.False(plan.IsLive);
		Assert.Equal(
			[
				new SmTradeListPacketWriteField("D", "targetObjId", 9001),
				new SmTradeListPacketWriteField("C", "tradeNpcType.index", 2),
				new SmTradeListPacketWriteField("D", "buyPriceModifier", 125),
				new SmTradeListPacketWriteField("D", "fixedAion45Modifier", 100),
				new SmTradeListPacketWriteField("C", "showBuyTab", 1),
				new SmTradeListPacketWriteField("C", "showSellTab", 0),
				new SmTradeListPacketWriteField("H", "tradeTabCount", 1),
				new SmTradeListPacketWriteField("D", "tradeTabId", 129),
				new SmTradeListPacketWriteField("H", "limitedItemCount", 1),
				new SmTradeListPacketWriteField("D", "limitedItem.itemId", 186000001),
				new SmTradeListPacketWriteField("H", "limitedItem.buyCount", 2),
				new SmTradeListPacketWriteField("H", "limitedItem.sellLimit", 5),
			],
			plan.JavaWriteOrder);
	}

	[Theory]
	[InlineData("NORMAL", 1)]
	[InlineData("ABYSS", 2)]
	[InlineData("LEGION_COIN", 3)]
	[InlineData("REWARD", 4)]
	[InlineData("ABYSS_KINAH", 5)]
	public void CreatePlan_MapsJavaTradeNpcTypeIndexes(string npcType, int expectedIndex)
	{
		var plan = SmTradeListPacketPlanService.CreatePlan(
			new SmTradeListPacketPlanInput(
				TargetObjectId: 9001,
				PlayerObjectId: 42,
				TradeList: new TradeListTemplateSummary(203060, [129], NpcType: npcType),
				GoodsLists: CreateGoodsLists(new GoodsListSummary(129))));

		Assert.Equal(SmTradeListPacketPlanStatus.Ready, plan.Status);
		Assert.Equal(expectedIndex, plan.TradeNpcTypeIndex);
		Assert.Equal(expectedIndex, plan.JavaWriteOrder[1].Value);
	}

	[Fact]
	public void CreatePlan_ReportsUnknownTradeNpcTypeWithoutClaimingReady()
	{
		var plan = SmTradeListPacketPlanService.CreatePlan(
			new SmTradeListPacketPlanInput(
				TargetObjectId: 9001,
				PlayerObjectId: 42,
				TradeList: new TradeListTemplateSummary(203060, [129], NpcType: "CUSTOM"),
				GoodsLists: CreateGoodsLists(new GoodsListSummary(129))));

		Assert.Equal(SmTradeListPacketPlanStatus.UnknownTradeNpcType, plan.Status);
		Assert.Equal(0, plan.TradeNpcTypeIndex);
		Assert.False(plan.IsLive);
	}

	private static GoodsListTable CreateGoodsLists(params GoodsListSummary[] goodsLists)
	{
		return new GoodsListTable(goodsLists, Array.Empty<GoodsListSummary>(), Array.Empty<GoodsListSummary>());
	}
}
