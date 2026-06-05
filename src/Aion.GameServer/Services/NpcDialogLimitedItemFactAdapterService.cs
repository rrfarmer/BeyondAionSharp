using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed record NpcDialogLimitedItemFactAdapterInput(
	int NpcId,
	int PlayerObjectId,
	IReadOnlyDictionary<int, int>? PlayerBuyCountsByItemId = null,
	IReadOnlyDictionary<int, int>? SellLimitsByItemId = null,
	IReadOnlyList<NpcDialogLimitedItemFact>? LiveLimitedItems = null
);

public sealed record NpcDialogLimitedItemFact(int ItemId, int SellLimit, int BuyLimit, int PlayerBuyCount, string? SalesTime);

public sealed record NpcDialogLimitedItemFactAdapterPlan(
	IReadOnlyList<NpcDialogLimitedItemFact> LimitedItems,
	IReadOnlyList<int> MissingGoodsListIds,
	string JavaSource,
	bool IsLive = false
)
{
	public IReadOnlyList<SmTradeListLimitedItemSummary> PacketItems { get; } =
		LimitedItems.Select(item => new SmTradeListLimitedItemSummary(item.ItemId, item.PlayerBuyCount, item.SellLimit)).ToArray();
}

public static class NpcDialogLimitedItemFactAdapterService
{
	public static NpcDialogLimitedItemFactAdapterPlan CreatePlan(
		NpcDialogLimitedItemFactAdapterInput input,
		TradeListTable tradeLists,
		GoodsListTable goodsLists
	)
	{
		ArgumentNullException.ThrowIfNull(tradeLists);
		ArgumentNullException.ThrowIfNull(goodsLists);

		// Java parity:
		// LimitedItemTradeService.start scans TradeListData.getTradeListTemplate(),
		// GoodsListData.getGoodsListById(tab.id), and GoodsList.getLimitedItems().
		if (input.LiveLimitedItems != null)
		{
			return new NpcDialogLimitedItemFactAdapterPlan(
				input.LiveLimitedItems,
				Array.Empty<int>(),
				"LimitedItemTradeService.start + LimitedItem.getBuyCount",
				IsLive: true);
		}

		var tradeList = tradeLists.GetTradeListTemplate(input.NpcId);
		var limitedItems = new List<NpcDialogLimitedItemFact>();
		var missingGoodsListIds = new List<int>();
		var buyCounts = input.PlayerBuyCountsByItemId ?? new Dictionary<int, int>();
		var sellLimits = input.SellLimitsByItemId ?? new Dictionary<int, int>();

		if (tradeList != null)
		{
			foreach (var goodsListId in tradeList.GoodsListIds)
			{
				var goodsList = goodsLists.GetGoodsListById(goodsListId);
				if (goodsList == null)
				{
					missingGoodsListIds.Add(goodsListId);
					continue;
				}

				foreach (var item in goodsList.ItemSummaries)
				{
					if (!item.IsLimitedItem)
						continue;

					limitedItems.Add(
						new NpcDialogLimitedItemFact(
							item.Id,
							sellLimits.GetValueOrDefault(item.Id, item.SellLimit!.Value),
							item.BuyLimit!.Value,
							buyCounts.GetValueOrDefault(item.Id),
							goodsList.SalesTime
						)
					);
				}
			}
		}

		return new NpcDialogLimitedItemFactAdapterPlan(
			limitedItems.AsReadOnly(),
			missingGoodsListIds.AsReadOnly(),
			"LimitedItemTradeService.start + LimitedItem.getBuyCount",
			IsLive: false
		);
	}
}
