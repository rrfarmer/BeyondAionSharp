namespace Aion.GameServer.Services;

public enum CmBuyItemRepurchaseReadPlanStatus
{
	PlanCreated,
	AuditAmountOutOfRange,
	AuditInvalidItem,
}

public sealed record CmBuyItemReadItem(
	int ItemObjectId,
	long Count);

public sealed record CmBuyItemRepurchaseReadPlan(
	CmBuyItemRepurchaseReadPlanStatus Status,
	int SellerObjectId,
	int TradeActionId,
	int DeclaredAmount,
	IReadOnlyList<CmBuyItemReadItem> ProcessedItems,
	IReadOnlyList<int> RepurchaseItemObjectIds,
	CmBuyItemReadItem? AuditItem,
	string JavaSource)
{
	public bool IsLive => false;

	public bool IsRepurchaseAction => TradeActionId == CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId;
}

public static class CmBuyItemRepurchaseReadPlanService
{
	public const int RepurchaseTradeActionId = 2;
	public const int MaxItemAmount = 36;
	public const long MaxItemCount = 20_000;

	public static CmBuyItemRepurchaseReadPlan CreatePlan(
		int sellerObjectId,
		int tradeActionId,
		int declaredAmount,
		IReadOnlyList<CmBuyItemReadItem> readItems,
		IReadOnlySet<int> repurchasableItemObjectIds)
	{
		// Java parity: network/aion/clientpackets/CM_BUY_ITEM.readImpl and model/trade/RepurchaseList.addRepurchaseItem.
		if (declaredAmount < 0 || declaredAmount > MaxItemAmount)
			return new CmBuyItemRepurchaseReadPlan(
				CmBuyItemRepurchaseReadPlanStatus.AuditAmountOutOfRange,
				sellerObjectId,
				tradeActionId,
				declaredAmount,
				ProcessedItems: Array.Empty<CmBuyItemReadItem>(),
				RepurchaseItemObjectIds: Array.Empty<int>(),
				AuditItem: null,
				"CM_BUY_ITEM.readImpl -> amount < 0 || amount > 36 -> isAudit=true and return");

		var processedItems = new List<CmBuyItemReadItem>();
		var repurchaseItemObjectIds = new List<int>();
		var repurchaseSeen = new HashSet<int>();

		foreach (var item in readItems.Take(declaredAmount))
		{
			processedItems.Add(item);
			if (item.Count < 0 || (item.ItemObjectId <= 0 && tradeActionId != 0) || item.Count > MaxItemCount)
				return new CmBuyItemRepurchaseReadPlan(
					CmBuyItemRepurchaseReadPlanStatus.AuditInvalidItem,
					sellerObjectId,
					tradeActionId,
					declaredAmount,
					processedItems.ToArray(),
					RepurchaseItemObjectIds: Array.Empty<int>(),
					AuditItem: item,
					"CM_BUY_ITEM.readImpl -> count < 0 || (itemId <= 0 && tradeActionId != 0) || count > 20000 -> isAudit=true and break; runImpl returns");

			if (tradeActionId == RepurchaseTradeActionId
				&& repurchasableItemObjectIds.Contains(item.ItemObjectId)
				&& repurchaseSeen.Add(item.ItemObjectId))
			{
				repurchaseItemObjectIds.Add(item.ItemObjectId);
			}
		}

		return new CmBuyItemRepurchaseReadPlan(
			CmBuyItemRepurchaseReadPlanStatus.PlanCreated,
			sellerObjectId,
			tradeActionId,
			declaredAmount,
			processedItems.ToArray(),
			repurchaseItemObjectIds.ToArray(),
			AuditItem: null,
			"CM_BUY_ITEM.readImpl -> action 2 creates RepurchaseList; RepurchaseList.addRepurchaseItem filters by RepurchaseService.canRepurchase and LinkedHashSet preserves first-seen order");
	}
}
