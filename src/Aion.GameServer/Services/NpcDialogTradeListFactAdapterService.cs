using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed record NpcDialogTradeListFactAdapterInput(
	int NpcId,
	int PlayerLegionLevel = 0,
	int VendorBuyModifier = 100);

public sealed record NpcDialogTradeListFactAdapterPlan(
	NpcDialogServiceSelectFacts Facts,
	TradeListTemplateSummary? TradeList,
	TradeListTemplateSummary? TradeInList,
	IReadOnlyList<int> MissingGoodsListIds,
	IReadOnlyList<int> RestrictedGoodsListIds,
	string JavaSource,
	bool IsLive = false);

public static class NpcDialogTradeListFactAdapterService
{
	public static NpcDialogTradeListFactAdapterPlan CreatePlan(
		NpcDialogTradeListFactAdapterInput input,
		TradeListTable tradeLists,
		GoodsListTable goodsLists)
	{
		// Java parity breadcrumbs:
		// - services/DialogService.onDialogSelect BUY checks TradeListData.getTradeListTemplate,
		//   GoodsListData.getGoodsListById, and GoodsList.legion_lvl against player legion level.
		// - services/DialogService.onDialogSelect TRADE_IN only checks TradeListData.getTradeInListTemplate.
		var tradeList = tradeLists.GetTradeListTemplate(input.NpcId);
		var tradeInList = tradeLists.GetTradeInListTemplate(input.NpcId);
		var missingGoodsListIds = new List<int>();
		var restrictedGoodsListIds = new List<int>();
		var hasSellableTradeGoods = false;

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

				if (goodsList.LegionLevel > input.PlayerLegionLevel)
				{
					restrictedGoodsListIds.Add(goodsListId);
					continue;
				}

				hasSellableTradeGoods = true;
			}
		}

		return new NpcDialogTradeListFactAdapterPlan(
			new NpcDialogServiceSelectFacts(
				HasTradeList: tradeList != null,
				HasSellableTradeGoods: hasSellableTradeGoods,
				VendorBuyModifier: input.VendorBuyModifier,
				TradeSellPriceRate: tradeList?.SellPriceRate ?? 100,
				HasTradeInList: tradeInList != null),
			tradeList,
			tradeInList,
			missingGoodsListIds.AsReadOnly(),
			restrictedGoodsListIds.AsReadOnly(),
			"DialogService.onDialogSelect BUY/TRADE_IN -> TradeListData + GoodsListData",
			IsLive: false);
	}
}

