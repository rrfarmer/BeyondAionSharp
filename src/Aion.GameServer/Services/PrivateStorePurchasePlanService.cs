using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PrivateStorePurchasePlanStatus
{
	PlanCreated,
	BlockedOfflineOrRaceMismatch,
	BlockedNoBoughtItems,
	BlockedBuyerInventoryFull,
	BlockedPriceOverflow,
	BlockedInsufficientKinah,
	BlockedMissingSellerItem,
	BlockedSellerItemCountChanged,
	BlockedMissingTemplate,
	BlockedBuyerAddFailed,
}

public sealed record PrivateStorePurchaseItemRequest(
	int StoreIndex,
	int ItemObjectId,
	int ItemId,
	long Count,
	long PricePerItem,
	string? ItemName);

public sealed record PrivateStorePurchasePlan(
	PrivateStorePurchasePlanStatus Status,
	IReadOnlyList<PrivateStorePurchaseItemRequest> BoughtItems,
	IReadOnlyList<InventoryItem> SellerItemUpdates,
	IReadOnlyList<int> SellerDeletedItemObjectIds,
	IReadOnlyList<InventoryItem> BuyerAddedItems,
	IReadOnlyList<InventoryItem> BuyerUpdatedItems,
	InventoryItem? BuyerKinahUpdate,
	InventoryItem? SellerKinahUpdate,
	IReadOnlyList<SmSystemMessage> BuyerMessages,
	IReadOnlyList<SmSystemMessage> SellerMessages,
	bool ShouldCloseSellerStore,
	string JavaSource)
{
	public bool IsLive => false;
}

public enum PrivateStoreLiveExecutorFacadeStatus
{
	MissingHandlerPlan,
	HandlerNotPrivateStore,
	MissingBoughtItemsPlan,
	BoughtItemsPlanNotReady,
	MissingPurchasePlan,
	PurchasePlanNotReady,
	DisabledNoSideEffects,
}

public enum PrivateStoreLiveExecutorOperationKind
{
	DecreaseSellerItem,
	UpdateSellerStoreItem,
	AddBuyerItem,
	SendSellerNotification,
	LogPrivateStoreSale,
	DecreaseBuyerKinah,
	IncreaseSellerKinah,
	CloseSellerStore,
}

public enum PrivateStoreLiveExecutorOperationStatus
{
	NotAttemptedMissingPlan,
	NotAttemptedNotPrivateStore,
	NotAttemptedCompositionNotReady,
	NotAttemptedDisabled,
}

public sealed record PrivateStoreLiveExecutorOperation(
	PrivateStoreLiveExecutorOperationKind Kind,
	PrivateStoreLiveExecutorOperationStatus Status,
	string JavaSource);

public sealed record PrivateStoreLiveExecutorFacadePlan(
	PrivateStoreLiveExecutorFacadeStatus Status,
	CmBuyItemHandlerCompositionPlan? HandlerPlan,
	PrivateStoreBoughtItemsPlan? BoughtItemsPlan,
	PrivateStorePurchasePlan? PurchasePlan,
	IReadOnlyList<PrivateStoreLiveExecutorOperation> Operations,
	bool WouldMutateSellerInventory,
	bool DidMutateSellerInventory,
	bool WouldMutateBuyerInventory,
	bool DidMutateBuyerInventory,
	bool WouldSendSellerMessages,
	bool DidSendSellerMessages,
	bool WouldWriteExchangeLog,
	bool DidWriteExchangeLog,
	bool WouldMutateBuyerKinah,
	bool DidMutateBuyerKinah,
	bool WouldMutateSellerKinah,
	bool DidMutateSellerKinah,
	bool WouldCloseSellerStore,
	bool DidCloseSellerStore,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive)
{
	public bool IsDisabled => Status == PrivateStoreLiveExecutorFacadeStatus.DisabledNoSideEffects;
}

public static class PrivateStorePurchasePlanService
{
	private const int CubeStorageId = 0;

