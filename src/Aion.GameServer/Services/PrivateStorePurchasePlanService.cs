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
	IReadOnlyList<PrivateStorePurchaseItemRequest> SkippedMissingSellerItems,
	IReadOnlyList<InventoryItem> SellerItemUpdates,
	IReadOnlyList<int> SellerDeletedItemObjectIds,
	IReadOnlyList<InventoryItem> BuyerAddedItems,
	IReadOnlyList<InventoryItem> BuyerUpdatedItems,
	InventoryItem? BuyerKinahUpdate,
	InventoryItem? SellerKinahUpdate,
	IReadOnlyList<SmSystemMessage> BuyerMessages,
	IReadOnlyList<SmSystemMessage> SellerMessages,
	bool WouldWriteAuditLog,
	string? AuditMessage,
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

public enum PrivateStorePersistenceAdapterStatus
{
	MissingPurchasePlan,
	PurchasePlanNotReady,
	DisabledNoWrites,
}

public enum PrivateStorePersistenceOperationKind
{
	SaveSellerItemUpdate,
	DeleteSellerItem,
	SaveBuyerAddedItem,
	SaveBuyerUpdatedItem,
	SaveBuyerKinah,
	SaveSellerKinah,
	UpdateSellerStoreItem,
	CloseSellerStore,
}

public enum PrivateStoreSendAdapterStatus
{
	MissingPurchasePlan,
	PurchasePlanNotReady,
	DisabledNoPackets,
}

public enum PrivateStoreSendIntentKind
{
	SendSellerItemUpdate,
	SendSellerItemDelete,
	SendBuyerItemAdd,
	SendBuyerItemUpdate,
	SendBuyerKinahUpdate,
	SendSellerKinahUpdate,
	SendSellerNotification,
	BroadcastSellerStoreClose,
	WriteExchangeLog,
}

public enum PrivateStorePurchaseOutcomePlanStatus
{
	MissingFacadePlan,
	FacadeNotReady,
	DisabledNoTransaction,
}

public enum PrivateStorePurchaseOutcomeStepKind
{
	PersistRepositoryWrites,
	DispatchPacketAndLogIntents,
	CommitTransactionBoundary,
}

public sealed record PrivateStoreLiveExecutorOperation(
	PrivateStoreLiveExecutorOperationKind Kind,
	PrivateStoreLiveExecutorOperationStatus Status,
	string JavaSource);

public sealed record PrivateStorePersistenceOperationPlan(
	PrivateStorePersistenceOperationKind Kind,
	int? ItemObjectId,
	int? PlayerObjectId,
	bool WouldWrite,
	bool DidWrite,
	string JavaSource);

