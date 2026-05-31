using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum TradeSellToShopPlanStatus
{
	PlanCreated,
	BlockedCannotTrade,
	BlockedMissingItem,
	BlockedMissingTemplate,
	BlockedInvalidPurchaseItem,
	BlockedNotSellable,
	BlockedCountExceedsAvailable,
	BlockedRepurchaseItemCreateFailed,
	BlockedKinahAddFailed,
}

public sealed record TradeSellToShopItemRequest(
	int ItemObjectId,
	long Count,
	bool IsSellable = true,
	long? SellLimitAdjustedCount = null);

public sealed record TradeSellToShopPlan(
	TradeSellToShopPlanStatus Status,
	IReadOnlyList<int> SellerDeletedItemObjectIds,
	IReadOnlyList<InventoryItem> SellerItemUpdates,
	IReadOnlyList<RepurchaseSourceItem> RepurchaseItems,
	InventoryItem? KinahUpdate,
	string JavaSource)
{
	public bool IsLive => false;
}

public enum PetMerchantSellLiveExecutorFacadeStatus
{
	MissingHandlerPlan,
	HandlerNotPetMerchantSell,
	MissingSellModifier,
	MissingSellToShopPlan,
	SellToShopPlanNotReady,
	DisabledNoSideEffects,
}

public enum PetMerchantSellLiveExecutorOperationKind
{
	ApplySellerInventoryMutation,
	AddRepurchaseItems,
	IncreaseKinah,
}

public enum PetMerchantSellLiveExecutorOperationStatus
{
	NotAttemptedMissingPlan,
	NotAttemptedNotPetMerchantSell,
	NotAttemptedCompositionNotReady,
	NotAttemptedDisabled,
}

public enum PetMerchantSellOutcomePlanStatus
{
	MissingFacadePlan,
	FacadeNotReady,
	DisabledNoTransaction,
}

public enum PetMerchantSellOutcomeStepKind
{
	ApplySellerInventoryMutation,
	AddRepurchaseItems,
	IncreaseKinah,
	CommitTransactionBoundary,
}

public sealed record PetMerchantSellLiveExecutorOperation(
	PetMerchantSellLiveExecutorOperationKind Kind,
	PetMerchantSellLiveExecutorOperationStatus Status,
	string JavaSource);

public sealed record PetMerchantSellOutcomeStepPlan(
	PetMerchantSellOutcomeStepKind Kind,
	bool WouldRun,
	bool DidRun,
	string JavaSource);