	public static PrivateStorePurchasePlan CreatePlan(
		bool sellerOnline,
		bool buyerOnline,
		bool sameRace,
		Player buyer,
		Player seller,
		IReadOnlyList<InventoryItem> buyerInventoryItems,
		IReadOnlyList<InventoryItem> sellerInventoryItems,
		IReadOnlyList<PrivateStorePurchaseItemRequest> boughtItems,
		IReadOnlyList<int> remainingStoreItemObjectIdsAfterPurchase,
		ItemTemplateTable itemTemplates,
		Func<int> nextObjectId)
	{
		// Java parity: services/PrivateStoreService.sellStoreItem.
		if (!sellerOnline || !buyerOnline || !sameRace)
			return Block(
				PrivateStorePurchasePlanStatus.BlockedOfflineOrRaceMismatch,
				boughtItems,
				"PrivateStoreService.sellStoreItem -> !seller.isOnline() || !buyer.isOnline() || seller.getRace() != buyer.getRace() -> return");

		if (boughtItems.Count == 0)
			return Block(
				PrivateStorePurchasePlanStatus.BlockedNoBoughtItems,
				boughtItems,
				"PrivateStoreService.sellStoreItem -> boughtItems == null || boughtItems.isEmpty() -> return");

		if (InventoryCapacity.GetFreeCubeSlots(buyer, buyerInventoryItems) < boughtItems.Count)
			return Block(
				PrivateStorePurchasePlanStatus.BlockedBuyerInventoryFull,
				boughtItems,
				"PrivateStoreService.sellStoreItem -> buyer.getInventory().getFreeSlots() < boughtItems.size() -> STR_MSG_DICE_INVEN_ERROR",
				buyerMessages: [SmSystemMessage.DiceInventoryError()]);

		if (!TryCalculatePrice(boughtItems, out var totalPrice))
			return Block(
				PrivateStorePurchasePlanStatus.BlockedPriceOverflow,
				boughtItems,
				"PrivateStoreService.sellStoreItem -> price < 0 kinah dupe guard -> audit and return");

		var buyerKinah = buyerInventoryItems.FirstOrDefault(item => item.ItemId == InventoryItemFactory.KinahItemId && item.Location == CubeStorageId);
		if (buyerKinah == null || buyerKinah.Count < totalPrice)
			return Block(
				PrivateStorePurchasePlanStatus.BlockedInsufficientKinah,
				boughtItems,
				"PrivateStoreService.sellStoreItem -> price > buyer.getInventory().getKinah() -> return");

		var workingBuyerItems = buyerInventoryItems.ToList();
		var sellerUpdates = new List<InventoryItem>();
		var sellerDeletes = new List<int>();
		var buyerAddedItems = new List<InventoryItem>();
		var buyerUpdatedItems = new List<InventoryItem>();
		var sellerMessages = new List<SmSystemMessage>();

		foreach (var boughtItem in boughtItems)
		{
			var sellerItem = sellerInventoryItems.FirstOrDefault(item => item.ObjectId == boughtItem.ItemObjectId && item.ItemId == boughtItem.ItemId);
			if (sellerItem == null)
				return Block(
					PrivateStorePurchasePlanStatus.BlockedMissingSellerItem,
					boughtItems,
					"PrivateStoreService.sellStoreItem -> seller.getInventory().getItemByObjId(...) == null -> skip item; planner blocks because live partial-send semantics are not wired");

			if (sellerItem.Count < boughtItem.Count)
				return Block(
					PrivateStorePurchasePlanStatus.BlockedSellerItemCountChanged,
					boughtItems,
					"PrivateStoreService.sellStoreItem -> item.getItemCount() < boughtItem.getCount() -> audit and return");

			var template = itemTemplates.GetItemTemplate(boughtItem.ItemId);
			if (template == null)
				return Block(
					PrivateStorePurchasePlanStatus.BlockedMissingTemplate,
					boughtItems,
					"ItemService.addItem(buyer, item, boughtItem.getCount()) -> DataManager.ITEM_DATA missing template would fail before inventory mutation");

			var sourceForBuyer = sellerItem.PackCount > 0
				? CopyInventoryItem(sellerItem, packCount: sellerItem.PackCount - 1)
				: sellerItem;

			var addPlan = InventoryAddService.CreateAddItemPlan(
				buyer,
				workingBuyerItems,
				template,
				boughtItem.Count,
				nextObjectId,
				allowInventoryOverflow: false,
				itemTemplates: itemTemplates,
				sourceItem: sourceForBuyer);

			if (!addPlan.Succeeded)
				return Block(
					PrivateStorePurchasePlanStatus.BlockedBuyerAddFailed,
					boughtItems,
					"ItemService.addItem(buyer, item, boughtItem.getCount()) returned remaining count; live partial-add and dice-message behavior are not yet wired",
					buyerMessages: addPlan.InventoryFull ? [SmSystemMessage.DiceInventoryError()] : Array.Empty<SmSystemMessage>());

			buyerAddedItems.AddRange(addPlan.AddedItems);
			buyerUpdatedItems.AddRange(addPlan.UpdatedItems);
			ApplyBuyerAddPlan(workingBuyerItems, addPlan);

			var sellerRemainingCount = sellerItem.Count - boughtItem.Count;
			if (sellerRemainingCount > 0)
				sellerUpdates.Add(CopyInventoryItem(sellerItem, sellerRemainingCount));
			else
				sellerDeletes.Add(sellerItem.ObjectId);

			var notification = PrivateStoreSellNotificationPlanService.CreatePlan(boughtItem.Count, boughtItem.ItemName);
			if (notification.ShouldSendToSeller && notification.NotificationMessage != null)
				sellerMessages.Add(notification.NotificationMessage);
		}

		var buyerKinahUpdate = CopyInventoryItem(buyerKinah, buyerKinah.Count - totalPrice);
		var sellerKinah = sellerInventoryItems.FirstOrDefault(item => item.ItemId == InventoryItemFactory.KinahItemId && item.Location == CubeStorageId);
		var sellerKinahUpdate = sellerKinah == null
			? new InventoryItem
			{
				ObjectId = nextObjectId(),
				ItemId = InventoryItemFactory.KinahItemId,
				Count = totalPrice,
				OwnerId = seller.ObjectId,
				Location = CubeStorageId,
				Slot = 65535,
				PersistentState = InventoryItemPersistentState.New,
			}
			: CopyInventoryItem(sellerKinah, sellerKinah.Count + totalPrice);

		return new PrivateStorePurchasePlan(
			PrivateStorePurchasePlanStatus.PlanCreated,
			boughtItems,
			sellerUpdates,
			sellerDeletes,
			buyerAddedItems,
			buyerUpdatedItems,
			buyerKinahUpdate,
			sellerKinahUpdate,
			BuyerMessages: Array.Empty<SmSystemMessage>(),
			sellerMessages,
			ShouldCloseSellerStore: remainingStoreItemObjectIdsAfterPurchase.Count == 0,
			"PrivateStoreService.sellStoreItem -> decrease seller item -> unpack if packCount>0 -> ItemService.addItem(buyer, item, count) -> seller message -> decrease buyer kinah -> increase seller kinah -> close store when soldItems empty");
	}

