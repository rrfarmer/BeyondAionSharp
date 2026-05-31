using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum TradeSellForApToShopPlanStatus
{
	PlanCreated,
	BlockedSellingApItemsDisabled,
	BlockedCannotTrade,
	BlockedMissingItem,
	BlockedMissingTemplate,
	BlockedInvalidPurchaseItem,
}

public enum TradeSellForApToShopStep
{
	CheckSellingApItemsEnabled,
	CheckPlayerCanTrade,
	FindInventoryItem,
	ValidatePurchaseTemplateGoods,
	PlanInventoryDecrease,
	PlanAbyssPointReward,
}

public sealed record TradeSellForApToShopItemRequest(
	int ItemObjectId,
	long Count,
	bool InventoryDecreaseSucceeds = true);

public sealed record TradeSellForApToShopApReward(
	int ItemObjectId,
	int ItemId,
	long Count,
	int RequiredApPerItem,
	int ApReward);

public sealed record TradeSellForApToShopPlan(
	TradeSellForApToShopPlanStatus Status,
	IReadOnlyList<TradeSellForApToShopStep> Steps,
	IReadOnlyList<int> DeletedItemObjectIds,
	IReadOnlyList<int> SkippedDeleteFailedItemObjectIds,
	IReadOnlyList<TradeSellForApToShopApReward> AbyssPointRewards,
	int TotalAbyssPoints,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	int? RejectedItemObjectId = null)
{
	public bool IsLive => false;
}

public enum TradeSellForApToShopOutcomePlanStatus
{
	MissingSellForApToShopPlan,
	SellForApToShopPlanNotReady,
	DisabledNoTransaction,
}

public enum TradeSellForApToShopOutcomeStepKind
{
	PersistRepositoryWrites,
	DispatchPacketIntents,
	CommitTransactionBoundary,
}

public sealed record TradeSellForApToShopOutcomeStepPlan(
	TradeSellForApToShopOutcomeStepKind Kind,
	bool WouldRun,
	bool DidRun,
	string JavaSource);

