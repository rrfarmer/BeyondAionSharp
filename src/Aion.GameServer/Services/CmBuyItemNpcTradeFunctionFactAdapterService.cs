using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public enum CmBuyItemNpcTradeFunctionFactAdapterStatus
{
	Resolved,
}

public sealed record CmBuyItemNpcTradeFunctionFactAdapterPlan(
	CmBuyItemNpcTradeFunctionFactAdapterStatus Status,
	int NpcId,
	bool NpcCanSell,
	bool NpcCanBuy,
	bool NpcCanPurchase,
	string JavaSource,
	bool IsLive = false);

public static class CmBuyItemNpcTradeFunctionFactAdapterService
{
	private const int BuyDialogAction = 2;
	private const int SellDialogAction = 3;
	private const int TradeSellListDialogAction = 103;

	public static CmBuyItemNpcTradeFunctionFactAdapterPlan CreatePlan(
		NpcTemplateSummary template,
		TradeListTable tradeLists)
	{
		// Java parity: model/gameobjects/Npc.canSell/canBuy/canPurchase.
		var npcId = template.TemplateId;
		var canSell = tradeLists.GetTradeListTemplate(npcId) != null
			&& template.SupportsDialogAction(BuyDialogAction);
		var canBuy = template.SupportsDialogAction(SellDialogAction) || canSell;
		var canPurchase = tradeLists.GetPurchaseTemplate(npcId) != null
			&& template.SupportsDialogAction(TradeSellListDialogAction);

		return new CmBuyItemNpcTradeFunctionFactAdapterPlan(
			CmBuyItemNpcTradeFunctionFactAdapterStatus.Resolved,
			npcId,
			canSell,
			canBuy,
			canPurchase,
			"Npc.canSell -> trade list + DialogAction.BUY; Npc.canBuy -> DialogAction.SELL || canSell; Npc.canPurchase -> purchase template + DialogAction.TRADE_SELL_LIST",
			IsLive: false);
	}
}