	private static bool TryCalculatePrice(IReadOnlyList<PrivateStorePurchaseItemRequest> boughtItems, out long totalPrice)
	{
		totalPrice = 0;
		foreach (var item in boughtItems)
		{
			unchecked
			{
				totalPrice += item.PricePerItem * item.Count;
			}
		}

		return totalPrice >= 0;
	}

	private static PrivateStorePurchasePlan Block(
		PrivateStorePurchasePlanStatus status,
		IReadOnlyList<PrivateStorePurchaseItemRequest> boughtItems,
		string javaSource,
		IReadOnlyList<SmSystemMessage>? buyerMessages = null)
	{
		return new PrivateStorePurchasePlan(
			status,
			boughtItems,
			SellerItemUpdates: Array.Empty<InventoryItem>(),
			SellerDeletedItemObjectIds: Array.Empty<int>(),
			BuyerAddedItems: Array.Empty<InventoryItem>(),
			BuyerUpdatedItems: Array.Empty<InventoryItem>(),
			BuyerKinahUpdate: null,
			SellerKinahUpdate: null,
			buyerMessages ?? Array.Empty<SmSystemMessage>(),
			SellerMessages: Array.Empty<SmSystemMessage>(),
			ShouldCloseSellerStore: false,
			javaSource);
	}

