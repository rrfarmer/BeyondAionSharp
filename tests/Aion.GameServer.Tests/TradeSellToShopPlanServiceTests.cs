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

	[Fact]
	public void CreateDisabledOutcome_ComposesSellPersistenceAndPacketsWithoutDispatch()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);
		var plan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 1_000, ownerId: player.ObjectId), sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1)]);

		var outcome = TradeSellToShopOutcomePlanService.CreateDisabledPlan(plan);

		Assert.Equal(TradeSellToShopOutcomePlanStatus.DisabledNoTransaction, outcome.Status);
		Assert.Same(plan, outcome.SellToShopPlan);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.True(outcome.WouldAddRepurchaseItems);
		Assert.True(outcome.WouldMutateKinah);
		Assert.True(outcome.WouldSendPackets);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
		Assert.Contains(outcome.Steps, step => step.Kind == TradeSellToShopOutcomeStepKind.PersistRepositoryWrites);
		Assert.Contains(outcome.Steps, step => step.Kind == TradeSellToShopOutcomeStepKind.DispatchPacketIntents);
	}

	[Fact]
	public void CreateRepurchaseDiagnosticSnapshot_CarriesSuccessfulSellRepurchaseItemsWithoutLiveState()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);
		var sellPlan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 1_000, ownerId: player.ObjectId), sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1)]);

		var snapshot = RepurchaseDiagnosticSnapshotPlanService.CreateDisabledPlan(sellPlan);

		Assert.Equal(RepurchaseDiagnosticSnapshotPlanStatus.SnapshotCreated, snapshot.Status);
		Assert.Same(sellPlan, snapshot.SellToShopPlan);
		Assert.Equal(sellPlan.RepurchaseItems, snapshot.RepurchaseItems);
		Assert.NotNull(snapshot.StateReplacementPlan);
		Assert.Equal(RepurchaseStateReplacePlanStatus.SnapshotReplaced, snapshot.StateReplacementPlan!.Status);
		Assert.Equal(player.ObjectId, snapshot.StateReplacementPlan.PlayerObjectId);
		Assert.Equal([sword.ObjectId], snapshot.StateReplacementPlan.Snapshot.RepurchaseItems.Select(item => item.Item.ObjectId));
		Assert.False(snapshot.StateReplacementPlan.PreservesJavaHashSetIterationOrder);
		Assert.True(snapshot.WouldReplacePlayerSnapshot);
		Assert.False(snapshot.DidReplacePlayerSnapshot);
		Assert.False(snapshot.ShouldDispatchLiveSideEffects);
		Assert.False(snapshot.IsLive);
		Assert.Contains("RepurchaseService.addRepurchaseItems", snapshot.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateRepurchaseDiagnosticSnapshot_StillCreatesEmptySnapshotForSuccessfulEmptySellList()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);
		var sellPlan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 1_000, ownerId: player.ObjectId), sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1, SellLimitAdjustedCount: 0)]);

		var snapshot = RepurchaseDiagnosticSnapshotPlanService.CreateDisabledPlan(sellPlan);

		Assert.Equal(RepurchaseDiagnosticSnapshotPlanStatus.SnapshotCreated, snapshot.Status);
		Assert.Empty(snapshot.RepurchaseItems);
		Assert.NotNull(snapshot.StateReplacementPlan);
		Assert.Equal(player.ObjectId, snapshot.StateReplacementPlan!.PlayerObjectId);
		Assert.Empty(snapshot.StateReplacementPlan.Snapshot.RepurchaseItems);
		Assert.True(snapshot.WouldReplacePlayerSnapshot);
		Assert.False(snapshot.DidReplacePlayerSnapshot);
	}

	[Fact]
	public void CreateRepurchaseDiagnosticSnapshot_CarriesStateReplacementAgainstSuppliedCurrentSnapshots()
	{
		var player = new Player { ObjectId = 1001 };
		var oldSnapshot = new RepurchaseStateSnapshot(
			player.ObjectId,
			[new RepurchaseSourceItem(Item(777, SwordItemId, 1, ownerId: player.ObjectId), RepurchasePrice: 1)],
			"old snapshot");
		var otherSnapshot = new RepurchaseStateSnapshot(
			1002,
			[new RepurchaseSourceItem(Item(888, SwordItemId, 1, ownerId: 1002), RepurchasePrice: 1)],
			"other snapshot");
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);
		var sellPlan = CreatePlan(
			player,
			inventoryItems: [Item(99, KinahItemId, 1_000, ownerId: player.ObjectId), sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1)]);

		var snapshot = RepurchaseDiagnosticSnapshotPlanService.CreateDisabledPlan(
			sellPlan,
			player.ObjectId,
			[oldSnapshot, otherSnapshot]);

		Assert.Equal(RepurchaseDiagnosticSnapshotPlanStatus.SnapshotCreated, snapshot.Status);
		var replacement = Assert.IsType<RepurchaseStateReplacePlan>(snapshot.StateReplacementPlan);
		Assert.True(replacement.WouldReplaceMapEntry);
		Assert.False(replacement.DidReplaceMapEntry);
		Assert.Equal([player.ObjectId], replacement.Snapshot.RepurchaseItems.Select(item => item.Item.OwnerId).Distinct());
		Assert.DoesNotContain(replacement.UpdatedSnapshots, state => state.PlayerObjectId == player.ObjectId && state.JavaSource == oldSnapshot.JavaSource);
		Assert.Contains(otherSnapshot, replacement.UpdatedSnapshots);
		Assert.Contains("disabled state replacement payload", snapshot.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateRepurchaseDiagnosticSnapshot_BlockedSellPlanDoesNotReplaceSnapshot()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);
		var sellPlan = TradeSellToShopPlanService.CreatePlan(
			canTrade: false,
			player,
			inventoryItems: [sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1)],
			CreateTemplates(),
			purchaseTemplate: null,
			goodsLists: null,
			sellModifier: 20,
			nextObjectId: () => 100);

		var snapshot = RepurchaseDiagnosticSnapshotPlanService.CreateDisabledPlan(sellPlan);

		Assert.Equal(RepurchaseDiagnosticSnapshotPlanStatus.SellToShopPlanNotReady, snapshot.Status);
		Assert.Empty(snapshot.RepurchaseItems);
		Assert.Null(snapshot.StateReplacementPlan);
		Assert.False(snapshot.WouldReplacePlayerSnapshot);
		Assert.False(snapshot.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreateDisabledOutcome_NotSellableCarriesOnlyJavaSystemPacket()
	{
		var player = new Player { ObjectId = 1001 };
		var sword = Item(200, SwordItemId, 1, ownerId: player.ObjectId);
		var plan = CreatePlan(
			player,
			inventoryItems: [sword],
			tradeItems: [new TradeSellToShopItemRequest(sword.ObjectId, Count: 1, IsSellable: false)]);

		var outcome = TradeSellToShopOutcomePlanService.CreateDisabledPlan(plan);

		Assert.Equal(TradeSellToShopOutcomePlanStatus.DisabledNoTransaction, outcome.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldAddRepurchaseItems);
		Assert.False(outcome.WouldMutateKinah);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.Single(outcome.Steps);
		Assert.Contains("CAN_NOT_BE_SELLED", Assert.Single(outcome.Steps).JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateDisabledOutcome_BlockedPlanStopsBeforeMutationOutcome()
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

		var outcome = TradeSellToShopOutcomePlanService.CreateDisabledPlan(plan);

		Assert.Equal(TradeSellToShopOutcomePlanStatus.SellToShopPlanNotReady, outcome.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
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
