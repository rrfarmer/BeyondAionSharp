using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed class LimitedItemTradeService
{
	private readonly Dictionary<int, List<LimitedItemRuntimeState>> _limitedTradeNpcs;
	private readonly object _sync = new();

	private LimitedItemTradeService(Dictionary<int, List<LimitedItemRuntimeState>> limitedTradeNpcs)
	{
		_limitedTradeNpcs = limitedTradeNpcs;
	}

	public static LimitedItemTradeService Empty { get; } = new(new Dictionary<int, List<LimitedItemRuntimeState>>());

	public static LimitedItemTradeService Create(TradeListTable tradeLists, GoodsListTable goodsLists)
	{
		ArgumentNullException.ThrowIfNull(tradeLists);
		ArgumentNullException.ThrowIfNull(goodsLists);

		// Java parity: services/LimitedItemTradeService.start scans TradeListData trade lists,
		// resolves each tab's GoodsList, then stores GoodsList.getLimitedItems per NPC.
		var limitedTradeNpcs = new Dictionary<int, List<LimitedItemRuntimeState>>();
		foreach (var tradeList in tradeLists.TradeLists)
		{
			foreach (var goodsListId in tradeList.GoodsListIds)
			{
				var goodsList = goodsLists.GetGoodsListById(goodsListId);
				if (goodsList == null)
					continue;

				foreach (var item in goodsList.ItemSummaries)
				{
					if (!item.IsLimitedItem)
						continue;

					if (!limitedTradeNpcs.TryGetValue(tradeList.NpcId, out var limitedItems))
					{
						limitedItems = [];
						limitedTradeNpcs[tradeList.NpcId] = limitedItems;
					}

					limitedItems.Add(
						new LimitedItemRuntimeState(
							item.Id,
							item.SellLimit!.Value,
							item.BuyLimit!.Value,
							goodsList.SalesTime));
				}
			}
		}

		return new LimitedItemTradeService(limitedTradeNpcs);
	}

	public IReadOnlyList<NpcDialogLimitedItemFact> GetLimitedItemFacts(int npcId, int playerObjectId)
	{
		lock (_sync)
		{
			if (!_limitedTradeNpcs.TryGetValue(npcId, out var limitedItems))
				return Array.Empty<NpcDialogLimitedItemFact>();

			return limitedItems
				.Select(item => item.ToFact(playerObjectId))
				.ToArray();
		}
	}

	public bool CanBuy(int npcId, int itemId, int playerObjectId, long count)
	{
		lock (_sync)
		{
			var item = FindLimitedItem(npcId, itemId);
			return item == null || item.CanBuy(playerObjectId, count);
		}
	}

	public LimitedItemBuyMutation? BuyItem(int npcId, int itemId, int playerObjectId, long count)
	{
		lock (_sync)
		{
			var item = FindLimitedItem(npcId, itemId);
			return item?.Buy(playerObjectId, count);
		}
	}

	private LimitedItemRuntimeState? FindLimitedItem(int npcId, int itemId)
	{
		if (!_limitedTradeNpcs.TryGetValue(npcId, out var limitedItems))
			return null;
		return limitedItems.FirstOrDefault(item => item.ItemId == itemId);
	}
}

public sealed record LimitedItemBuyMutation(
	int ItemId,
	int? PlayerBuyCount,
	int? SellLimit);

internal sealed class LimitedItemRuntimeState
{
	private readonly Dictionary<int, int> _buyCounts = new();

	public LimitedItemRuntimeState(int itemId, int sellLimit, int buyLimit, string? salesTime)
	{
		ItemId = itemId;
		SellLimit = sellLimit;
		BuyLimit = buyLimit;
		DefaultSellLimit = sellLimit;
		SalesTime = salesTime;
	}

	public int ItemId { get; }

	public int SellLimit { get; private set; }

	public int BuyLimit { get; }

	public int DefaultSellLimit { get; }

	public string? SalesTime { get; }

	public NpcDialogLimitedItemFact ToFact(int playerObjectId)
	{
		return new NpcDialogLimitedItemFact(
			ItemId,
			SellLimit,
			BuyLimit,
			_buyCounts.GetValueOrDefault(playerObjectId),
			SalesTime);
	}

	public bool CanBuy(int playerObjectId, long count)
	{
		// Java parity: TradeService.canBuyLimitItem checks current sellLimit and
		// LimitedItem.getBuyCount(playerObjectId) against the requested tradeItem count.
		if (SellLimit > 0 && SellLimit - count < 0)
			return false;
		if (BuyLimit > 0 && _buyCounts.GetValueOrDefault(playerObjectId) + count > BuyLimit)
			return false;
		return true;
	}

	public LimitedItemBuyMutation Buy(int playerObjectId, long count)
	{
		// Java parity: TradeService.performBuyTransaction final loop calls
		// LimitedItem.setBuyCount and setSellLimit after ItemService.addItem succeeds.
		var playerBuyCount = _buyCounts.GetValueOrDefault(playerObjectId);
		int? updatedBuyCount = null;
		if (BuyLimit > 0)
		{
			updatedBuyCount = checked(playerBuyCount + (int)count);
			_buyCounts[playerObjectId] = updatedBuyCount.Value;
		}

		int? updatedSellLimit = null;
		if (DefaultSellLimit > 0)
		{
			updatedSellLimit = checked(SellLimit - (int)count);
			SellLimit = updatedSellLimit.Value;
		}

		return new LimitedItemBuyMutation(ItemId, updatedBuyCount, updatedSellLimit);
	}
}
