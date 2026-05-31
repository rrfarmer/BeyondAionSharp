using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TradeSellToShopPlanServiceTests
{
	[Fact]
	public void CreatePlan_SellsWholeItemAndStoresOriginalForRepurchase()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId, creator: "maker", enchant: 5);

		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 1_000, ownerId: player.ObjectId), sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1)]);

		Assert.Equal(TradeSellToShopPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Equal([sword.ObjectId], plan.SellerDeletedItemObjectIds);
		Assert.Empty(plan.SellerItemUpdates);
		Assert.Equal(1_200, plan.KinahUpdate!.Count);

		var repurchase = Assert.Single(plan.RepurchaseItems);
		Assert.Equal(sword.ObjectId, repurchase.Item.ObjectId);
		Assert.Equal(sword.ItemId, repurchase.Item.ItemId);
		Assert.Equal(sword.Count, repurchase.Item.Count);
		Assert.Equal(200, repurchase.RepurchasePrice);
		Assert.Equal("maker", repurchase.Item.Creator);
		Assert.Equal(5, repurchase.Item.Enchant);
	}

	[Fact]
	public void CreatePlan_SellsPartialStackAndCreatesFreshRepurchaseItem()
	{
		var player = new Player { ObjectId = 1001 };
		var stack = Item(200, StackableItemId, 5, ownerId: player.ObjectId, creator: "source metadata must not copy");

		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 1_000, ownerId: player.ObjectId), stack],
			tradeItems: [new TradeSellToShopItemRequest(stack.ObjectId, Count: 3)]);

		Assert.Equal(TradeSellToShopPlanStatus.PlanCreated, plan.Status);
		var sellerUpdate = Assert.Single(plan.SellerItemUpdates);
		Assert.Equal((stack.ObjectId, 2L), (sellerUpdate.ObjectId, sellerUpdate.Count));
		Assert.Empty(plan.SellerDeletedItemObjectIds);
		Assert.Equal(1_060, plan.KinahUpdate!.Count);

		var repurchase = Assert.Single(plan.RepurchaseItems);
		Assert.Equal((100, StackableItemId, 3L, player.ObjectId), (repurchase.Item.ObjectId, repurchase.Item.ItemId, repurchase.Item.Count, repurchase.Item.OwnerId));
		Assert.Equal(60, repurchase.RepurchasePrice);
		Assert.Null(repurchase.Item.Creator);
	}

	[Fact]
	public void CreatePlan_UsesPurchaseTemplateGoodsAndBuyPriceRate()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);

		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 1_000, ownerId: player.ObjectId), sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1)],
			purchaseTemplate: new TradeListTemplateSummary(NpcId: 203060, GoodsListIds: [129], BuyPriceRate: 35),
			goodsLists: new GoodsListTable(
				goodsLists: [],
				goodsInLists: [],
				goodsPurchaseLists: [new GoodsListSummary(129, Items: [new GoodsListItemSummary(SwordItemId)])]));

		Assert.Equal(TradeSellToShopPlanStatus.PlanCreated, plan.Status);
		Assert.Equal(350, Assert.Single(plan.RepurchaseItems).RepurchasePrice);
		Assert.Equal(1_350, plan.KinahUpdate!.Count);
	}

	[Fact]
	public void CreatePlan_InvalidPurchaseTemplateItemBlocks()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);

		var plan = CreatePlan(
			player,
			inventoryItems: [sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1)],
			purchaseTemplate: new TradeListTemplateSummary(NpcId: 203060, GoodsListIds: [129], BuyPriceRate: 35),
			goodsLists: new GoodsListTable(
				goodsLists: [],
				goodsInLists: [],
				goodsPurchaseLists: [new GoodsListSummary(129, Items: [new GoodsListItemSummary(StackableItemId)])]));

		Assert.Equal(TradeSellToShopPlanStatus.BlockedInvalidPurchaseItem, plan.Status);
		Assert.Empty(plan.RepurchaseItems);
		Assert.Null(plan.KinahUpdate);
	}

	[Fact]
	public void CreatePlan_NotSellableBlocksWhenNoPurchaseTemplate()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);

		var plan = CreatePlan(
			player,
			inventoryItems: [sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1, IsSellable: false)]);

		Assert.Equal(TradeSellToShopPlanStatus.BlockedNotSellable, plan.Status);
		Assert.Empty(plan.RepurchaseItems);
	}

	[Fact]
	public void CreatePlan_SellLimitAdjustedZeroBreaksWithoutMutations()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);

		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 1_000, ownerId: player.ObjectId), sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1, SellLimitAdjustedCount: 0)]);

		Assert.Equal(TradeSellToShopPlanStatus.PlanCreated, plan.Status);
		Assert.Empty(plan.RepurchaseItems);
		Assert.Empty(plan.SellerDeletedItemObjectIds);
		Assert.Empty(plan.SellerItemUpdates);
		Assert.Equal(1_000, plan.KinahUpdate!.Count);
	}

	[Fact]
	public void CreatePlan_CountExceedsAvailableBlocks()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);

		var plan = CreatePlan(
			player,
			inventoryItems: [sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 2)]);

		Assert.Equal(TradeSellToShopPlanStatus.BlockedCountExceedsAvailable, plan.Status);
		Assert.Empty(plan.RepurchaseItems);
	}

	[Fact]
	public void CreatePlan_CannotTradeBlocksBeforeMutations()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);

		var plan = TradeSellToShopPlanService.CreatePlan(
			canTrade: false,
			player,
			inventoryItems: [sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1)],
			CreateTemplates(),
			purchaseTemplate: null,
			goodsLists: null,
			sellModifier: 20,
			nextObjectId: () => 100);

		Assert.Equal(TradeSellToShopPlanStatus.BlockedCannotTrade, plan.Status);
		Assert.Empty(plan.RepurchaseItems);
	}

	private static TradeSellToShopPlan CreatePlan(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		IReadOnlyList<TradeSellToShopItemRequest> tradeItems,
		TradeListTemplateSummary? purchaseTemplate = null,
		GoodsListTable? goodsLists = null)
	{
		var nextObjectId = 99;
		return TradeSellToShopPlanService.CreatePlan(
			canTrade: true,
			player,
			inventoryItems,
			tradeItems,
			CreateTemplates(),
			purchaseTemplate,
			goodsLists,
			sellModifier: 20,
			() => ++nextObjectId);
	}

	private static InventoryItem Item(int objectId, int itemId, long count, int ownerId, string? creator = null, int enchant = 0)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			OwnerId = ownerId,
			Location = 0,
			Slot = 65535,
			Creator = creator,
			Enchant = enchant,
		};
	}

	private static ItemTemplateTable CreateTemplates()
	{
		return new ItemTemplateTable(
		[
			Template(KinahItemId, price: 0, maxStackCount: 1),
			Template(SwordItemId, price: 1_000, maxStackCount: 1),
			Template(StackableItemId, price: 100, maxStackCount: 10),
		]);
	}

	private static ItemTemplateSummary Template(int itemId, long price, int maxStackCount)
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
			Price: price,
			ValidEquipmentSlots: 0);
	}

	private const int KinahItemId = 182400001;
	private const int SwordItemId = 100000001;
	private const int StackableItemId = 182003001;
}