public sealed record TradeSellForApToShopOutcomePlan(
	TradeSellForApToShopOutcomePlanStatus Status,
	TradeSellForApToShopPlan? SellForApToShopPlan,
	IReadOnlyList<TradeSellForApToShopOutcomeStepPlan> Steps,
	bool WouldWritePersistence,
	bool DidWritePersistence,
	bool WouldMutateSellerInventory,
	bool DidMutateSellerInventory,
	bool WouldMutateAbyssPoints,
	bool DidMutateAbyssPoints,
	bool WouldSendPackets,
	bool DidSendPackets,
	bool WouldCommitTransactionBoundary,
	bool DidCommitTransactionBoundary,
	bool ShouldCommitTransactionBoundary,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public static class TradeSellForApToShopPlanService
{
	public static TradeSellForApToShopPlan CreatePlan(
		bool sellingApItemsEnabled,
		bool canTrade,
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<TradeSellForApToShopItemRequest> tradeItems,
		ItemTemplateTable itemTemplates,
		TradeListTemplateSummary purchaseTemplate,
		GoodsListTable goodsLists)
	{
		// Java parity: services/TradeService.performSellForAPToShop.
		// This planner models the decision order and AP reward intent only; it never
		// mutates inventory, AP, packets, audit logs, or repositories.
		var steps = new List<TradeSellForApToShopStep>
		{
			TradeSellForApToShopStep.CheckSellingApItemsEnabled,
		};

		if (!sellingApItemsEnabled)
			return Block(
				TradeSellForApToShopPlanStatus.BlockedSellingApItemsDisabled,
				steps,
				"TradeService.performSellForAPToShop -> !CustomConfig.SELLING_APITEMS_ENABLED -> send message and false");

		steps.Add(TradeSellForApToShopStep.CheckPlayerCanTrade);
		if (!canTrade)
			return Block(
				TradeSellForApToShopPlanStatus.BlockedCannotTrade,
				steps,
				"TradeService.performSellForAPToShop -> !PlayerRestrictions.canTrade(player) -> false");

		var deletedItemObjectIds = new List<int>();
		var skippedDeleteFailedItemObjectIds = new List<int>();
		var apRewards = new List<TradeSellForApToShopApReward>();

		foreach (var tradeItem in tradeItems)
		{
			steps.Add(TradeSellForApToShopStep.FindInventoryItem);
			var item = inventoryItems.FirstOrDefault(candidate => candidate.ObjectId == tradeItem.ItemObjectId);
			if (item == null)
				return Block(
					TradeSellForApToShopPlanStatus.BlockedMissingItem,
					steps,
					"TradeService.performSellForAPToShop -> inventory.getItemByObjId(...) == null -> false",
					tradeItem.ItemObjectId);

			var template = itemTemplates.GetItemTemplate(item.ItemId);
			if (template == null)
				return Block(
					TradeSellForApToShopPlanStatus.BlockedMissingTemplate,
					steps,
					"TradeService.performSellForAPToShop -> item.getItemTemplate().getAcquisition(); non-live planner blocks missing template before mutation",
					tradeItem.ItemObjectId);

			steps.Add(TradeSellForApToShopStep.ValidatePurchaseTemplateGoods);
			if (!IsAllowedPurchaseItem(purchaseTemplate, goodsLists, item.ItemId))
				return Block(
					TradeSellForApToShopPlanStatus.BlockedInvalidPurchaseItem,
					steps,
					"TradeService.performSellForAPToShop -> item id not found in purchase template goods list -> false",
					tradeItem.ItemObjectId);

			steps.Add(TradeSellForApToShopStep.PlanInventoryDecrease);
			if (!tradeItem.InventoryDecreaseSucceeds)
			{
				skippedDeleteFailedItemObjectIds.Add(tradeItem.ItemObjectId);
				continue;
			}

			deletedItemObjectIds.Add(tradeItem.ItemObjectId);
			steps.Add(TradeSellForApToShopStep.PlanAbyssPointReward);
			var apReward = TradeApFormulaService.CalculateApResaleReward(
				template.RequiredAbyssPoints,
				purchaseTemplate.BuyPriceRate,
				tradeItem.Count);
			apRewards.Add(new TradeSellForApToShopApReward(
				tradeItem.ItemObjectId,
				item.ItemId,
				tradeItem.Count,
				template.RequiredAbyssPoints,
				apReward));
		}

		return new TradeSellForApToShopPlan(
			TradeSellForApToShopPlanStatus.PlanCreated,
			steps,
			deletedItemObjectIds,
			skippedDeleteFailedItemObjectIds,
			apRewards,
			apRewards.Sum(reward => reward.ApReward),
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performSellForAPToShop -> decreaseByObjectId succeeds per item -> AbyssPointsService.addAp(apToAdd * (int) count)");
	}

	private static bool IsAllowedPurchaseItem(
		TradeListTemplateSummary purchaseTemplate,
		GoodsListTable goodsLists,
		int itemId)
	{
		return purchaseTemplate.GoodsListIds
			.Select(goodsLists.GetGoodsPurchaseListById)
			.Where(goodsList => goodsList != null)
			.Any(goodsList => goodsList!.ItemSummaries.Any(item => item.Id == itemId));
	}

	private static TradeSellForApToShopPlan Block(
		TradeSellForApToShopPlanStatus status,
		IReadOnlyList<TradeSellForApToShopStep> steps,
		string javaSource,
		int? rejectedItemObjectId = null)
	{
		return new TradeSellForApToShopPlan(
			status,
			steps.ToArray(),
			DeletedItemObjectIds: Array.Empty<int>(),
			SkippedDeleteFailedItemObjectIds: Array.Empty<int>(),
			AbyssPointRewards: Array.Empty<TradeSellForApToShopApReward>(),
			TotalAbyssPoints: 0,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			rejectedItemObjectId);
	}
}

public static class TradeSellForApToShopOutcomePlanService
{
	public static TradeSellForApToShopOutcomePlan CreateDisabledPlan(TradeSellForApToShopPlan? sellForApToShopPlan)
	{
		if (sellForApToShopPlan == null)
			return CreateTerminalPlan(
				TradeSellForApToShopOutcomePlanStatus.MissingSellForApToShopPlan,
				sellForApToShopPlan,
				"TradeService.performSellForAPToShop final outcome requires a sell-for-AP-to-shop mutation plan");

		if (sellForApToShopPlan.Status == TradeSellForApToShopPlanStatus.BlockedSellingApItemsDisabled)
		{
			var disabledMessageStep = Disabled(
				TradeSellForApToShopOutcomeStepKind.DispatchPacketIntents,
				"TradeService.performSellForAPToShop -> !CustomConfig.SELLING_APITEMS_ENABLED sends disabled message and returns false");

			return new TradeSellForApToShopOutcomePlan(
				TradeSellForApToShopOutcomePlanStatus.DisabledNoTransaction,
				sellForApToShopPlan,
				[disabledMessageStep],
				WouldWritePersistence: false,
				DidWritePersistence: false,
				WouldMutateSellerInventory: false,
				DidMutateSellerInventory: false,
				WouldMutateAbyssPoints: false,
				DidMutateAbyssPoints: false,
				WouldSendPackets: true,
				DidSendPackets: false,
				WouldCommitTransactionBoundary: false,
				DidCommitTransactionBoundary: false,
				ShouldCommitTransactionBoundary: false,
				ShouldDispatchLiveSideEffects: false,
				"TradeService.performSellForAPToShop disabled-feature outcome records the Java message send without dispatch",
				IsLive: false);
		}

		if (sellForApToShopPlan.Status != TradeSellForApToShopPlanStatus.PlanCreated)
			return CreateTerminalPlan(
				TradeSellForApToShopOutcomePlanStatus.SellForApToShopPlanNotReady,
				sellForApToShopPlan,
				"TradeService.performSellForAPToShop final outcome stops because the AP-sell plan is blocked before mutation");

		var wouldMutateSellerInventory = sellForApToShopPlan.DeletedItemObjectIds.Count > 0;
		var wouldMutateAbyssPoints = sellForApToShopPlan.AbyssPointRewards.Count > 0;
		var wouldWritePersistence = wouldMutateSellerInventory || wouldMutateAbyssPoints;
		var wouldSendPackets = wouldMutateSellerInventory || wouldMutateAbyssPoints;
		var wouldCommitBoundary = wouldWritePersistence || wouldSendPackets;

		var steps = new List<TradeSellForApToShopOutcomeStepPlan>();
		if (wouldWritePersistence)
			steps.Add(Disabled(
				TradeSellForApToShopOutcomeStepKind.PersistRepositoryWrites,
				"TradeService.performSellForAPToShop -> inventory.decreaseByObjectId and AbyssPointsService.addAp persist item/AP state"));
		if (wouldSendPackets)
			steps.Add(Disabled(
				TradeSellForApToShopOutcomeStepKind.DispatchPacketIntents,
				"TradeService.performSellForAPToShop -> inventory decrease and AbyssPointsService.addAp emit item/AP packet intents"));
		if (wouldCommitBoundary)
			steps.Add(Disabled(
				TradeSellForApToShopOutcomeStepKind.CommitTransactionBoundary,
				"TradeService.performSellForAPToShop transaction boundary is recorded only; Java runtime transaction semantics are not yet verified"));

		return new TradeSellForApToShopOutcomePlan(
			TradeSellForApToShopOutcomePlanStatus.DisabledNoTransaction,
			sellForApToShopPlan,
			steps,
			wouldWritePersistence,
			DidWritePersistence: false,
			wouldMutateSellerInventory,
			DidMutateSellerInventory: false,
			wouldMutateAbyssPoints,
			DidMutateAbyssPoints: false,
			wouldSendPackets,
			DidSendPackets: false,
			wouldCommitBoundary,
			DidCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performSellForAPToShop final outcome is disabled; item/AP writes and sends are recorded without dispatch",
			IsLive: false);
	}

	private static TradeSellForApToShopOutcomePlan CreateTerminalPlan(
		TradeSellForApToShopOutcomePlanStatus status,
		TradeSellForApToShopPlan? sellForApToShopPlan,
		string javaSource) =>
		new(
			status,
			sellForApToShopPlan,
			Steps: Array.Empty<TradeSellForApToShopOutcomeStepPlan>(),
			WouldWritePersistence: false,
			DidWritePersistence: false,
			WouldMutateSellerInventory: false,
			DidMutateSellerInventory: false,
			WouldMutateAbyssPoints: false,
			DidMutateAbyssPoints: false,
			WouldSendPackets: false,
			DidSendPackets: false,
			WouldCommitTransactionBoundary: false,
			DidCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);

	private static TradeSellForApToShopOutcomeStepPlan Disabled(
		TradeSellForApToShopOutcomeStepKind kind,
		string javaSource) =>
		new(kind, WouldRun: true, DidRun: false, javaSource);
}
