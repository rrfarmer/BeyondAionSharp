using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TradeSellForApToShopPlanServiceTests
{
	[Fact]
	public void CreatePlan_DeletesItemsAndPlansAbyssPointRewards()
	{
		var sword = Item(200, ApSwordItemId, 2);
		var shield = Item(201, ApShieldItemId, 1);

		var plan = CreatePlan(
			inventoryItems: [sword, shield],
			tradeItems:
			[
				new TradeSellForApToShopItemRequest(sword.ObjectId, Count: 2),
				new TradeSellForApToShopItemRequest(shield.ObjectId, Count: 1),
			]);

		Assert.Equal(TradeSellForApToShopPlanStatus.PlanCreated, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Equal([sword.ObjectId, shield.ObjectId], plan.DeletedItemObjectIds);
		Assert.Empty(plan.UpdatedItems);
		Assert.Empty(plan.SkippedDeleteFailedItemObjectIds);
		Assert.Equal(654, plan.TotalAbyssPoints);
		Assert.Equal([436, 218], plan.AbyssPointRewards.Select(reward => reward.ApReward).ToArray());
		Assert.Contains(TradeSellForApToShopStep.PlanAbyssPointReward, plan.Steps);
	}

	[Fact]
	public void CreatePlan_UpdatesPartialStackAndPlansAbyssPointReward()
	{
		var sword = Item(200, ApSwordItemId, 3);

		var plan = CreatePlan(
			inventoryItems: [sword],
			tradeItems: [new TradeSellForApToShopItemRequest(sword.ObjectId, Count: 2)]);

		Assert.Equal(TradeSellForApToShopPlanStatus.PlanCreated, plan.Status);
		Assert.Empty(plan.DeletedItemObjectIds);
		var update = Assert.Single(plan.UpdatedItems);
		Assert.Equal((sword.ObjectId, ApSwordItemId, 1L), (update.ObjectId, update.ItemId, update.Count));
		Assert.Empty(plan.SkippedDeleteFailedItemObjectIds);
		Assert.Equal(436, plan.TotalAbyssPoints);
	}

	[Fact]
	public void CreatePlan_BlocksWhenApItemSellingDisabledBeforeTradeRestriction()
	{
		var plan = CreatePlan(
			sellingApItemsEnabled: false,
			canTrade: false,
			inventoryItems: [],
			tradeItems: [new TradeSellForApToShopItemRequest(200, Count: 1)]);

		Assert.Equal(TradeSellForApToShopPlanStatus.BlockedSellingApItemsDisabled, plan.Status);
		Assert.DoesNotContain(TradeSellForApToShopStep.CheckPlayerCanTrade, plan.Steps);
	}

	[Fact]
	public void CreatePlan_BlocksCannotTradeBeforeInventoryLookup()
	{
		var plan = CreatePlan(
			canTrade: false,
			inventoryItems: [Item(200, ApSwordItemId, 1)],
			tradeItems: [new TradeSellForApToShopItemRequest(200, Count: 1)]);

		Assert.Equal(TradeSellForApToShopPlanStatus.BlockedCannotTrade, plan.Status);
		Assert.DoesNotContain(TradeSellForApToShopStep.FindInventoryItem, plan.Steps);
	}

	[Fact]
	public void CreatePlan_BlocksMissingInventoryItemLikeJava()
	{
		var plan = CreatePlan(
			inventoryItems: [Item(200, ApSwordItemId, 1)],
			tradeItems: [new TradeSellForApToShopItemRequest(999, Count: 1)]);

		Assert.Equal(TradeSellForApToShopPlanStatus.BlockedMissingItem, plan.Status);
		Assert.Equal(999, plan.RejectedItemObjectId);
		Assert.Empty(plan.AbyssPointRewards);
	}

	[Fact]
	public void CreatePlan_BlocksMissingTemplateBeforePurchaseValidation()
	{
		var item = Item(200, itemId: 999999, count: 1);

		var plan = CreatePlan(
			inventoryItems: [item],
			tradeItems: [new TradeSellForApToShopItemRequest(item.ObjectId, Count: 1)]);

		Assert.Equal(TradeSellForApToShopPlanStatus.BlockedMissingTemplate, plan.Status);
		Assert.Equal(item.ObjectId, plan.RejectedItemObjectId);
		Assert.DoesNotContain(TradeSellForApToShopStep.ValidatePurchaseTemplateGoods, plan.Steps);
	}

	[Fact]
	public void CreatePlan_BlocksItemNotListedInPurchaseGoods()
	{
		var item = Item(200, ApSwordItemId, 1);

		var plan = CreatePlan(
			inventoryItems: [item],
			tradeItems: [new TradeSellForApToShopItemRequest(item.ObjectId, Count: 1)],
			goodsLists: new GoodsListTable(
				goodsLists: [],
				goodsInLists: [],
				goodsPurchaseLists: [new GoodsListSummary(PurchaseGoodsListId, Items: [new GoodsListItemSummary(ApShieldItemId)])]));

		Assert.Equal(TradeSellForApToShopPlanStatus.BlockedInvalidPurchaseItem, plan.Status);
		Assert.Equal(item.ObjectId, plan.RejectedItemObjectId);
		Assert.Empty(plan.DeletedItemObjectIds);
	}

	[Fact]
	public void CreatePlan_DeleteFailureSkipsAbyssPointRewardAndContinuesLikeJava()
	{
		var sword = Item(200, ApSwordItemId, 1);
		var shield = Item(201, ApShieldItemId, 1);

		var plan = CreatePlan(
			inventoryItems: [sword, shield],
			tradeItems:
			[
				new TradeSellForApToShopItemRequest(sword.ObjectId, Count: 1, InventoryDecreaseSucceeds: false),
				new TradeSellForApToShopItemRequest(shield.ObjectId, Count: 1),
			]);

		Assert.Equal(TradeSellForApToShopPlanStatus.PlanCreated, plan.Status);
		Assert.Equal([sword.ObjectId], plan.SkippedDeleteFailedItemObjectIds);
		Assert.Equal([shield.ObjectId], plan.DeletedItemObjectIds);
		Assert.Empty(plan.UpdatedItems);
		Assert.Equal(218, plan.TotalAbyssPoints);
		Assert.Equal(shield.ObjectId, Assert.Single(plan.AbyssPointRewards).ItemObjectId);
	}

	[Fact]
	public void CreatePlan_TooLargeCountSkipsAbyssPointRewardAndContinuesLikeJava()
	{
		var sword = Item(200, ApSwordItemId, 1);
		var shield = Item(201, ApShieldItemId, 1);

		var plan = CreatePlan(
			inventoryItems: [sword, shield],
			tradeItems:
			[
				new TradeSellForApToShopItemRequest(sword.ObjectId, Count: 2),
				new TradeSellForApToShopItemRequest(shield.ObjectId, Count: 1),
			]);

		Assert.Equal(TradeSellForApToShopPlanStatus.PlanCreated, plan.Status);
		Assert.Equal([sword.ObjectId], plan.SkippedDeleteFailedItemObjectIds);
		Assert.Equal([shield.ObjectId], plan.DeletedItemObjectIds);
		Assert.Empty(plan.UpdatedItems);
		Assert.Equal(218, plan.TotalAbyssPoints);
	}

	[Fact]
	public void CreatePlan_UsesJavaMathRoundAndCountCastThroughFormulaService()
	{
		var item = Item(200, ApRoundingItemId, 10);

		var plan = CreatePlan(
			inventoryItems: [item],
			tradeItems: [new TradeSellForApToShopItemRequest(item.ObjectId, Count: 3)]);

		Assert.Equal(489, plan.TotalAbyssPoints);
		Assert.Equal(1_255, Assert.Single(plan.AbyssPointRewards).RequiredApPerItem);
	}

	[Fact]
	public void CreateDisabledOutcome_ComposesInventoryApAndPacketBoundariesWithoutDispatch()
	{
		var item = Item(200, ApSwordItemId, 1);
		var plan = CreatePlan(
			inventoryItems: [item],
			tradeItems: [new TradeSellForApToShopItemRequest(item.ObjectId, Count: 1)]);

		var outcome = TradeSellForApToShopOutcomePlanService.CreateDisabledPlan(plan);

		Assert.Equal(TradeSellForApToShopOutcomePlanStatus.DisabledNoTransaction, outcome.Status);
		Assert.Same(plan, outcome.SellForApToShopPlan);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.True(outcome.WouldMutateAbyssPoints);
		Assert.True(outcome.WouldSendPackets);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
		Assert.Contains(outcome.Steps, step => step.Kind == TradeSellForApToShopOutcomeStepKind.PersistRepositoryWrites);
		Assert.Contains(outcome.Steps, step => step.Kind == TradeSellForApToShopOutcomeStepKind.DispatchPacketIntents);
	}

	[Fact]
	public void CreateDisabledOutcome_FeatureDisabledCarriesOnlyJavaMessageSend()
	{
		var plan = CreatePlan(
			sellingApItemsEnabled: false,
			inventoryItems: [],
			tradeItems: [new TradeSellForApToShopItemRequest(200, Count: 1)]);

		var outcome = TradeSellForApToShopOutcomePlanService.CreateDisabledPlan(plan);

		Assert.Equal(TradeSellForApToShopOutcomePlanStatus.DisabledNoTransaction, outcome.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldMutateAbyssPoints);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.Single(outcome.Steps);
		Assert.Contains("disabled message", Assert.Single(outcome.Steps).JavaSource, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void CreateDisabledOutcome_BlockedPlanStopsBeforeOutcomeSideEffects()
	{
		var plan = CreatePlan(
			canTrade: false,
			inventoryItems: [Item(200, ApSwordItemId, 1)],
			tradeItems: [new TradeSellForApToShopItemRequest(200, Count: 1)]);

		var outcome = TradeSellForApToShopOutcomePlanService.CreateDisabledPlan(plan);

		Assert.Equal(TradeSellForApToShopOutcomePlanStatus.SellForApToShopPlanNotReady, outcome.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
	}

	private static TradeSellForApToShopPlan CreatePlan(
		bool sellingApItemsEnabled = true,
		bool canTrade = true,
		IReadOnlyList<InventoryItem>? inventoryItems = null,
		IReadOnlyList<TradeSellForApToShopItemRequest>? tradeItems = null,
		GoodsListTable? goodsLists = null)
	{
		return TradeSellForApToShopPlanService.CreatePlan(
			sellingApItemsEnabled,
			canTrade,
			inventoryItems ?? [],
			tradeItems ?? [],
			CreateTemplates(),
			new TradeListTemplateSummary(
				NpcId: 203060,
				GoodsListIds: [PurchaseGoodsListId],
				NpcType: "ABYSS",
				BuyPriceRate: 13),
			goodsLists ?? new GoodsListTable(
				goodsLists: [],
				goodsInLists: [],
				goodsPurchaseLists:
				[
					new GoodsListSummary(
						PurchaseGoodsListId,
						Items:
						[
							new GoodsListItemSummary(ApSwordItemId),
							new GoodsListItemSummary(ApShieldItemId),
							new GoodsListItemSummary(ApRoundingItemId),
						]),
				]));
	}

	private static ItemTemplateTable CreateTemplates()
	{
		return new ItemTemplateTable(
		[
			Template(ApSwordItemId, requiredAbyssPoints: 1_680),
			Template(ApShieldItemId, requiredAbyssPoints: 1_680),
			Template(ApRoundingItemId, requiredAbyssPoints: 1_255),
		]);
	}

	private static ItemTemplateSummary Template(int itemId, int requiredAbyssPoints)
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
			MaxStackCount: 1,
			Price: 1_000,
			ValidEquipmentSlots: 0,
			RequiredAbyssPoints: requiredAbyssPoints);
	}

	private static InventoryItem Item(int objectId, int itemId, long count)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			OwnerId = 1001,
			Location = 0,
			Slot = 65535,
		};
	}

	private const int PurchaseGoodsListId = 129;
	private const int ApSwordItemId = 100000001;
	private const int ApShieldItemId = 100000002;
	private const int ApRoundingItemId = 100000003;
}
