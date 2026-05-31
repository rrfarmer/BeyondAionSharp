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