public sealed record PetMerchantSellOutcomePlan(
	PetMerchantSellOutcomePlanStatus Status,
	PetMerchantSellLiveExecutorFacadePlan? FacadePlan,
	TradeSellToShopPlan? SellToShopPlan,
	IReadOnlyList<PetMerchantSellOutcomeStepPlan> Steps,
	bool WouldMutateSellerInventory,
	bool DidMutateSellerInventory,
	bool WouldAddRepurchaseItems,
	bool DidAddRepurchaseItems,
	bool WouldMutateKinah,
	bool DidMutateKinah,
	bool WouldCommitTransactionBoundary,
	bool DidCommitTransactionBoundary,
	bool ShouldCommitTransactionBoundary,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public sealed record PetMerchantSellLiveExecutorFacadePlan(
	PetMerchantSellLiveExecutorFacadeStatus Status,
	CmBuyItemHandlerCompositionPlan? HandlerPlan,
	int? PetSellModifier,
	TradeSellToShopPlan? SellToShopPlan,
	IReadOnlyList<PetMerchantSellLiveExecutorOperation> Operations,
	bool WouldMutateSellerInventory,
	bool DidMutateSellerInventory,
	bool WouldAddRepurchaseItems,
	bool DidAddRepurchaseItems,
	bool WouldMutateKinah,
	bool DidMutateKinah,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive)
{
	public bool IsDisabled => Status == PetMerchantSellLiveExecutorFacadeStatus.DisabledNoSideEffects;
}

public static class TradeSellToShopPlanService
{
	private const int CubeStorageId = 0;
	private const long FirstAvailableSlot = 65535;

	public static TradeSellToShopPlan CreatePlan(
		bool canTrade,
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<TradeSellToShopItemRequest> tradeItems,
		ItemTemplateTable itemTemplates,
		TradeListTemplateSummary? purchaseTemplate,
		GoodsListTable? goodsLists,
		int sellModifier,
		Func<int> nextObjectId)
	{
		// Java parity: services/TradeService.performSellToShop.
		if (!canTrade)
			return Block(
				TradeSellToShopPlanStatus.BlockedCannotTrade,
				"TradeService.performSellToShop -> !PlayerRestrictions.canTrade(player) -> false");

		var sellerDeletes = new List<int>();
		var sellerUpdates = new List<InventoryItem>();
		var repurchaseItems = new List<RepurchaseSourceItem>();
		var kinahReward = 0L;

		foreach (var tradeItem in tradeItems)
		{
			var item = inventoryItems.FirstOrDefault(candidate => candidate.ObjectId == tradeItem.ItemObjectId);
			if (item == null)
				return Block(
					TradeSellToShopPlanStatus.BlockedMissingItem,
					"TradeService.performSellToShop -> inventory.getItemByObjId(...) == null -> false");

			var template = itemTemplates.GetItemTemplate(item.ItemId);
			if (template == null)
				return Block(
					TradeSellToShopPlanStatus.BlockedMissingTemplate,
					"TradeService.performSellToShop -> item.getItemTemplate() missing; non-live planner blocks before mutation");

			var sellReward = CalculateSellReward(template, purchaseTemplate, goodsLists, item.ItemId, sellModifier, tradeItem.IsSellable, out var blockedStatus);
			if (blockedStatus != null)
				return Block(
					blockedStatus.Value,
					blockedStatus == TradeSellToShopPlanStatus.BlockedInvalidPurchaseItem
						? "TradeService.performSellToShop -> item id not found in purchase template goods list -> false"
						: "TradeService.performSellToShop -> !item.isSellable() -> STR_BUY_SELL_ITEM_CAN_NOT_BE_SELLED_TO_NPC and false");

			var count = tradeItem.SellLimitAdjustedCount ?? tradeItem.Count;
			if (count == 0)
				break;

			var remainingCount = item.Count - count;
			if (remainingCount < 0)
				return Block(
					TradeSellToShopPlanStatus.BlockedCountExceedsAvailable,
					"TradeService.performSellToShop -> item.getItemCount() - count < 0 -> audit and false");

			var repurchasePrice = sellReward * count;
			if (remainingCount == 0)
			{
				sellerDeletes.Add(item.ObjectId);
				repurchaseItems.Add(new RepurchaseSourceItem(CopyInventoryItem(item), repurchasePrice));
			}
			else
			{
				sellerUpdates.Add(CopyInventoryItem(item, remainingCount));
				var repurchaseObjectId = nextObjectId();
				if (repurchaseObjectId == 0)
					return Block(
						TradeSellToShopPlanStatus.BlockedRepurchaseItemCreateFailed,
						"ItemFactory.newItem source object id allocation failed in non-live planner");
				var repurchaseItem = InventoryItemFactory.CreateNewItem(
					repurchaseObjectId,
					template,
					count,
					player.ObjectId,
					CubeStorageId,
					FirstAvailableSlot);
				repurchaseItems.Add(new RepurchaseSourceItem(repurchaseItem, repurchasePrice));
			}

			kinahReward += repurchasePrice;
		}

		var kinahItem = inventoryItems.FirstOrDefault(item => item.ItemId == InventoryItemFactory.KinahItemId && item.Location == CubeStorageId);
		InventoryItem? kinahUpdate;
		if (kinahItem == null)
		{
			var kinahObjectId = nextObjectId();
			if (kinahObjectId == 0)
				return Block(
					TradeSellToShopPlanStatus.BlockedKinahAddFailed,
					"Storage.increaseKinah would create a Kinah row; non-live object id allocation failed");
			var kinahTemplate = itemTemplates.GetItemTemplate(InventoryItemFactory.KinahItemId);
			kinahUpdate = kinahTemplate == null
				? new InventoryItem
				{
					ObjectId = kinahObjectId,
					ItemId = InventoryItemFactory.KinahItemId,
					Count = kinahReward,
					OwnerId = player.ObjectId,
					Location = CubeStorageId,
					Slot = FirstAvailableSlot,
				}
				: InventoryItemFactory.CreateNewItem(kinahObjectId, kinahTemplate, kinahReward, player.ObjectId, CubeStorageId, FirstAvailableSlot);
		}
		else
		{
			kinahUpdate = CopyInventoryItem(kinahItem, kinahItem.Count + kinahReward);
		}

		return new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.PlanCreated,
			sellerDeletes,
			sellerUpdates,
			repurchaseItems,
			kinahUpdate,
			"TradeService.performSellToShop -> delete/decrease sold item -> setRepurchasePrice -> RepurchaseService.addRepurchaseItems -> inventory.increaseKinah");
	}

	private static long CalculateSellReward(
		ItemTemplateSummary template,
		TradeListTemplateSummary? purchaseTemplate,
		GoodsListTable? goodsLists,
		int itemId,
		int sellModifier,
		bool isSellable,
		out TradeSellToShopPlanStatus? blockedStatus)
	{
		blockedStatus = null;
		if (purchaseTemplate != null)
		{
			var valid = purchaseTemplate.GoodsListIds
				.Select(goodsListId => goodsLists?.GetGoodsPurchaseListById(goodsListId))
				.Where(goodsList => goodsList != null)
				.Any(goodsList => goodsList!.ItemSummaries.Any(item => item.Id == itemId));
			if (!valid)
			{
				blockedStatus = TradeSellToShopPlanStatus.BlockedInvalidPurchaseItem;
				return 0;
			}
			return (long)(template.Price * purchaseTemplate.BuyPriceRate / 100D);
		}

		if (!isSellable)
		{
			blockedStatus = TradeSellToShopPlanStatus.BlockedNotSellable;
			return 0;
		}

		return PricesService.GetSellReward(template.Price, sellModifier);
	}

	private static TradeSellToShopPlan Block(TradeSellToShopPlanStatus status, string javaSource)
	{
		return new TradeSellToShopPlan(
			status,
			SellerDeletedItemObjectIds: Array.Empty<int>(),
			SellerItemUpdates: Array.Empty<InventoryItem>(),
			RepurchaseItems: Array.Empty<RepurchaseSourceItem>(),
			KinahUpdate: null,
			javaSource);
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long? count = null)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count ?? item.Count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			PersistentState = item.PersistentState,
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}
}

