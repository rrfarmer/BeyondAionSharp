using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PrivateStorePurchasePlanServiceTests
{
	[Fact]
	public void CreatePlan_BuysNonStackableItemWithSourceCloneAndKinahTransfers()
	{
		var buyer = new Player { ObjectId = 1001 };
		var seller = new Player { ObjectId = 2001 };
		var buyerKinah = Item(10, KinahItemId, 10_000, ownerId: buyer.ObjectId);
		var sellerKinah = Item(20, KinahItemId, 500, ownerId: seller.ObjectId);
		var sellerItemBase = Item(30, SwordItemId, 1, ownerId: seller.ObjectId);
		var sellerItem = new InventoryItem
		{
			ObjectId = sellerItemBase.ObjectId,
			ItemId = sellerItemBase.ItemId,
			Count = sellerItemBase.Count,
			Color = 123,
			Creator = "seller",
			OwnerId = sellerItemBase.OwnerId,
			Location = sellerItemBase.Location,
			Slot = sellerItemBase.Slot,
			Enchant = 7,
			OptionalSocket = 2,
			RandomBonus = 9,
			IsSoulBound = true,
		};
		sellerItem.ManaStones = [new ItemStoneSocket(167000001, 0)];
		sellerItem.Godstone = new PlayerGodstone(168000001, ProcCount: 3);

		var plan = CreatePlan(
			buyer,
			seller,
			buyerInventoryItems: [buyerKinah],
			sellerInventoryItems: [sellerKinah, sellerItem],
			boughtItems:
			[
				new PrivateStorePurchaseItemRequest(0, sellerItem.ObjectId, sellerItem.ItemId, Count: 1, PricePerItem: 4_000, ItemName: "Practice Sword"),
			],
			remainingStoreItemObjectIdsAfterPurchase: []);

		Assert.Equal(PrivateStorePurchasePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Empty(plan.BuyerMessages);
		Assert.Equal([sellerItem.ObjectId], plan.SellerDeletedItemObjectIds);
		Assert.Empty(plan.SellerItemUpdates);
		Assert.Equal(6_000, plan.BuyerKinahUpdate!.Count);
		Assert.Equal(4_500, plan.SellerKinahUpdate!.Count);
		Assert.True(plan.ShouldCloseSellerStore);

		var buyerItem = Assert.Single(plan.BuyerAddedItems);
		Assert.Equal((100, SwordItemId, 1L, buyer.ObjectId, 0, 65535L), (buyerItem.ObjectId, buyerItem.ItemId, buyerItem.Count, buyerItem.OwnerId, buyerItem.Location, buyerItem.Slot));
		Assert.Equal(sellerItem.Color, buyerItem.Color);
		Assert.Equal(sellerItem.Creator, buyerItem.Creator);
		Assert.Equal(sellerItem.Enchant, buyerItem.Enchant);
		Assert.Equal(sellerItem.OptionalSocket, buyerItem.OptionalSocket);
		Assert.Equal(sellerItem.RandomBonus, buyerItem.RandomBonus);
		Assert.True(buyerItem.IsSoulBound);
		Assert.Equal(sellerItem.ManaStones, buyerItem.ManaStones);
		Assert.Equal(sellerItem.Godstone, buyerItem.Godstone);

		var sellerMessage = Assert.Single(plan.SellerMessages);
		Assert.Equal(1400134, sellerMessage.MessageId);
	}

	[Fact]
	public void CreatePlan_BuysPartialStackableItemAndLeavesSellerRemainder()
	{
		var buyer = new Player { ObjectId = 1001 };
		var seller = new Player { ObjectId = 2001 };
		var buyerKinah = Item(10, KinahItemId, 10_000, ownerId: buyer.ObjectId);
		var sellerItem = Item(30, StackableItemId, 5, ownerId: seller.ObjectId, packCount: 4);

		var plan = CreatePlan(
			buyer,
			seller,
			buyerInventoryItems: [buyerKinah],
			sellerInventoryItems: [sellerItem],
			boughtItems:
			[
				new PrivateStorePurchaseItemRequest(0, sellerItem.ObjectId, sellerItem.ItemId, Count: 3, PricePerItem: 100, ItemName: "Practice Bundle"),
			],
			remainingStoreItemObjectIdsAfterPurchase: [sellerItem.ObjectId]);

		Assert.Equal(PrivateStorePurchasePlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.ShouldCloseSellerStore);
		Assert.Empty(plan.SellerDeletedItemObjectIds);
		var sellerUpdate = Assert.Single(plan.SellerItemUpdates);
		Assert.Equal((sellerItem.ObjectId, 2L), (sellerUpdate.ObjectId, sellerUpdate.Count));
		Assert.Equal(3, sellerUpdate.PackCount);
		var buyerItem = Assert.Single(plan.BuyerAddedItems);
		Assert.Equal(3, buyerItem.Count);
		Assert.Equal(0, buyerItem.PackCount);
		Assert.Equal(9_700, plan.BuyerKinahUpdate!.Count);
		Assert.Equal(300, plan.SellerKinahUpdate!.Count);
		Assert.Equal(1400135, Assert.Single(plan.SellerMessages).MessageId);
	}

	[Fact]
	public void CreatePlan_BuyerInventoryFullBlocksBeforePriceCalculation()
	{
		var buyer = new Player { ObjectId = 1001 };
		var seller = new Player { ObjectId = 2001 };
		var buyerItems = Enumerable.Range(0, 27)
			.Select(index => Item(index + 1, 3_000 + index, 1, ownerId: buyer.ObjectId))
			.Prepend(Item(99, KinahItemId, 10_000, ownerId: buyer.ObjectId))
			.ToArray();
		var sellerItem = Item(30, SwordItemId, 1, ownerId: seller.ObjectId);

		var plan = CreatePlan(
			buyer,
			seller,
			buyerInventoryItems: buyerItems,
			sellerInventoryItems: [sellerItem],
			boughtItems:
			[
				new PrivateStorePurchaseItemRequest(0, sellerItem.ObjectId, sellerItem.ItemId, Count: 1, PricePerItem: long.MaxValue, ItemName: "Practice Sword"),
			],
			remainingStoreItemObjectIdsAfterPurchase: []);

		Assert.Equal(PrivateStorePurchasePlanStatus.BlockedBuyerInventoryFull, plan.Status);
		Assert.Equal(1390182, Assert.Single(plan.BuyerMessages).MessageId);
		Assert.Null(plan.BuyerKinahUpdate);
		Assert.Empty(plan.BuyerAddedItems);
	}

	[Fact]
	public void CreatePlan_BlocksWhenPriceOverflows()
	{
		var buyer = new Player { ObjectId = 1001 };
		var seller = new Player { ObjectId = 2001 };
		var sellerItem = Item(30, SwordItemId, 1, ownerId: seller.ObjectId);

		var plan = CreatePlan(
			buyer,
			seller,
			buyerInventoryItems: [Item(99, KinahItemId, long.MaxValue, ownerId: buyer.ObjectId)],
			sellerInventoryItems: [sellerItem],
			boughtItems:
			[
				new PrivateStorePurchaseItemRequest(0, sellerItem.ObjectId, sellerItem.ItemId, Count: 2, PricePerItem: long.MaxValue, ItemName: "Practice Sword"),
			],
			remainingStoreItemObjectIdsAfterPurchase: []);

		Assert.Equal(PrivateStorePurchasePlanStatus.BlockedPriceOverflow, plan.Status);
		Assert.Empty(plan.BuyerMessages);
		Assert.Empty(plan.BuyerAddedItems);
		Assert.True(plan.WouldWriteAuditLog);
		Assert.Equal("tried to buy item with negative kinah price from private store", plan.AuditMessage);
	}

	[Fact]
	public void CreatePlan_BlocksWhenBuyerCannotPay()
	{
		var buyer = new Player { ObjectId = 1001 };
		var seller = new Player { ObjectId = 2001 };
		var sellerItem = Item(30, SwordItemId, 1, ownerId: seller.ObjectId);

		var plan = CreatePlan(
			buyer,
			seller,
			buyerInventoryItems: [Item(99, KinahItemId, 99, ownerId: buyer.ObjectId)],
			sellerInventoryItems: [sellerItem],
			boughtItems:
			[
				new PrivateStorePurchaseItemRequest(0, sellerItem.ObjectId, sellerItem.ItemId, Count: 1, PricePerItem: 100, ItemName: "Practice Sword"),
			],
			remainingStoreItemObjectIdsAfterPurchase: []);

		Assert.Equal(PrivateStorePurchasePlanStatus.BlockedInsufficientKinah, plan.Status);
		Assert.Null(plan.BuyerKinahUpdate);
		Assert.Empty(plan.BuyerMessages);
	}

	[Fact]
	public void CreatePlan_BlocksWhenSellerItemCountChanged()
	{
		var buyer = new Player { ObjectId = 1001 };
		var seller = new Player { ObjectId = 2001 };
		var sellerItem = Item(30, StackableItemId, 1, ownerId: seller.ObjectId);

		var plan = CreatePlan(
			buyer,
			seller,
			buyerInventoryItems: [Item(99, KinahItemId, 10_000, ownerId: buyer.ObjectId)],
			sellerInventoryItems: [sellerItem],
			boughtItems:
			[
				new PrivateStorePurchaseItemRequest(0, sellerItem.ObjectId, sellerItem.ItemId, Count: 2, PricePerItem: 100, ItemName: "Practice Bundle"),
			],
			remainingStoreItemObjectIdsAfterPurchase: [sellerItem.ObjectId]);

		Assert.Equal(PrivateStorePurchasePlanStatus.BlockedSellerItemCountChanged, plan.Status);
		Assert.Empty(plan.BuyerAddedItems);
		Assert.Empty(plan.SellerItemUpdates);
		Assert.True(plan.WouldWriteAuditLog);
		Assert.Equal("tried to buy more than players private store item stack count", plan.AuditMessage);
	}

	[Fact]
	public void CreatePlan_SkipsMissingSellerItemButKeepsJavaKinahTransferIntent()
	{
		var buyer = new Player { ObjectId = 1001 };
		var seller = new Player { ObjectId = 2001 };
		var buyerKinah = Item(10, KinahItemId, 10_000, ownerId: buyer.ObjectId);
		var sellerKinah = Item(20, KinahItemId, 500, ownerId: seller.ObjectId);
		var request = new PrivateStorePurchaseItemRequest(0, ItemObjectId: 30, SwordItemId, Count: 1, PricePerItem: 4_000, ItemName: "Practice Sword");

		var plan = CreatePlan(
			buyer,
			seller,
			buyerInventoryItems: [buyerKinah],
			sellerInventoryItems: [sellerKinah],
			boughtItems: [request],
			remainingStoreItemObjectIdsAfterPurchase: []);

		Assert.Equal(PrivateStorePurchasePlanStatus.PlanCreated, plan.Status);
		Assert.Equal([request], plan.SkippedMissingSellerItems);
		Assert.Empty(plan.SellerItemUpdates);
		Assert.Empty(plan.SellerDeletedItemObjectIds);
		Assert.Empty(plan.BuyerAddedItems);
		Assert.Empty(plan.BuyerUpdatedItems);
		Assert.Empty(plan.SellerMessages);
		Assert.False(plan.WouldWriteAuditLog);
		Assert.False(plan.ShouldCloseSellerStore);
		Assert.Equal(6_000, plan.BuyerKinahUpdate!.Count);
		Assert.Equal(4_500, plan.SellerKinahUpdate!.Count);
		Assert.Contains("seller.getInventory().getItemByObjId", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_MixedPresentAndMissingSellerItemsMutatesPresentItemButChargesFullPrice()
	{
		var buyer = new Player { ObjectId = 1001 };
		var seller = new Player { ObjectId = 2001 };
		var buyerKinah = Item(10, KinahItemId, 10_000, ownerId: buyer.ObjectId);
		var sellerKinah = Item(20, KinahItemId, 500, ownerId: seller.ObjectId);
		var sellerItem = Item(30, StackableItemId, 5, ownerId: seller.ObjectId);
		var presentRequest = new PrivateStorePurchaseItemRequest(0, sellerItem.ObjectId, sellerItem.ItemId, Count: 2, PricePerItem: 100, ItemName: "Practice Bundle");
		var missingRequest = new PrivateStorePurchaseItemRequest(1, ItemObjectId: 31, SwordItemId, Count: 1, PricePerItem: 4_000, ItemName: "Practice Sword");

		var plan = CreatePlan(
			buyer,
			seller,
			buyerInventoryItems: [buyerKinah],
			sellerInventoryItems: [sellerKinah, sellerItem],
			boughtItems: [presentRequest, missingRequest],
			remainingStoreItemObjectIdsAfterPurchase: [missingRequest.ItemObjectId]);

		Assert.Equal(PrivateStorePurchasePlanStatus.PlanCreated, plan.Status);
		Assert.Equal([presentRequest, missingRequest], plan.BoughtItems);
		Assert.Equal([missingRequest], plan.SkippedMissingSellerItems);
		var sellerUpdate = Assert.Single(plan.SellerItemUpdates);
		Assert.Equal((sellerItem.ObjectId, 3L), (sellerUpdate.ObjectId, sellerUpdate.Count));
		Assert.Empty(plan.SellerDeletedItemObjectIds);
		var buyerItem = Assert.Single(plan.BuyerAddedItems);
		Assert.Equal((StackableItemId, 2L, buyer.ObjectId), (buyerItem.ItemId, buyerItem.Count, buyerItem.OwnerId));
		Assert.Empty(plan.BuyerUpdatedItems);
		Assert.Equal(5_800, plan.BuyerKinahUpdate!.Count);
		Assert.Equal(4_700, plan.SellerKinahUpdate!.Count);
		Assert.False(plan.ShouldCloseSellerStore);
		Assert.Equal(1400135, Assert.Single(plan.SellerMessages).MessageId);
		Assert.False(plan.WouldWriteAuditLog);
		Assert.Contains("skips that bought item", plan.JavaSource, StringComparison.Ordinal);
	}

	private static PrivateStorePurchasePlan CreatePlan(
		Player buyer,
		Player seller,
		IReadOnlyList<InventoryItem> buyerInventoryItems,
		IReadOnlyList<InventoryItem> sellerInventoryItems,
		IReadOnlyList<PrivateStorePurchaseItemRequest> boughtItems,
		IReadOnlyList<int> remainingStoreItemObjectIdsAfterPurchase)
	{
		var nextObjectId = 99;
		buyer.InventoryItems = buyerInventoryItems;
		seller.InventoryItems = sellerInventoryItems;
		return PrivateStorePurchasePlanService.CreatePlan(
			sellerOnline: true,
			buyerOnline: true,
			sameRace: true,
			buyer,
			seller,
			buyerInventoryItems,
			sellerInventoryItems,
			boughtItems,
			remainingStoreItemObjectIdsAfterPurchase,
			CreateTemplates(),
			() => ++nextObjectId);
	}

	private static InventoryItem Item(int objectId, int itemId, long count, int ownerId, int packCount = 0)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			OwnerId = ownerId,
			Location = 0,
			Slot = 65535,
			PackCount = packCount,
		};
	}

	private static ItemTemplateTable CreateTemplates()
	{
		var fillerTemplates = Enumerable.Range(3_000, 27)
			.Select(itemId => Template(itemId, maxStackCount: 1))
			.ToArray();
		return new ItemTemplateTable(
		[
			Template(KinahItemId, maxStackCount: 1),
			Template(SwordItemId, maxStackCount: 1),
			Template(StackableItemId, maxStackCount: 10),
			.. fillerTemplates,
		]);
	}

	private static ItemTemplateSummary Template(int itemId, int maxStackCount)
	{
		return new ItemTemplateSummary(
			itemId,
			$"Item {itemId}",
			DescriptionId: 1,
			Mask: 0,
			Level: 1,
			ItemGroup: "NORMAL",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: maxStackCount,
			Price: 0,
			ValidEquipmentSlots: 0);
	}

	private const int KinahItemId = 182400001;
	private const int SwordItemId = 100000001;
	private const int StackableItemId = 182003001;
}
