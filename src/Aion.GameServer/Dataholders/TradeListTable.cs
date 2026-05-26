using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class TradeListTable
{
	private readonly IReadOnlyDictionary<int, TradeListTemplateSummary> _tradeListsByNpcId;
	private readonly IReadOnlyDictionary<int, TradeListTemplateSummary> _tradeInListsByNpcId;
	private readonly IReadOnlyDictionary<int, TradeListTemplateSummary> _purchaseListsByNpcId;

	public TradeListTable(
		IReadOnlyList<TradeListTemplateSummary> tradeLists,
		IReadOnlyList<TradeListTemplateSummary> tradeInLists,
		IReadOnlyList<TradeListTemplateSummary> purchaseLists)
	{
		TradeLists = tradeLists;
		TradeInLists = tradeInLists;
		PurchaseLists = purchaseLists;
		_tradeListsByNpcId = ToJavaNpcIdIndex(tradeLists);
		_tradeInListsByNpcId = ToJavaNpcIdIndex(tradeInLists);
		_purchaseListsByNpcId = ToJavaNpcIdIndex(purchaseLists);
	}

	public IReadOnlyList<TradeListTemplateSummary> TradeLists { get; }

	public IReadOnlyList<TradeListTemplateSummary> TradeInLists { get; }

	public IReadOnlyList<TradeListTemplateSummary> PurchaseLists { get; }

	public int TradeListCount => _tradeListsByNpcId.Count;

	public int TradeInListCount => _tradeInListsByNpcId.Count;

	public int PurchaseListCount => _purchaseListsByNpcId.Count;

	public TradeListTemplateSummary? GetTradeListTemplate(int npcId)
	{
		// Java parity: dataholders/TradeListData.getTradeListTemplate.
		return _tradeListsByNpcId.GetValueOrDefault(npcId);
	}

	public TradeListTemplateSummary? GetTradeInListTemplate(int npcId)
	{
		// Java parity: dataholders/TradeListData.getTradeInListTemplate.
		return _tradeInListsByNpcId.GetValueOrDefault(npcId);
	}

	public TradeListTemplateSummary? GetPurchaseTemplate(int npcId)
	{
		// Java parity: dataholders/TradeListData.getPurchaseTemplate.
		return _purchaseListsByNpcId.GetValueOrDefault(npcId);
	}

	private static IReadOnlyDictionary<int, TradeListTemplateSummary> ToJavaNpcIdIndex(
		IReadOnlyList<TradeListTemplateSummary> templates)
	{
		var byNpcId = new Dictionary<int, TradeListTemplateSummary>();
		foreach (var template in templates)
		{
			// Java parity: TradeListData.afterUnmarshal puts by npc_id; duplicate ids are last-write-wins.
			byNpcId[template.NpcId] = template;
		}

		return new ReadOnlyDictionary<int, TradeListTemplateSummary>(byNpcId);
	}
}

public sealed record TradeListTemplateSummary(
	int NpcId,
	IReadOnlyList<int> GoodsListIds,
	string NpcType = "NORMAL",
	int SellPriceRate = 100,
	int SellPriceRate2 = 100,
	int ApSellPriceRate2 = 100,
	int BuyPriceRate = 0,
	int SaveCount = 0);

public sealed class GoodsListTable
{
	private readonly IReadOnlyDictionary<int, GoodsListSummary> _goodsListsById;
	private readonly IReadOnlyDictionary<int, GoodsListSummary> _goodsInListsById;
	private readonly IReadOnlyDictionary<int, GoodsListSummary> _goodsPurchaseListsById;

	public GoodsListTable(
		IReadOnlyList<GoodsListSummary> goodsLists,
		IReadOnlyList<GoodsListSummary> goodsInLists,
		IReadOnlyList<GoodsListSummary> goodsPurchaseLists)
	{
		GoodsLists = goodsLists;
		GoodsInLists = goodsInLists;
		GoodsPurchaseLists = goodsPurchaseLists;
		_goodsListsById = ToJavaIdIndex(goodsLists);
		_goodsInListsById = ToJavaIdIndex(goodsInLists);
		_goodsPurchaseListsById = ToJavaIdIndex(goodsPurchaseLists);
	}

	public IReadOnlyList<GoodsListSummary> GoodsLists { get; }

	public IReadOnlyList<GoodsListSummary> GoodsInLists { get; }

	public IReadOnlyList<GoodsListSummary> GoodsPurchaseLists { get; }

	public int Count => _goodsListsById.Count + _goodsInListsById.Count + _goodsPurchaseListsById.Count;

	public GoodsListSummary? GetGoodsListById(int id)
	{
		// Java parity: dataholders/GoodsListData.getGoodsListById.
		return _goodsListsById.GetValueOrDefault(id);
	}

	public GoodsListSummary? GetGoodsInListById(int id)
	{
		// Java parity: dataholders/GoodsListData.getGoodsInListById.
		return _goodsInListsById.GetValueOrDefault(id);
	}

	public GoodsListSummary? GetGoodsPurchaseListById(int id)
	{
		// Java parity: dataholders/GoodsListData.getGoodsPurchaseListById.
		return _goodsPurchaseListsById.GetValueOrDefault(id);
	}

	private static IReadOnlyDictionary<int, GoodsListSummary> ToJavaIdIndex(IReadOnlyList<GoodsListSummary> goodsLists)
	{
		var byId = new Dictionary<int, GoodsListSummary>();
		foreach (var goodsList in goodsLists)
		{
			// Java parity: GoodsListData.afterUnmarshal puts by list id; duplicate ids are last-write-wins.
			byId[goodsList.Id] = goodsList;
		}

		return new ReadOnlyDictionary<int, GoodsListSummary>(byId);
	}
}

public sealed record GoodsListSummary(
	int Id,
	int LegionLevel = 0,
	string? SalesTime = null,
	IReadOnlyList<GoodsListItemSummary>? Items = null)
{
	public IReadOnlyList<GoodsListItemSummary> ItemSummaries { get; } = Items ?? Array.Empty<GoodsListItemSummary>();
}

public sealed record GoodsListItemSummary(
	int Id,
	int? SellLimit = null,
	int? BuyLimit = null)
{
	public bool IsLimitedItem => SellLimit.HasValue && BuyLimit.HasValue;
}