public static class PetMerchantSellOutcomePlanService
{
	public static PetMerchantSellOutcomePlan CreateDisabledPlan(PetMerchantSellLiveExecutorFacadePlan? facadePlan)
	{
		if (facadePlan == null)
			return CreateTerminalPlan(
				PetMerchantSellOutcomePlanStatus.MissingFacadePlan,
				facadePlan,
				"CM_BUY_ITEM pet merchant final outcome requires a disabled live facade plan");

		if (facadePlan.Status != PetMerchantSellLiveExecutorFacadeStatus.DisabledNoSideEffects)
			return CreateTerminalPlan(
				PetMerchantSellOutcomePlanStatus.FacadeNotReady,
				facadePlan,
				"CM_BUY_ITEM pet merchant final outcome stops because facade is not eligible for disabled side-effect composition");

		var wouldMutateSellerInventory = facadePlan.WouldMutateSellerInventory;
		var wouldAddRepurchaseItems = facadePlan.WouldAddRepurchaseItems;
		var wouldMutateKinah = facadePlan.WouldMutateKinah;
		var wouldCommitBoundary = wouldMutateSellerInventory || wouldAddRepurchaseItems || wouldMutateKinah;

		var steps = new List<PetMerchantSellOutcomeStepPlan>();
		if (wouldMutateSellerInventory)
			steps.Add(Disabled(
				PetMerchantSellOutcomeStepKind.ApplySellerInventoryMutation,
				"TradeService.performSellToShop -> inventory.delete/decreaseItemCount for pet merchant sold items"));
		if (wouldAddRepurchaseItems)
			steps.Add(Disabled(
				PetMerchantSellOutcomeStepKind.AddRepurchaseItems,
				"TradeService.performSellToShop -> RepurchaseService.addRepurchaseItems(player, items)"));
		if (wouldMutateKinah)
			steps.Add(Disabled(
				PetMerchantSellOutcomeStepKind.IncreaseKinah,
				"TradeService.performSellToShop -> inventory.increaseKinah(kinahReward, INC_KINAH_SELL)"));
		if (wouldCommitBoundary)
			steps.Add(Disabled(
				PetMerchantSellOutcomeStepKind.CommitTransactionBoundary,
				"TradeService.performSellToShop pet merchant transaction boundary is recorded only; Java transaction semantics are not yet runtime-verified"));

		return new PetMerchantSellOutcomePlan(
			PetMerchantSellOutcomePlanStatus.DisabledNoTransaction,
			facadePlan,
			facadePlan.SellToShopPlan,
			steps,
			wouldMutateSellerInventory,
			DidMutateSellerInventory: false,
			wouldAddRepurchaseItems,
			DidAddRepurchaseItems: false,
			wouldMutateKinah,
			DidMutateKinah: false,
			wouldCommitBoundary,
			DidCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			"CM_BUY_ITEM Pet MERCHANT action 17 final outcome is disabled; inventory/repurchase/Kinah/transaction boundaries are recorded without dispatch",
			IsLive: false);
	}

