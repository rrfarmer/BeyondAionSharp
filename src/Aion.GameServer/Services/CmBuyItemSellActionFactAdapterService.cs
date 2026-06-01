using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public enum CmBuyItemSellActionFactAdapterPlanStatus
{
	SkippedNpcCannotBuyOrPurchase,
	ClassifiedNormalSellToShop,
	ClassifiedSellForApToShop,
}

public enum CmBuyItemSellActionFactAdapterStep
{
	CheckNpcCanBuyOrPurchase,
	LookupPurchaseTemplate,
	ClassifyPurchaseTemplateType,
}

public sealed record CmBuyItemSellActionFactAdapterInput(
	int NpcId,
	bool NpcCanBuy,
	bool NpcCanPurchase);

public sealed record CmBuyItemSellActionFactAdapterPlan(
	CmBuyItemSellActionFactAdapterPlanStatus Status,
	IReadOnlyList<CmBuyItemSellActionFactAdapterStep> Steps,
	TradeListTemplateSummary? PurchaseTemplate,
	bool DispatchesAbyssApSell,
	bool ShouldHydrateSellToShopPlan,
	bool ShouldHydrateSellForApToShopPlan,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive = false);

public static class CmBuyItemSellActionFactAdapterService
{
	public static CmBuyItemSellActionFactAdapterPlan CreatePlan(
		CmBuyItemSellActionFactAdapterInput input,
		TradeListTable tradeLists)
	{
		// Java parity: CM_BUY_ITEM.runImpl action 1 checks npc.canBuy() || npc.canPurchase(),
		// then reads DataManager.TRADE_LIST_DATA.getPurchaseTemplate(npc.getNpcId()) and
		// dispatches AP sell only when the purchase template type is TradeNpcType.ABYSS.
		var steps = new List<CmBuyItemSellActionFactAdapterStep>
		{
			CmBuyItemSellActionFactAdapterStep.CheckNpcCanBuyOrPurchase,
		};

		if (!input.NpcCanBuy && !input.NpcCanPurchase)
			return new CmBuyItemSellActionFactAdapterPlan(
				CmBuyItemSellActionFactAdapterPlanStatus.SkippedNpcCannotBuyOrPurchase,
				steps,
				PurchaseTemplate: null,
				DispatchesAbyssApSell: false,
				ShouldHydrateSellToShopPlan: false,
				ShouldHydrateSellForApToShopPlan: false,
				ShouldDispatchLiveSideEffects: false,
				"CM_BUY_ITEM.runImpl action 1 -> if (npc.canBuy() || npc.canPurchase())",
				IsLive: false);

		steps.Add(CmBuyItemSellActionFactAdapterStep.LookupPurchaseTemplate);
		var purchaseTemplate = tradeLists.GetPurchaseTemplate(input.NpcId);

		steps.Add(CmBuyItemSellActionFactAdapterStep.ClassifyPurchaseTemplateType);
		var dispatchesAbyssApSell = string.Equals(purchaseTemplate?.NpcType, "ABYSS", StringComparison.Ordinal);

		return new CmBuyItemSellActionFactAdapterPlan(
			dispatchesAbyssApSell
				? CmBuyItemSellActionFactAdapterPlanStatus.ClassifiedSellForApToShop
				: CmBuyItemSellActionFactAdapterPlanStatus.ClassifiedNormalSellToShop,
			steps,
			purchaseTemplate,
			dispatchesAbyssApSell,
			ShouldHydrateSellToShopPlan: !dispatchesAbyssApSell,
			ShouldHydrateSellForApToShopPlan: dispatchesAbyssApSell,
			ShouldDispatchLiveSideEffects: false,
			dispatchesAbyssApSell
				? "CM_BUY_ITEM.runImpl action 1 -> purchaseTemplate TradeNpcType.ABYSS -> TradeService.performSellForAPToShop"
				: "CM_BUY_ITEM.runImpl action 1 -> TradeService.performSellToShop(player, tradeList, tradeTemplate)",
			IsLive: false);
	}
}