public sealed record PrivateStorePersistenceAdapterPlan(
	PrivateStorePersistenceAdapterStatus Status,
	PrivateStorePurchasePlan? PurchasePlan,
	IReadOnlyList<PrivateStorePersistenceOperationPlan> Operations,
	bool WouldWriteRepository,
	bool DidWriteRepository,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public sealed record PrivateStoreSendIntentPlan(
	PrivateStoreSendIntentKind Kind,
	int? TargetPlayerObjectId,
	int? ItemObjectId,
	bool WouldSend,
	bool DidSend,
	string JavaSource);

public sealed record PrivateStoreSendAdapterPlan(
	PrivateStoreSendAdapterStatus Status,
	PrivateStorePurchasePlan? PurchasePlan,
	IReadOnlyList<PrivateStoreSendIntentPlan> Intents,
	bool WouldSendPackets,
	bool DidSendPackets,
	bool WouldWriteExchangeLog,
	bool DidWriteExchangeLog,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public sealed record PrivateStorePurchaseOutcomeStepPlan(
	PrivateStorePurchaseOutcomeStepKind Kind,
	bool WouldRun,
	bool DidRun,
	string JavaSource);

public sealed record PrivateStorePurchaseOutcomePlan(
	PrivateStorePurchaseOutcomePlanStatus Status,
	PrivateStoreLiveExecutorFacadePlan? FacadePlan,
	PrivateStorePersistenceAdapterPlan? PersistenceAdapterPlan,
	PrivateStoreSendAdapterPlan? SendAdapterPlan,
	IReadOnlyList<PrivateStorePurchaseOutcomeStepPlan> Steps,
	bool WouldWritePersistence,
	bool DidWritePersistence,
	bool WouldSendPackets,
	bool DidSendPackets,
	bool WouldWriteExchangeLog,
	bool DidWriteExchangeLog,
	bool WouldCommitTransactionBoundary,
	bool DidCommitTransactionBoundary,
	bool ShouldCommitTransactionBoundary,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public sealed record PrivateStoreLiveExecutorFacadePlan(
	PrivateStoreLiveExecutorFacadeStatus Status,
	CmBuyItemHandlerCompositionPlan? HandlerPlan,
	PrivateStoreBoughtItemsPlan? BoughtItemsPlan,
	PrivateStorePurchasePlan? PurchasePlan,
	PrivateStorePersistenceAdapterPlan PersistenceAdapterPlan,
	PrivateStoreSendAdapterPlan SendAdapterPlan,
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
				"PrivateStoreService.sellStoreItem -> price < 0 kinah dupe guard -> audit and return",
				wouldWriteAuditLog: true,
				auditMessage: "tried to buy item with negative kinah price from private store");

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
		var skippedMissingSellerItems = new List<PrivateStorePurchaseItemRequest>();
		var sellerMessages = new List<SmSystemMessage>();

		foreach (var boughtItem in boughtItems)
		{
			var sellerItem = sellerInventoryItems.FirstOrDefault(item => item.ObjectId == boughtItem.ItemObjectId && item.ItemId == boughtItem.ItemId);
			if (sellerItem == null)
			{
				skippedMissingSellerItems.Add(boughtItem);
				continue;
			}

			if (sellerItem.Count < boughtItem.Count)
				return Block(
					PrivateStorePurchasePlanStatus.BlockedSellerItemCountChanged,
					boughtItems,
					"PrivateStoreService.sellStoreItem -> item.getItemCount() < boughtItem.getCount() -> audit and return",
					wouldWriteAuditLog: true,
					auditMessage: "tried to buy more than players private store item stack count");

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
				sellerUpdates.Add(CopyInventoryItem(
					sellerItem,
					sellerRemainingCount,
					sellerItem.PackCount > 0 ? sellerItem.PackCount - 1 : sellerItem.PackCount));
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
		var javaSource = skippedMissingSellerItems.Count == 0
			? "PrivateStoreService.sellStoreItem -> decrease seller item -> unpack if packCount>0 -> ItemService.addItem(buyer, item, count) -> seller message -> decrease buyer kinah -> increase seller kinah -> close store when soldItems empty"
			: "PrivateStoreService.sellStoreItem -> seller.getInventory().getItemByObjId(...) == null skips that bought item inside loop; buyer.getInventory().decreaseKinah(price) and seller.getInventory().increaseKinah(price) still run after loop";

		return new PrivateStorePurchasePlan(
			PrivateStorePurchasePlanStatus.PlanCreated,
			boughtItems,
			skippedMissingSellerItems,
			sellerUpdates,
			sellerDeletes,
			buyerAddedItems,
			buyerUpdatedItems,
			buyerKinahUpdate,
			sellerKinahUpdate,
			BuyerMessages: Array.Empty<SmSystemMessage>(),
			sellerMessages,
			WouldWriteAuditLog: false,
			AuditMessage: null,
			ShouldCloseSellerStore: remainingStoreItemObjectIdsAfterPurchase.Count == 0,
			javaSource);
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
		IReadOnlyList<SmSystemMessage>? buyerMessages = null,
		bool wouldWriteAuditLog = false,
		string? auditMessage = null)
	{
		return new PrivateStorePurchasePlan(
			status,
			boughtItems,
			SkippedMissingSellerItems: Array.Empty<PrivateStorePurchaseItemRequest>(),
			SellerItemUpdates: Array.Empty<InventoryItem>(),
			SellerDeletedItemObjectIds: Array.Empty<int>(),
			BuyerAddedItems: Array.Empty<InventoryItem>(),
			BuyerUpdatedItems: Array.Empty<InventoryItem>(),
			BuyerKinahUpdate: null,
			SellerKinahUpdate: null,
			buyerMessages ?? Array.Empty<SmSystemMessage>(),
			SellerMessages: Array.Empty<SmSystemMessage>(),
			wouldWriteAuditLog,
			auditMessage,
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

public static class PrivateStorePurchaseOutcomePlanService
{
	public static PrivateStorePurchaseOutcomePlan CreateDisabledPlan(PrivateStoreLiveExecutorFacadePlan? facadePlan)
	{
		if (facadePlan == null)
			return CreateTerminalPlan(
				PrivateStorePurchaseOutcomePlanStatus.MissingFacadePlan,
				facadePlan,
				"PrivateStoreService.sellStoreItem final outcome requires a disabled live facade plan");

		if (facadePlan.Status != PrivateStoreLiveExecutorFacadeStatus.DisabledNoSideEffects)
			return CreateTerminalPlan(
				PrivateStorePurchaseOutcomePlanStatus.FacadeNotReady,
				facadePlan,
				"PrivateStoreService.sellStoreItem final outcome stops because facade is not eligible for disabled side-effect composition");

		var persistenceAdapterPlan = facadePlan.PersistenceAdapterPlan;
		var sendAdapterPlan = facadePlan.SendAdapterPlan;
		var wouldWritePersistence = persistenceAdapterPlan.WouldWriteRepository;
		var wouldSendPackets = sendAdapterPlan.WouldSendPackets;
		var wouldWriteExchangeLog = sendAdapterPlan.WouldWriteExchangeLog;
		var wouldCommitBoundary = wouldWritePersistence || wouldSendPackets || wouldWriteExchangeLog;

		var steps = new List<PrivateStorePurchaseOutcomeStepPlan>();
		if (wouldWritePersistence)
			steps.Add(Disabled(
				PrivateStorePurchaseOutcomeStepKind.PersistRepositoryWrites,
				"PrivateStoreService.sellStoreItem -> apply seller/buyer inventory, Kinah, and store-state persistence writes"));
		if (wouldSendPackets || wouldWriteExchangeLog)
			steps.Add(Disabled(
				PrivateStorePurchaseOutcomeStepKind.DispatchPacketAndLogIntents,
				"PrivateStoreService.sellStoreItem -> dispatch inventory/Kinah/system packets and EXCHANGE_LOG sale line"));
		if (wouldCommitBoundary)
			steps.Add(Disabled(
				PrivateStorePurchaseOutcomeStepKind.CommitTransactionBoundary,
				"PrivateStoreService.sellStoreItem final transaction boundary is recorded only; Java transaction semantics are not yet runtime-verified"));

		return new PrivateStorePurchaseOutcomePlan(
			PrivateStorePurchaseOutcomePlanStatus.DisabledNoTransaction,
			facadePlan,
			persistenceAdapterPlan,
			sendAdapterPlan,
			steps,
			wouldWritePersistence,
			DidWritePersistence: false,
			wouldSendPackets,
			DidSendPackets: false,
			wouldWriteExchangeLog,
			DidWriteExchangeLog: false,
			wouldCommitBoundary,
			DidCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			"PrivateStoreService.sellStoreItem final outcome is disabled; write/send/log/transaction boundaries are recorded without dispatch",
			IsLive: false);
	}

	private static PrivateStorePurchaseOutcomePlan CreateTerminalPlan(
		PrivateStorePurchaseOutcomePlanStatus status,
		PrivateStoreLiveExecutorFacadePlan? facadePlan,
		string javaSource) =>
		new(
			status,
			facadePlan,
			facadePlan?.PersistenceAdapterPlan,
			facadePlan?.SendAdapterPlan,
			Steps: Array.Empty<PrivateStorePurchaseOutcomeStepPlan>(),
			WouldWritePersistence: false,
			DidWritePersistence: false,
			WouldSendPackets: false,
			DidSendPackets: false,
			WouldWriteExchangeLog: false,
			DidWriteExchangeLog: false,
			WouldCommitTransactionBoundary: false,
			DidCommitTransactionBoundary: false,
			ShouldCommitTransactionBoundary: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);

	private static PrivateStorePurchaseOutcomeStepPlan Disabled(
		PrivateStorePurchaseOutcomeStepKind kind,
		string javaSource) =>
		new(kind, WouldRun: true, DidRun: false, javaSource);
}

public static class PrivateStorePersistenceAdapterPlanService
{
	public static PrivateStorePersistenceAdapterPlan CreateDisabledPlan(PrivateStorePurchasePlan? purchasePlan)
	{
		if (purchasePlan == null)
			return CreateTerminalPlan(
				PrivateStorePersistenceAdapterStatus.MissingPurchasePlan,
				purchasePlan,
				"PrivateStoreService.sellStoreItem persistence adapter requires a purchase mutation plan");

		if (purchasePlan.Status != PrivateStorePurchasePlanStatus.PlanCreated)
			return CreateTerminalPlan(
				PrivateStorePersistenceAdapterStatus.PurchasePlanNotReady,
				purchasePlan,
				"PrivateStoreService.sellStoreItem persistence adapter stops because purchase plan is blocked");

		var operations = new List<PrivateStorePersistenceOperationPlan>();
		operations.AddRange(purchasePlan.SellerItemUpdates.Select(item => Disabled(
			PrivateStorePersistenceOperationKind.SaveSellerItemUpdate,
			item.ObjectId,
			item.OwnerId,
			"PrivateStoreService.decreaseItemFromPlayer -> persist seller inventory decreased stack")));
		operations.AddRange(purchasePlan.SellerDeletedItemObjectIds.Select(objectId => Disabled(
			PrivateStorePersistenceOperationKind.DeleteSellerItem,
			objectId,
			playerObjectId: null,
			"PrivateStoreService.decreaseItemFromPlayer -> persist seller inventory item delete")));
		operations.AddRange(purchasePlan.BuyerAddedItems.Select(item => Disabled(
			PrivateStorePersistenceOperationKind.SaveBuyerAddedItem,
			item.ObjectId,
			item.OwnerId,
			"ItemService.addItem(buyer, item, count) -> persist buyer added item")));
		operations.AddRange(purchasePlan.BuyerUpdatedItems.Select(item => Disabled(
			PrivateStorePersistenceOperationKind.SaveBuyerUpdatedItem,
			item.ObjectId,
			item.OwnerId,
			"ItemService.addItem(buyer, item, count) -> persist buyer stack update")));

		if (purchasePlan.BuyerKinahUpdate != null)
			operations.Add(Disabled(
				PrivateStorePersistenceOperationKind.SaveBuyerKinah,
				purchasePlan.BuyerKinahUpdate.ObjectId,
				purchasePlan.BuyerKinahUpdate.OwnerId,
				"buyer.getInventory().decreaseKinah(price) -> persist buyer Kinah"));
		if (purchasePlan.SellerKinahUpdate != null)
			operations.Add(Disabled(
				PrivateStorePersistenceOperationKind.SaveSellerKinah,
				purchasePlan.SellerKinahUpdate.ObjectId,
				purchasePlan.SellerKinahUpdate.OwnerId,
				"seller.getInventory().increaseKinah(price) -> persist seller Kinah"));
		foreach (var sellerItem in purchasePlan.SellerItemUpdates)
			operations.Add(Disabled(
				PrivateStorePersistenceOperationKind.UpdateSellerStoreItem,
				sellerItem.ObjectId,
				playerObjectId: null,
				"PrivateStoreService.decreaseItemFromPlayer -> persist private-store sold item count/remove"));
		foreach (var objectId in purchasePlan.SellerDeletedItemObjectIds)
			operations.Add(Disabled(
				PrivateStorePersistenceOperationKind.UpdateSellerStoreItem,
				objectId,
				playerObjectId: null,
				"PrivateStoreService.decreaseItemFromPlayer -> persist private-store sold item count/remove"));
		if (purchasePlan.ShouldCloseSellerStore)
			operations.Add(Disabled(
				PrivateStorePersistenceOperationKind.CloseSellerStore,
				itemObjectId: null,
				playerObjectId: null,
				"PrivateStoreService.sellStoreItem -> closePrivateStore(seller) after soldItems empty"));

		return new PrivateStorePersistenceAdapterPlan(
			PrivateStorePersistenceAdapterStatus.DisabledNoWrites,
			purchasePlan,
			operations,
			WouldWriteRepository: operations.Count > 0,
			DidWriteRepository: false,
			ShouldDispatchLiveSideEffects: false,
			"PrivateStoreService.sellStoreItem persistence writes are recorded but disabled",
			IsLive: false);
	}

	private static PrivateStorePersistenceAdapterPlan CreateTerminalPlan(
		PrivateStorePersistenceAdapterStatus status,
		PrivateStorePurchasePlan? purchasePlan,
		string javaSource) =>
		new(
			status,
			purchasePlan,
			Operations: Array.Empty<PrivateStorePersistenceOperationPlan>(),
			WouldWriteRepository: false,
			DidWriteRepository: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);

	private static PrivateStorePersistenceOperationPlan Disabled(
		PrivateStorePersistenceOperationKind kind,
		int? itemObjectId,
		int? playerObjectId,
		string javaSource) =>
		new(kind, itemObjectId, playerObjectId, WouldWrite: true, DidWrite: false, javaSource);
}

public static class PrivateStoreSendAdapterPlanService
{
	public static PrivateStoreSendAdapterPlan CreateDisabledPlan(PrivateStorePurchasePlan? purchasePlan)
	{
		if (purchasePlan == null)
			return CreateTerminalPlan(
				PrivateStoreSendAdapterStatus.MissingPurchasePlan,
				purchasePlan,
				"PrivateStoreService.sellStoreItem send adapter requires a purchase mutation plan");

		if (purchasePlan.Status != PrivateStorePurchasePlanStatus.PlanCreated)
			return CreateTerminalPlan(
				PrivateStoreSendAdapterStatus.PurchasePlanNotReady,
				purchasePlan,
				"PrivateStoreService.sellStoreItem send adapter stops because purchase plan is blocked");

		var intents = new List<PrivateStoreSendIntentPlan>();
		intents.AddRange(purchasePlan.SellerItemUpdates.Select(item => Disabled(
			PrivateStoreSendIntentKind.SendSellerItemUpdate,
			item.OwnerId,
			item.ObjectId,
			"seller.getInventory().decreaseItemCount -> send seller inventory update")));
		intents.AddRange(purchasePlan.SellerDeletedItemObjectIds.Select(objectId => Disabled(
			PrivateStoreSendIntentKind.SendSellerItemDelete,
			targetPlayerObjectId: null,
			objectId,
			"seller.getInventory().decreaseItemCount -> send seller inventory delete")));
		intents.AddRange(purchasePlan.BuyerAddedItems.Select(item => Disabled(
			PrivateStoreSendIntentKind.SendBuyerItemAdd,
			item.OwnerId,
			item.ObjectId,
			"ItemService.addItem -> send buyer item add")));
		intents.AddRange(purchasePlan.BuyerUpdatedItems.Select(item => Disabled(
			PrivateStoreSendIntentKind.SendBuyerItemUpdate,
			item.OwnerId,
			item.ObjectId,
			"ItemService.addItem -> send buyer stack update")));

		if (purchasePlan.BuyerKinahUpdate != null)
			intents.Add(Disabled(
				PrivateStoreSendIntentKind.SendBuyerKinahUpdate,
				purchasePlan.BuyerKinahUpdate.OwnerId,
				purchasePlan.BuyerKinahUpdate.ObjectId,
				"buyer.getInventory().decreaseKinah(price) -> send buyer Kinah update"));
		if (purchasePlan.SellerKinahUpdate != null)
			intents.Add(Disabled(
				PrivateStoreSendIntentKind.SendSellerKinahUpdate,
				purchasePlan.SellerKinahUpdate.OwnerId,
				purchasePlan.SellerKinahUpdate.ObjectId,
				"seller.getInventory().increaseKinah(price) -> send seller Kinah update"));
		intents.AddRange(purchasePlan.SellerMessages.Select(message => Disabled(
			PrivateStoreSendIntentKind.SendSellerNotification,
			targetPlayerObjectId: null,
			itemObjectId: null,
			$"PrivateStoreService.sellStoreItem -> send seller system message {message.MessageId}")));
		if (purchasePlan.ShouldCloseSellerStore)
			intents.Add(Disabled(
				PrivateStoreSendIntentKind.BroadcastSellerStoreClose,
				targetPlayerObjectId: null,
				itemObjectId: null,
				"PrivateStoreService.closePrivateStore -> broadcast SM_EMOTION(CLOSE_PRIVATESHOP)"));
		if (purchasePlan.BuyerAddedItems.Count > 0 || purchasePlan.BuyerUpdatedItems.Count > 0)
			intents.Add(Disabled(
				PrivateStoreSendIntentKind.WriteExchangeLog,
				targetPlayerObjectId: null,
				itemObjectId: null,
				"PrivateStoreService.sellStoreItem -> EXCHANGE_LOG private-store sale line"));

		return new PrivateStoreSendAdapterPlan(
			PrivateStoreSendAdapterStatus.DisabledNoPackets,
			purchasePlan,
			intents,
			WouldSendPackets: intents.Any(intent => intent.Kind != PrivateStoreSendIntentKind.WriteExchangeLog),
			DidSendPackets: false,
			WouldWriteExchangeLog: intents.Any(intent => intent.Kind == PrivateStoreSendIntentKind.WriteExchangeLog),
			DidWriteExchangeLog: false,
			ShouldDispatchLiveSideEffects: false,
			"PrivateStoreService.sellStoreItem packet/log sends are recorded but disabled",
			IsLive: false);
	}

	private static PrivateStoreSendAdapterPlan CreateTerminalPlan(
		PrivateStoreSendAdapterStatus status,
		PrivateStorePurchasePlan? purchasePlan,
		string javaSource) =>
		new(
			status,
			purchasePlan,
			Intents: Array.Empty<PrivateStoreSendIntentPlan>(),
			WouldSendPackets: false,
			DidSendPackets: false,
			WouldWriteExchangeLog: false,
			DidWriteExchangeLog: false,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);

	private static PrivateStoreSendIntentPlan Disabled(
		PrivateStoreSendIntentKind kind,
		int? targetPlayerObjectId,
		int? itemObjectId,
		string javaSource) =>
		new(kind, targetPlayerObjectId, itemObjectId, WouldSend: true, DidSend: false, javaSource);
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

		var persistenceAdapterPlan = PrivateStorePersistenceAdapterPlanService.CreateDisabledPlan(purchasePlan);
		var sendAdapterPlan = PrivateStoreSendAdapterPlanService.CreateDisabledPlan(purchasePlan);
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

		if (purchasePlan.BuyerAddedItems.Count > 0 || purchasePlan.BuyerUpdatedItems.Count > 0)
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
			persistenceAdapterPlan,
			sendAdapterPlan,
			operations,
			WouldMutateSellerInventory: wouldMutateSellerInventory,
			DidMutateSellerInventory: false,
			WouldMutateBuyerInventory: wouldMutateBuyerInventory,
			DidMutateBuyerInventory: false,
			WouldSendSellerMessages: purchasePlan.SellerMessages.Count > 0,
			DidSendSellerMessages: false,
			WouldWriteExchangeLog: purchasePlan.BuyerAddedItems.Count > 0 || purchasePlan.BuyerUpdatedItems.Count > 0,
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
			PrivateStorePersistenceAdapterPlanService.CreateDisabledPlan(purchasePlan),
			PrivateStoreSendAdapterPlanService.CreateDisabledPlan(purchasePlan),
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