	private static void ApplyBuyerAddPlan(List<InventoryItem> workingBuyerItems, InventoryAddPlan addPlan)
	{
		foreach (var updatedItem in addPlan.UpdatedItems)
		{
			var index = workingBuyerItems.FindIndex(item => item.ObjectId == updatedItem.ObjectId);
			if (index >= 0)
				workingBuyerItems[index] = updatedItem;
		}
		workingBuyerItems.AddRange(addPlan.AddedItems);
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long? count = null, int? packCount = null)
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
			PackCount = packCount ?? item.PackCount,
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

public static class PrivateStoreLiveExecutorFacadePlanService
{
	public static PrivateStoreLiveExecutorFacadePlan CreateDisabledPlan(CmBuyItemHandlerCompositionPlan? handlerPlan)
	{
		// Java parity: PrivateStoreService.sellStoreItem live side effects remain behind
		// a disabled boundary until seller/buyer inventory, Kinah, store state, packet,
		// and exchange-log mutation can be enabled and verified together.
		if (handlerPlan == null)
			return CreateTerminalPlan(
				PrivateStoreLiveExecutorFacadeStatus.MissingHandlerPlan,
				handlerPlan,
				boughtItemsPlan: null,
				purchasePlan: null,
				NotAttempted(PrivateStoreLiveExecutorOperationStatus.NotAttemptedMissingPlan),
				"PrivateStoreService.sellStoreItem live facade requires CM_BUY_ITEM handler composition evidence");

		if (handlerPlan.Status != CmBuyItemHandlerCompositionPlanStatus.SelectedPrivateStorePlanner)
			return CreateTerminalPlan(
				PrivateStoreLiveExecutorFacadeStatus.HandlerNotPrivateStore,
				handlerPlan,
				handlerPlan.PrivateStoreBoughtItemsPlan,
				handlerPlan.PrivateStorePurchasePlan,
				NotAttempted(PrivateStoreLiveExecutorOperationStatus.NotAttemptedNotPrivateStore),
				"CM_BUY_ITEM.runImpl dispatch did not select Player action 0 -> PrivateStoreService.sellStoreItem");

		var boughtItemsPlan = handlerPlan.PrivateStoreBoughtItemsPlan;
		if (boughtItemsPlan == null)
			return CreateTerminalPlan(
				PrivateStoreLiveExecutorFacadeStatus.MissingBoughtItemsPlan,
				handlerPlan,
				boughtItemsPlan,
				handlerPlan.PrivateStorePurchasePlan,
				NotAttempted(PrivateStoreLiveExecutorOperationStatus.NotAttemptedCompositionNotReady),
				"PrivateStoreService.sellStoreItem facade requires getBoughtItems composition before side effects");

		if (boughtItemsPlan.Status != PrivateStoreBoughtItemsPlanStatus.PlanCreated)
			return CreateTerminalPlan(
				PrivateStoreLiveExecutorFacadeStatus.BoughtItemsPlanNotReady,
				handlerPlan,
				boughtItemsPlan,
				handlerPlan.PrivateStorePurchasePlan,
				NotAttempted(PrivateStoreLiveExecutorOperationStatus.NotAttemptedCompositionNotReady),
				"PrivateStoreService.getBoughtItems returned null-equivalent blocked plan; live private-store effects are not eligible");

		var purchasePlan = handlerPlan.PrivateStorePurchasePlan;
		if (purchasePlan == null)
			return CreateTerminalPlan(
				PrivateStoreLiveExecutorFacadeStatus.MissingPurchasePlan,
				handlerPlan,
				boughtItemsPlan,
				purchasePlan,
				NotAttempted(PrivateStoreLiveExecutorOperationStatus.NotAttemptedCompositionNotReady),
				"PrivateStoreService.sellStoreItem facade requires purchase mutation planning before side effects");

		if (purchasePlan.Status != PrivateStorePurchasePlanStatus.PlanCreated)
			return CreateTerminalPlan(
				PrivateStoreLiveExecutorFacadeStatus.PurchasePlanNotReady,
				handlerPlan,
				boughtItemsPlan,
				purchasePlan,
				NotAttempted(PrivateStoreLiveExecutorOperationStatus.NotAttemptedCompositionNotReady),
				"PrivateStoreService.sellStoreItem purchase plan is blocked; live private-store effects are not eligible");

		var operations = new List<PrivateStoreLiveExecutorOperation>();
		var wouldMutateSellerInventory = purchasePlan.SellerItemUpdates.Count > 0 || purchasePlan.SellerDeletedItemObjectIds.Count > 0;
		if (wouldMutateSellerInventory)
		{
			operations.Add(Disabled(
				PrivateStoreLiveExecutorOperationKind.DecreaseSellerItem,
				"PrivateStoreService.decreaseItemFromPlayer -> seller.getInventory().decreaseItemCount(item, boughtItem.getCount())"));
			operations.Add(Disabled(
				PrivateStoreLiveExecutorOperationKind.UpdateSellerStoreItem,
				"PrivateStoreService.decreaseItemFromPlayer -> storeItem.decreaseCount/removeItem when count reaches zero"));
		}

		var wouldMutateBuyerInventory = purchasePlan.BuyerAddedItems.Count > 0 || purchasePlan.BuyerUpdatedItems.Count > 0;
		if (wouldMutateBuyerInventory)
			operations.Add(Disabled(
				PrivateStoreLiveExecutorOperationKind.AddBuyerItem,
				"PrivateStoreService.sellStoreItem -> ItemService.addItem(buyer, item, boughtItem.getCount())"));

		if (purchasePlan.SellerMessages.Count > 0)
			operations.Add(Disabled(
				PrivateStoreLiveExecutorOperationKind.SendSellerNotification,
				"PrivateStoreService.sellStoreItem -> STR_MSG_PERSONAL_SHOP_SELL_ITEM or STR_MSG_PERSONAL_SHOP_SELL_ITEM_MULTI"));

		if (purchasePlan.BoughtItems.Count > 0)
			operations.Add(Disabled(
				PrivateStoreLiveExecutorOperationKind.LogPrivateStoreSale,
				"PrivateStoreService.sellStoreItem -> EXCHANGE_LOG private-store sale line"));

		if (purchasePlan.BuyerKinahUpdate != null)
			operations.Add(Disabled(
				PrivateStoreLiveExecutorOperationKind.DecreaseBuyerKinah,
				"PrivateStoreService.sellStoreItem -> buyer.getInventory().decreaseKinah(price)"));

		if (purchasePlan.SellerKinahUpdate != null)
			operations.Add(Disabled(
				PrivateStoreLiveExecutorOperationKind.IncreaseSellerKinah,
				"PrivateStoreService.sellStoreItem -> seller.getInventory().increaseKinah(price)"));

		if (purchasePlan.ShouldCloseSellerStore)
			operations.Add(Disabled(
				PrivateStoreLiveExecutorOperationKind.CloseSellerStore,
				"PrivateStoreService.sellStoreItem -> if seller store empty closePrivateStore(seller)"));

		return new PrivateStoreLiveExecutorFacadePlan(
			PrivateStoreLiveExecutorFacadeStatus.DisabledNoSideEffects,
			handlerPlan,
			boughtItemsPlan,
			purchasePlan,
			operations,
			WouldMutateSellerInventory: wouldMutateSellerInventory,
			DidMutateSellerInventory: false,
			WouldMutateBuyerInventory: wouldMutateBuyerInventory,
			DidMutateBuyerInventory: false,
			WouldSendSellerMessages: purchasePlan.SellerMessages.Count > 0,
			DidSendSellerMessages: false,
			WouldWriteExchangeLog: purchasePlan.BoughtItems.Count > 0,
			DidWriteExchangeLog: false,
			WouldMutateBuyerKinah: purchasePlan.BuyerKinahUpdate != null,
			DidMutateBuyerKinah: false,
			WouldMutateSellerKinah: purchasePlan.SellerKinahUpdate != null,
			DidMutateSellerKinah: false,
			WouldCloseSellerStore: purchasePlan.ShouldCloseSellerStore,
			DidCloseSellerStore: false,
			ShouldDispatchLiveSideEffects: false,
			"PrivateStoreService.sellStoreItem live side-effect executor facade is disabled; Java side-effect order is recorded without dispatch",
			IsLive: false);
	}

