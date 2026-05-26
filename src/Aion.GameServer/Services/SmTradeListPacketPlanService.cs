using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public enum SmTradeListPacketPlanStatus
{
	Ready,
	UnknownTradeNpcType,
}

public sealed record SmTradeListPacketPlanInput(
	int TargetObjectId,
	int PlayerObjectId,
	TradeListTemplateSummary TradeList,
	GoodsListTable GoodsLists,
	int PlayerLegionLevel = 0,
	bool NpcCanSell = true,
	bool NpcCanBuy = true,
	int BuyPriceModifier = 100,
	IReadOnlyList<SmTradeListLimitedItemSummary>? LimitedItems = null);

public sealed record SmTradeListLimitedItemSummary(int ItemId, int BuyCount, int SellLimit);

public sealed record SmTradeListPacketWriteField(string Type, string Name, int Value);

public sealed record SmTradeListPacketPlan(
	SmTradeListPacketPlanStatus Status,
	int TargetObjectId,
	int PlayerObjectId,
	int TradeNpcTypeIndex,
	int BuyPriceModifier,
	int FixedAion45Modifier,
	bool ShowBuyTab,
	bool ShowSellTab,
	IReadOnlyList<int> TradeTabIds,
	IReadOnlyList<SmTradeListLimitedItemSummary> LimitedItems,
	IReadOnlyList<int> MissingGoodsListIds,
	IReadOnlyList<int> RestrictedGoodsListIds,
	IReadOnlyList<SmTradeListPacketWriteField> JavaWriteOrder,
	string JavaSource,
	bool IsLive = false);

public static class SmTradeListPacketPlanService
{
	private const int FixedAion45Modifier = 100;

	public static SmTradeListPacketPlan CreatePlan(SmTradeListPacketPlanInput input)
	{
		ArgumentNullException.ThrowIfNull(input.TradeList);
		ArgumentNullException.ThrowIfNull(input.GoodsLists);

		// Java parity breadcrumbs:
		// - network/aion/serverpackets/SM_TRADELIST constructor filters TradeTab entries by
		//   GoodsListData.getGoodsListById and GoodsList.legion_lvl <= player legion level.
		// - SM_TRADELIST.writeImpl writes object id, TradeNpcType.index(), buy modifier,
		//   the fixed 4.5 modifier 100, buy/sell tab flags, visible tab ids, and limited items.
		var status = TryGetTradeNpcTypeIndex(input.TradeList.NpcType, out var tradeNpcTypeIndex)
			? SmTradeListPacketPlanStatus.Ready
			: SmTradeListPacketPlanStatus.UnknownTradeNpcType;
		var tradeTabIds = new List<int>();
		var missingGoodsListIds = new List<int>();
		var restrictedGoodsListIds = new List<int>();

		foreach (var goodsListId in input.TradeList.GoodsListIds)
		{
			var goodsList = input.GoodsLists.GetGoodsListById(goodsListId);
			if (goodsList == null)
			{
				missingGoodsListIds.Add(goodsListId);
				continue;
			}

			if (goodsList.LegionLevel > input.PlayerLegionLevel)
			{
				restrictedGoodsListIds.Add(goodsListId);
				continue;
			}

			tradeTabIds.Add(goodsListId);
		}

		var limitedItems = input.LimitedItems ?? Array.Empty<SmTradeListLimitedItemSummary>();
		var javaWriteOrder = CreateWriteOrder(
			input.TargetObjectId,
			tradeNpcTypeIndex,
			input.BuyPriceModifier,
			input.NpcCanSell,
			input.NpcCanBuy,
			tradeTabIds,
			limitedItems);

		return new SmTradeListPacketPlan(
			status,
			input.TargetObjectId,
			input.PlayerObjectId,
			tradeNpcTypeIndex,
			input.BuyPriceModifier,
			FixedAion45Modifier,
			input.NpcCanSell,
			input.NpcCanBuy,
			tradeTabIds.AsReadOnly(),
			limitedItems,
			missingGoodsListIds.AsReadOnly(),
			restrictedGoodsListIds.AsReadOnly(),
			javaWriteOrder,
			"SM_TRADELIST(Player, Npc, TradeListTemplate, int) + writeImpl",
			IsLive: false);
	}

	private static IReadOnlyList<SmTradeListPacketWriteField> CreateWriteOrder(
		int targetObjectId,
		int tradeNpcTypeIndex,
		int buyPriceModifier,
		bool showBuyTab,
		bool showSellTab,
		IReadOnlyList<int> tradeTabIds,
		IReadOnlyList<SmTradeListLimitedItemSummary> limitedItems)
	{
		var fields = new List<SmTradeListPacketWriteField>
		{
			new("D", "targetObjId", targetObjectId),
			new("C", "tradeNpcType.index", tradeNpcTypeIndex),
			new("D", "buyPriceModifier", buyPriceModifier),
			new("D", "fixedAion45Modifier", FixedAion45Modifier),
			new("C", "showBuyTab", showBuyTab ? 1 : 0),
			new("C", "showSellTab", showSellTab ? 1 : 0),
			new("H", "tradeTabCount", tradeTabIds.Count),
		};

		foreach (var tradeTabId in tradeTabIds)
		{
			fields.Add(new SmTradeListPacketWriteField("D", "tradeTabId", tradeTabId));
		}

		fields.Add(new SmTradeListPacketWriteField("H", "limitedItemCount", limitedItems.Count));
		foreach (var limitedItem in limitedItems)
		{
			fields.Add(new SmTradeListPacketWriteField("D", "limitedItem.itemId", limitedItem.ItemId));
			fields.Add(new SmTradeListPacketWriteField("H", "limitedItem.buyCount", limitedItem.BuyCount));
			fields.Add(new SmTradeListPacketWriteField("H", "limitedItem.sellLimit", limitedItem.SellLimit));
		}

		return fields.AsReadOnly();
	}

	private static bool TryGetTradeNpcTypeIndex(string npcType, out int index)
	{
		index = npcType switch
		{
			"NORMAL" => 1,
			"ABYSS" => 2,
			"LEGION_COIN" => 3,
			"REWARD" => 4,
			"ABYSS_KINAH" => 5,
			_ => 0,
		};

		return index != 0;
	}
}