	private static PetMerchantSellOutcomePlan CreateTerminalPlan(
		PetMerchantSellOutcomePlanStatus status,
		PetMerchantSellLiveExecutorFacadePlan? facadePlan,
		string javaSource) =>
		new(
			status,
			facadePlan,
			facadePlan?.SellToShopPlan,
			Steps: Array.Empty<PetMerchantSellOutcomeStepPlan>(),
			WouldMutateSellerInventory: false,
			DidMutateSellerInventory: false,
			WouldAddRepurchaseItems: false,
			DidAddRepurchaseItems: false,
			WouldMutateKinah: false,
			DidMutateKinah: false,
			WouldCommitTransactionBoundary: false,
			DidCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);

	private static PetMerchantSellOutcomeStepPlan Disabled(
		PetMerchantSellOutcomeStepKind kind,
		string javaSource) =>
		new(kind, WouldRun: true, DidRun: false, javaSource);
}

public static class PetMerchantSellLiveExecutorFacadePlanService
{
	public static PetMerchantSellLiveExecutorFacadePlan CreateDisabledPlan(CmBuyItemHandlerCompositionPlan? handlerPlan)
	{
		// Java parity: CM_BUY_ITEM Pet MERCHANT action 17 forwards to
		// TradeService.performSellToShop(player, tradeList, null, pf.getRatePrice()).
		// This facade records that side-effect boundary without mutating live state.
		if (handlerPlan == null)
			return CreateTerminalPlan(
				PetMerchantSellLiveExecutorFacadeStatus.MissingHandlerPlan,
				handlerPlan,
				petSellModifier: null,
				sellToShopPlan: null,
				NotAttempted(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedMissingPlan),
				"Pet merchant sell facade requires CM_BUY_ITEM handler composition evidence");

		if (handlerPlan.Status != CmBuyItemHandlerCompositionPlanStatus.SelectedPetSellToShopPlanner)
			return CreateTerminalPlan(
				PetMerchantSellLiveExecutorFacadeStatus.HandlerNotPetMerchantSell,
				handlerPlan,
				handlerPlan.PetSellModifier,
				handlerPlan.PetSellToShopPlan,
				NotAttempted(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedNotPetMerchantSell),
				"CM_BUY_ITEM.runImpl did not select Pet MERCHANT action 17 -> TradeService.performSellToShop");

		if (handlerPlan.PetSellModifier == null)
			return CreateTerminalPlan(
				PetMerchantSellLiveExecutorFacadeStatus.MissingSellModifier,
				handlerPlan,
				handlerPlan.PetSellModifier,
				handlerPlan.PetSellToShopPlan,
				NotAttempted(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedCompositionNotReady),
				"CM_BUY_ITEM pet merchant facade requires PetFunction.getRatePrice payload before side effects");

		var sellToShopPlan = handlerPlan.PetSellToShopPlan;
		if (sellToShopPlan == null)
			return CreateTerminalPlan(
				PetMerchantSellLiveExecutorFacadeStatus.MissingSellToShopPlan,
				handlerPlan,
				handlerPlan.PetSellModifier,
				sellToShopPlan,
				NotAttempted(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedCompositionNotReady),
				"CM_BUY_ITEM pet merchant facade requires TradeService.performSellToShop planning before side effects");

		if (sellToShopPlan.Status != TradeSellToShopPlanStatus.PlanCreated)
			return CreateTerminalPlan(
				PetMerchantSellLiveExecutorFacadeStatus.SellToShopPlanNotReady,
				handlerPlan,
				handlerPlan.PetSellModifier,
				sellToShopPlan,
				NotAttempted(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedCompositionNotReady),
				"TradeService.performSellToShop plan is blocked; pet merchant live side effects are not eligible");

		var operations = new List<PetMerchantSellLiveExecutorOperation>();
		var wouldMutateSellerInventory = sellToShopPlan.SellerDeletedItemObjectIds.Count > 0 || sellToShopPlan.SellerItemUpdates.Count > 0;
		if (wouldMutateSellerInventory)
			operations.Add(Disabled(
				PetMerchantSellLiveExecutorOperationKind.ApplySellerInventoryMutation,
				"TradeService.performSellToShop -> inventory.delete/decreaseItemCount for sold items"));

		var wouldAddRepurchaseItems = sellToShopPlan.RepurchaseItems.Count > 0;
		if (wouldAddRepurchaseItems)
			operations.Add(Disabled(
				PetMerchantSellLiveExecutorOperationKind.AddRepurchaseItems,
				"TradeService.performSellToShop -> RepurchaseService.addRepurchaseItems(player, items)"));

		var wouldMutateKinah = sellToShopPlan.KinahUpdate != null;
		if (wouldMutateKinah)
			operations.Add(Disabled(
				PetMerchantSellLiveExecutorOperationKind.IncreaseKinah,
				"TradeService.performSellToShop -> inventory.increaseKinah(kinahReward, INC_KINAH_SELL)"));

		return new PetMerchantSellLiveExecutorFacadePlan(
			PetMerchantSellLiveExecutorFacadeStatus.DisabledNoSideEffects,
			handlerPlan,
			handlerPlan.PetSellModifier,
			sellToShopPlan,
			operations,
			WouldMutateSellerInventory: wouldMutateSellerInventory,
			DidMutateSellerInventory: false,
			WouldAddRepurchaseItems: wouldAddRepurchaseItems,
			DidAddRepurchaseItems: false,
			WouldMutateKinah: wouldMutateKinah,
			DidMutateKinah: false,
			ShouldDispatchLiveSideEffects: false,
			"Pet merchant TradeService.performSellToShop live side-effect executor facade is disabled; Java side-effect order is recorded without dispatch",
			IsLive: false);
	}

