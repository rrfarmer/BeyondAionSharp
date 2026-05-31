using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public enum PrivateStoreBoughtItemsPlanStatus
{
	PlanCreated,
	BlockedInvalidStoreIndex,
	BlockedCountExceedsStoreItemCount,
}

public sealed record PrivateStoreListedItemSummary(
	int StoreIndex,
	int ItemObjectId,
	int ItemId,
	long Count,
	long PricePerItem,
	string? ItemName);

public sealed record PrivateStoreBoughtItemsPlan(
	PrivateStoreBoughtItemsPlanStatus Status,
	IReadOnlyList<PrivateStorePurchaseItemRequest> BoughtItems,
	int? InvalidStoreIndex,
	long? RequestedCount,
	long? AvailableCount,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class PrivateStoreBoughtItemsPlanService
{
	public static PrivateStoreBoughtItemsPlan CreatePlan(
		IReadOnlyList<CmBuyItemEntry> tradeItems,
		IReadOnlyList<PrivateStoreListedItemSummary> storeItems)
	{
		// Java parity: PrivateStoreService.getBoughtItems. For action 0, CM_BUY_ITEM
		// stores private-shop list indices in TradeItem.itemId.
		var boughtItems = new List<PrivateStorePurchaseItemRequest>();
		foreach (var tradeItem in tradeItems)
		{
			var storeIndex = tradeItem.ItemObjectId;
			if (storeIndex < 0 || storeIndex >= storeItems.Count)
				return new PrivateStoreBoughtItemsPlan(
					PrivateStoreBoughtItemsPlanStatus.BlockedInvalidStoreIndex,
					Array.Empty<PrivateStorePurchaseItemRequest>(),
					storeIndex,
					tradeItem.Count,
					AvailableCount: null,
					"PrivateStoreService.getBoughtItems -> invalid store index warning and return null");

			var storeItem = storeItems[storeIndex];
			if (tradeItem.Count > storeItem.Count)
				return new PrivateStoreBoughtItemsPlan(
					PrivateStoreBoughtItemsPlanStatus.BlockedCountExceedsStoreItemCount,
					Array.Empty<PrivateStorePurchaseItemRequest>(),
					storeIndex,
					tradeItem.Count,
					storeItem.Count,
					"PrivateStoreService.getBoughtItems -> requested count > store item count warning and return null");

			boughtItems.Add(new PrivateStorePurchaseItemRequest(
				storeIndex,
				storeItem.ItemObjectId,
				storeItem.ItemId,
				tradeItem.Count,
				storeItem.PricePerItem,
				storeItem.ItemName));
		}

		return new PrivateStoreBoughtItemsPlan(
			PrivateStoreBoughtItemsPlanStatus.PlanCreated,
			boughtItems,
			InvalidStoreIndex: null,
			RequestedCount: null,
			AvailableCount: null,
			"PrivateStoreService.getBoughtItems -> index-based store item lookup preserves private store insertion order");
	}
}