	private static PrivateStoreLiveExecutorFacadePlan CreateTerminalPlan(
		PrivateStoreLiveExecutorFacadeStatus status,
		CmBuyItemHandlerCompositionPlan? handlerPlan,
		PrivateStoreBoughtItemsPlan? boughtItemsPlan,
		PrivateStorePurchasePlan? purchasePlan,
		PrivateStoreLiveExecutorOperation operation,
		string javaSource)
	{
		return new PrivateStoreLiveExecutorFacadePlan(
			status,
			handlerPlan,
			boughtItemsPlan,
			purchasePlan,
			[operation],
			WouldMutateSellerInventory: false,
			DidMutateSellerInventory: false,
			WouldMutateBuyerInventory: false,
			DidMutateBuyerInventory: false,
			WouldSendSellerMessages: false,
			DidSendSellerMessages: false,
			WouldWriteExchangeLog: false,
			DidWriteExchangeLog: false,
			WouldMutateBuyerKinah: false,
			DidMutateBuyerKinah: false,
			WouldMutateSellerKinah: false,
			DidMutateSellerKinah: false,
			WouldCloseSellerStore: false,
			DidCloseSellerStore: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);
	}

	private static PrivateStoreLiveExecutorOperation Disabled(PrivateStoreLiveExecutorOperationKind kind, string javaSource)
	{
		return new PrivateStoreLiveExecutorOperation(
			kind,
			PrivateStoreLiveExecutorOperationStatus.NotAttemptedDisabled,
			javaSource);
	}

	private static PrivateStoreLiveExecutorOperation NotAttempted(PrivateStoreLiveExecutorOperationStatus status)
	{
		return new PrivateStoreLiveExecutorOperation(
			PrivateStoreLiveExecutorOperationKind.DecreaseSellerItem,
			status,
			"PrivateStoreService.sellStoreItem live executor facade did not reach this side-effect boundary");
	}
}