	private static PetMerchantSellLiveExecutorFacadePlan CreateTerminalPlan(
		PetMerchantSellLiveExecutorFacadeStatus status,
		CmBuyItemHandlerCompositionPlan? handlerPlan,
		int? petSellModifier,
		TradeSellToShopPlan? sellToShopPlan,
		PetMerchantSellLiveExecutorOperation operation,
		string javaSource)
	{
		return new PetMerchantSellLiveExecutorFacadePlan(
			status,
			handlerPlan,
			petSellModifier,
			sellToShopPlan,
			[operation],
			WouldMutateSellerInventory: false,
			DidMutateSellerInventory: false,
			WouldAddRepurchaseItems: false,
			DidAddRepurchaseItems: false,
			WouldMutateKinah: false,
			DidMutateKinah: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);
	}

	private static PetMerchantSellLiveExecutorOperation Disabled(PetMerchantSellLiveExecutorOperationKind kind, string javaSource)
	{
		return new PetMerchantSellLiveExecutorOperation(
			kind,
			PetMerchantSellLiveExecutorOperationStatus.NotAttemptedDisabled,
			javaSource);
	}

	private static PetMerchantSellLiveExecutorOperation NotAttempted(PetMerchantSellLiveExecutorOperationStatus status)
	{
		return new PetMerchantSellLiveExecutorOperation(
			PetMerchantSellLiveExecutorOperationKind.ApplySellerInventoryMutation,
			status,
			"Pet merchant sell live executor facade did not reach this side-effect boundary");
	}
}
