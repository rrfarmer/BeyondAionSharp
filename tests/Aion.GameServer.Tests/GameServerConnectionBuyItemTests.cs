using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionBuyItemTests
{
	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemWithoutActivePlayerRecordsSilentNoPlayerPlan()
	{
		await using var fixture = await BuyItemFixture.CreateAsync();
		SetConnectionState(fixture.Connection, GameConnectionState.InGame);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedMissingPlayer, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemUnknownTargetRecordsNonLiveDiagnosticPlan()
	{
		await using var fixture = await BuyItemFixture.CreateAsync();
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedUnknownTarget, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.Null(outcome.BuyFromShopOutcomePlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcTargetSelectsNonLiveBuyFromShopPlanner()
	{
		await using var fixture = await BuyItemFixture.CreateAsync();
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedBuyFromShopPlanner, plan.Status);
		Assert.NotNull(plan.BuyFromShopPlan);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.BuyFromShopOutcomeCreated, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.Equal(TradeBuyTransactionOutcomePlanStatus.MissingTransactionPlan, outcome.BuyFromShopOutcomePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcRepurchaseExecutesLiveFromSnapshot()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			playerEnterWorldRepository: playerRepository);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 5_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		player.RepurchaseItems =
		[
			new RepurchaseSourceItem(
				new InventoryItem
				{
					ObjectId = 7101,
					ItemId = 100000001,
					Count = 1,
					OwnerId = player.ObjectId,
					Location = 0,
					Slot = 65535,
				},
				RepurchasePrice: 1_200),
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 2, [(7101, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedRepurchasePlanner, plan.Status);
		Assert.Equal(CmBuyItemRepurchaseCompositionPlanStatus.WouldDispatchRepurchase, plan.RepurchasePlan!.Status);
		Assert.Equal([7101], plan.RepurchasePlan.ReadPlan.RepurchaseItemObjectIds);
		var dispatch = Assert.IsType<CmBuyItemRepurchaseDispatchDescriptor>(plan.RepurchasePlan.RunPlan.Dispatch);
		var repurchasePlan = Assert.IsType<RepurchasePlan>(dispatch.RepurchasePlan);
		Assert.Equal(RepurchasePlanStatus.PlanCreated, repurchasePlan.Status);
		Assert.Equal([7101], repurchasePlan.RepurchasedItemObjectIds);
		Assert.Equal([7101], repurchasePlan.RemovedRepurchaseItemObjectIds);
		Assert.Equal(3_800, repurchasePlan.KinahUpdate!.Count);
		var addedPlanItem = Assert.Single(repurchasePlan.AddedItems);
		Assert.Equal((8001, 100000001, 1L, player.ObjectId, 0), (
			addedPlanItem.ObjectId,
			addedPlanItem.ItemId,
			addedPlanItem.Count,
			addedPlanItem.OwnerId,
			addedPlanItem.Location));
		Assert.False(plan.ShouldDispatchLiveSideEffects);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.RepurchaseOutcomeCreated, outcome.Status);
		Assert.Equal(RepurchaseOutcomePlanStatus.DisabledNoTransaction, outcome.RepurchaseOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateBuyerInventory);
		Assert.True(outcome.WouldMutateKinah);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.NotNull(outcome.RepurchaseOutcomePlan.StateItemRemovalPlan);
		var stateRemoval = outcome.RepurchaseOutcomePlan.StateItemRemovalPlan!;
		Assert.Equal(RepurchaseStateItemRemovalPlanStatus.SnapshotUpdated, stateRemoval.Status);
		Assert.Equal([7101], stateRemoval.RemovedItemObjectIds);
		Assert.Empty(stateRemoval.UpdatedSnapshot!.RepurchaseItems);
		Assert.False(stateRemoval.DidRemoveItems);
		Assert.False(stateRemoval.IsLive);
		Assert.Equal(1, playerRepository.SaveNpcShopRepurchaseMutationCalls);
		Assert.NotNull(playerRepository.NpcShopRepurchasePersistence);
		var persistence = playerRepository.NpcShopRepurchasePersistence!;
		Assert.Equal(player.ObjectId, persistence.PlayerObjectId);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 3_800L), (
			persistence.KinahItem!.ObjectId,
			persistence.KinahItem.ItemId,
			persistence.KinahItem.Count));
		Assert.Empty(persistence.UpdatedItems);
		var persistedAddedItem = Assert.Single(persistence.AddedItems);
		Assert.Equal((8001, 100000001, 1L, player.ObjectId, 0), (
			persistedAddedItem.ObjectId,
			persistedAddedItem.ItemId,
			persistedAddedItem.Count,
			persistedAddedItem.OwnerId,
			persistedAddedItem.Location));
		Assert.Equal(3_800, player.InventoryItems.Single(item => item.ItemId == InventoryItemFactory.KinahItemId).Count);
		Assert.Equal(8001, player.InventoryItems.Single(item => item.ItemId == 100000001).ObjectId);
		Assert.Empty(player.RepurchaseItems);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => AssertInventoryAddItemPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 8001,
				expectedItemId: 100000001,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.ItemCollect),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopHydratesDisabledBuyTransactionPlan()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(
					1001,
					price: 500,
					requiredAbyssPoints: 1_000,
					acquisitionType: "AP",
					acquisitionItemId: 186000001,
					acquisitionItemCount: 3)));
		var player = CreatePlayer();
		player.AbyssRank = player.AbyssRank with { Ap = 2_500 };
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
			new InventoryItem
			{
				ObjectId = 3002,
				ItemId = 186000001,
				Count = 6,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 1,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedBuyFromShopPlanner, plan.Status);
		var dispatch = Assert.IsType<CmBuyItemBuyFromShopDispatchDescriptor>(plan.BuyFromShopPlan!.Dispatch);
		Assert.True(dispatch.UseKinah);
		Assert.NotNull(dispatch.TradeTemplate);
		var transactionPlan = Assert.IsType<TradeBuyTransactionPlan>(dispatch.BuyTransactionPlan);
		Assert.Equal(TradeBuyTransactionPlanStatus.WouldApplyBuyTransaction, transactionPlan.Status);
		Assert.Equal(500, transactionPlan.RequiredKinah);
		Assert.Equal(1_000, transactionPlan.RequiredAbyssPoints);
		Assert.Equal(new TradeBuyTransactionRequiredItem(186000001, 6), Assert.Single(transactionPlan.RequiredItems));
		Assert.False(transactionPlan.ShouldDispatchLiveSideEffects);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.BuyFromShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeBuyTransactionOutcomePlanStatus.DisabledNoTransaction, outcome.BuyFromShopOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopHydratesJavaBuyPriceDiagnostics()
	{
		var options = new GameServerOptions
		{
			Prices = new GameServerPriceOptions
			{
				DefaultPrices = 100,
				DefaultModifier = 90,
				DefaultTaxes = 100,
				VendorBuyModifier = 125,
				VendorSellModifier = 20,
			},
		};
		await using var fixture = await BuyItemFixture.CreateAsync(
			options: options,
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 12_345)),
			buyItemPriceInfluenceRates: new PriceInfluenceRates(Elyos: 0.5f, Asmodians: 0.3f));
		var player = new Player
		{
			ObjectId = 1001,
			Name = "BuyItemTester",
			Race = "ASMODIANS",
			PlayerClass = "RANGER",
			Level = 1,
			IsOnline = true,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 20_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		var dispatch = Assert.IsType<CmBuyItemBuyFromShopDispatchDescriptor>(plan.BuyFromShopPlan!.Dispatch);
		var transactionPlan = Assert.IsType<TradeBuyTransactionPlan>(dispatch.BuyTransactionPlan);
		Assert.Equal(TradeBuyTransactionPlanStatus.WouldApplyBuyTransaction, transactionPlan.Status);
		Assert.Equal(new PriceSnapshot(GlobalPrices: 110, GlobalPricesModifier: 90, Taxes: 105, VendorBuyModifier: 125, VendorSellModifier: 20), transactionPlan.PriceSnapshot);
		Assert.Equal(16_039, Assert.Single(transactionPlan.Mutation!.AddedItems).UnitBuyPrice);
		Assert.Equal(16_039, transactionPlan.RequiredKinah);
		Assert.False(transactionPlan.ShouldDispatchLiveSideEffects);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.BuyFromShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeBuyTransactionOutcomePlanStatus.DisabledNoTransaction, outcome.BuyFromShopOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopExecutesNormalKinahPurchaseWithoutObservers()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, maxStackCount: 100),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		Assert.Empty(fixture.BuyItemPlans);
		Assert.Empty(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.Count == 9_500);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 8001 && item.ItemId == 1001 && item.Count == 2);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => AssertInventoryAddItemPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 8001,
				expectedItemId: 1001,
				expectedCount: 2,
				expectedAddType: SmInventoryAddItem.Buy),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
		Assert.Collection(
			fixture.PacketEvents,
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, AssertPacketEvent<SmInventoryUpdateItem>(packet, "active").UpdateType),
			packet => AssertInventoryAddItemPayload(
				AssertPacketEvent<SmInventoryAddItem>(packet, "active"),
				expectedObjectId: 8001,
				expectedItemId: 1001,
				expectedCount: 2,
				expectedAddType: SmInventoryAddItem.Buy),
			packet => AssertCubeUpdatePayload(AssertPacketEvent<SmCubeUpdate>(packet, "active"), expectedItemsCount: 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopPersistsSuccessfulNormalKinahPurchaseBeforePackets()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001), new GoodsListItemSummary(1002)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, maxStackCount: 100),
				Template(1002, price: 500, maxStackCount: 100),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			playerEnterWorldRepository: playerRepository,
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
			new InventoryItem
			{
				ObjectId = 3002,
				ItemId = 1001,
				Count = 3,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 1,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2), (1002, 1)]));

		Assert.Equal(1, playerRepository.SaveNpcShopBuyMutationCalls);
		Assert.NotNull(playerRepository.NpcShopBuyPersistence);
		var persistence = playerRepository.NpcShopBuyPersistence;
		Assert.Equal(player.ObjectId, persistence.PlayerObjectId);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 9_250L), (
			persistence.KinahItem!.ObjectId,
			persistence.KinahItem.ItemId,
			persistence.KinahItem.Count));
		var updatedItem = Assert.Single(persistence.UpdatedItems);
		Assert.Equal((3002, 1001, 5L, player.ObjectId, 0), (
			updatedItem.ObjectId,
			updatedItem.ItemId,
			updatedItem.Count,
			updatedItem.OwnerId,
			updatedItem.Location));
		var addedItem = Assert.Single(persistence.AddedItems);
		Assert.Equal((8001, 1002, 1L, player.ObjectId, 0), (
			addedItem.ObjectId,
			addedItem.ItemId,
			addedItem.Count,
			addedItem.OwnerId,
			addedItem.Location));
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.Count == 9_250);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3002 && item.Count == 5);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 8001 && item.ItemId == 1002 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => Assert.Equal(SmInventoryUpdateItem.IncreaseItemBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => AssertInventoryAddItemPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 8001,
				expectedItemId: 1002,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.Buy),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopConsumesRequiredItemStackBeforeRewardAdd()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, maxStackCount: 100, acquisitionItemId: 186000001, acquisitionItemCount: 2),
				Template(186000001, price: 1, maxStackCount: 100),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			playerEnterWorldRepository: playerRepository,
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
			new InventoryItem
			{
				ObjectId = 3002,
				ItemId = 186000001,
				Count = 5,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 1,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Equal(1, playerRepository.SaveNpcShopBuyMutationCalls);
		Assert.NotNull(playerRepository.NpcShopBuyPersistence);
		var persistence = playerRepository.NpcShopBuyPersistence!;
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 9_750L), (
			persistence.KinahItem!.ObjectId,
			persistence.KinahItem.ItemId,
			persistence.KinahItem.Count));
		var requiredUpdate = Assert.Single(persistence.RequiredItemUpdates);
		Assert.Equal((3002, 186000001, 3L), (requiredUpdate.ObjectId, requiredUpdate.ItemId, requiredUpdate.Count));
		Assert.Empty(persistence.DeletedRequiredItemObjectIds);
		var addedItem = Assert.Single(persistence.AddedItems);
		Assert.Equal((8001, 1001, 1L), (addedItem.ObjectId, addedItem.ItemId, addedItem.Count));
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.Count == 9_750);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3002 && item.Count == 3);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 8001 && item.ItemId == 1001 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseItemUse, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => AssertInventoryAddItemPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 8001,
				expectedItemId: 1001,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.Buy),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopDeletesRequiredItemStackAtZero()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, maxStackCount: 100, acquisitionItemId: 186000001, acquisitionItemCount: 2),
				Template(186000001, price: 1, maxStackCount: 100),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			playerEnterWorldRepository: playerRepository,
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
			new InventoryItem
			{
				ObjectId = 3002,
				ItemId = 186000001,
				Count = 2,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 1,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Equal(1, playerRepository.SaveNpcShopBuyMutationCalls);
		Assert.NotNull(playerRepository.NpcShopBuyPersistence);
		var persistence = playerRepository.NpcShopBuyPersistence!;
		Assert.Empty(persistence.RequiredItemUpdates);
		Assert.Equal([3002], persistence.DeletedRequiredItemObjectIds);
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 3002);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.Count == 9_750);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 8001 && item.ItemId == 1001 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), 3002, SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertInventoryAddItemPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 8001,
				expectedItemId: 1001,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.Buy),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopSpendsAbyssPointsBeforeInventoryPackets()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, requiredAbyssPoints: 2_000, maxStackCount: 100, acquisitionType: "AP"),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			playerEnterWorldRepository: playerRepository,
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 2_500, Rank = 2, MaxRank = 2 };
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Equal(1, playerRepository.SaveNpcShopBuyMutationCalls);
		Assert.NotNull(playerRepository.NpcShopBuyPersistence);
		var persistence = playerRepository.NpcShopBuyPersistence!;
		Assert.NotNull(persistence.AbyssRank);
		Assert.Equal(1_500, persistence.AbyssRank!.Ap);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 9_750L), (
			persistence.KinahItem!.ObjectId,
			persistence.KinahItem.ItemId,
			persistence.KinahItem.Count));
		var addedItem = Assert.Single(persistence.AddedItems);
		Assert.Equal((8001, 1001, 1L), (addedItem.ObjectId, addedItem.ItemId, addedItem.Count));
		Assert.Equal(1_500, player.AbyssRank.Ap);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.Count == 9_750);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 8001 && item.ItemId == 1001 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(1300965, Assert.IsType<SmSystemMessage>(packet).MessageId),
			packet => AssertAbyssRankPayload(Assert.IsType<SmAbyssRank>(packet), expectedAp: 1_500, expectedRank: 2),
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => AssertInventoryAddItemPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 8001,
				expectedItemId: 1001,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.Buy),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
		Assert.Empty(fixture.Registry.VisibleBroadcasts);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcAbyssKinahBuyFromShopUsesSecondaryRatesLive()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(
					700001,
					[501],
					NpcType: "ABYSS_KINAH",
					SellPriceRate: 50,
					SellPriceRate2: 75,
					ApSellPriceRate2: 80)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 1_000, requiredAbyssPoints: 10_000, maxStackCount: 100, acquisitionType: "AP"),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			playerEnterWorldRepository: playerRepository,
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 10_000, Rank = 2, MaxRank = 2 };
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Equal(1, playerRepository.SaveNpcShopBuyMutationCalls);
		Assert.NotNull(playerRepository.NpcShopBuyPersistence);
		var persistence = playerRepository.NpcShopBuyPersistence!;
		Assert.Equal(2_000, persistence.AbyssRank!.Ap);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 9_250L), (
			persistence.KinahItem!.ObjectId,
			persistence.KinahItem.ItemId,
			persistence.KinahItem.Count));
		var addedItem = Assert.Single(persistence.AddedItems);
		Assert.Equal((8001, 1001, 1L), (addedItem.ObjectId, addedItem.ItemId, addedItem.Count));
		Assert.Equal(2_000, player.AbyssRank.Ap);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.Count == 9_250);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 8001 && item.ItemId == 1001 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(1300965, Assert.IsType<SmSystemMessage>(packet).MessageId),
			packet => AssertAbyssRankPayload(Assert.IsType<SmAbyssRank>(packet), expectedAp: 2_000, expectedRank: 2),
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => AssertInventoryAddItemPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 8001,
				expectedItemId: 1001,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.Buy),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcAbyssBuyFromShopSpendsApWithoutKinahLive()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "ABYSS", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 1_000, requiredAbyssPoints: 10_000, maxStackCount: 100, acquisitionType: "AP")),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			playerEnterWorldRepository: playerRepository,
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.AbyssRank = PlayerAbyssRank.Default() with { Ap = 6_000, Rank = 2, MaxRank = 2 };
		player.InventoryItems = [];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Equal(1, playerRepository.SaveNpcShopBuyMutationCalls);
		Assert.NotNull(playerRepository.NpcShopBuyPersistence);
		var persistence = playerRepository.NpcShopBuyPersistence!;
		Assert.Equal(1_000, persistence.AbyssRank!.Ap);
		Assert.Null(persistence.KinahItem);
		var addedItem = Assert.Single(persistence.AddedItems);
		Assert.Equal((8001, 1001, 1L), (addedItem.ObjectId, addedItem.ItemId, addedItem.Count));
		Assert.Equal(1_000, player.AbyssRank.Ap);
		Assert.DoesNotContain(player.InventoryItems, item => item.ItemId == InventoryItemFactory.KinahItemId);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 8001 && item.ItemId == 1001 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(1300965, Assert.IsType<SmSystemMessage>(packet).MessageId),
			packet => AssertAbyssRankPayload(Assert.IsType<SmAbyssRank>(packet), expectedAp: 1_000, expectedRank: 1),
			packet => AssertInventoryAddItemPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 8001,
				expectedItemId: 1001,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.Buy),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcRewardBuyFromShopConsumesTokenWithoutKinahLive()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "REWARD", SellPriceRate: 100)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 1_000, maxStackCount: 100, acquisitionItemId: 186000001, acquisitionItemCount: 2),
				Template(186000001, price: 1, maxStackCount: 100)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			playerEnterWorldRepository: playerRepository,
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3002,
				ItemId = 186000001,
				Count = 2,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 1,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Equal(1, playerRepository.SaveNpcShopBuyMutationCalls);
		Assert.NotNull(playerRepository.NpcShopBuyPersistence);
		var persistence = playerRepository.NpcShopBuyPersistence!;
		Assert.Null(persistence.AbyssRank);
		Assert.Null(persistence.KinahItem);
		Assert.Empty(persistence.RequiredItemUpdates);
		Assert.Equal([3002], persistence.DeletedRequiredItemObjectIds);
		var addedItem = Assert.Single(persistence.AddedItems);
		Assert.Equal((8001, 1001, 1L), (addedItem.ObjectId, addedItem.ItemId, addedItem.Count));
		Assert.DoesNotContain(player.InventoryItems, item => item.ItemId == InventoryItemFactory.KinahItemId);
		Assert.DoesNotContain(player.InventoryItems, item => item.ItemId == 186000001);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 8001 && item.ItemId == 1001 && item.Count == 1);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), 3002, SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => AssertInventoryAddItemPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 8001,
				expectedItemId: 1001,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.Buy),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopPersistenceFailureStopsMutationAndPackets()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository { SaveNpcShopBuyMutationResult = false };
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, maxStackCount: 100),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			playerEnterWorldRepository: playerRepository,
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		Assert.Equal(1, playerRepository.SaveNpcShopBuyMutationCalls);
		Assert.NotNull(playerRepository.NpcShopBuyPersistence);
		var kinah = Assert.Single(player.InventoryItems);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 10_000L), (kinah.ObjectId, kinah.ItemId, kinah.Count));
		Assert.Empty(fixture.SentPackets);
		Assert.Empty(fixture.PacketEvents);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopInsufficientKinahSendsLiveDenialWithoutMutation()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, maxStackCount: 100),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 499,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		Assert.Empty(fixture.BuyItemPlans);
		Assert.Empty(fixture.BuyItemSideEffectOutcomePlans);
		var kinah = Assert.Single(player.InventoryItems);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 499L), (kinah.ObjectId, kinah.ItemId, kinah.Count));
		var denial = Assert.Single(fixture.SentPackets);
		Assert.Equal(1300759, Assert.IsType<SmSystemMessage>(denial).MessageId);
		var packetEvent = Assert.Single(fixture.PacketEvents);
		Assert.Equal(1300759, AssertPacketEvent<SmSystemMessage>(packetEvent, "active").MessageId);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopInvalidGoodsSendsLiveDenialWithoutMutation()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, maxStackCount: 100),
				Template(1002, price: 250, maxStackCount: 100),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1002, 1)]));

		Assert.Empty(fixture.BuyItemPlans);
		Assert.Empty(fixture.BuyItemSideEffectOutcomePlans);
		var kinah = Assert.Single(player.InventoryItems);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 10_000L), (kinah.ObjectId, kinah.ItemId, kinah.Count));
		var denial = Assert.Single(fixture.SentPackets);
		AssertInvalidGoodsMessage(Assert.IsType<SmMessage>(denial));
		var packetEvent = Assert.Single(fixture.PacketEvents);
		AssertInvalidGoodsMessage(AssertPacketEvent<SmMessage>(packetEvent, "active"));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopFullInventorySendsLiveDenialWithoutMutation()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, maxStackCount: 100),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		var inventoryItems = new List<InventoryItem>
		{
			new()
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		};
		inventoryItems.AddRange(Enumerable.Range(0, 27).Select(index => new InventoryItem
		{
			ObjectId = 4000 + index,
			ItemId = 200000000 + index,
			Count = 1,
			OwnerId = player.ObjectId,
			Location = 0,
			Slot = index + 1,
		}));
		player.InventoryItems = inventoryItems;
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Empty(fixture.BuyItemPlans);
		Assert.Empty(fixture.BuyItemSideEffectOutcomePlans);
		Assert.DoesNotContain(player.InventoryItems, item => item.ItemId == 1001);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.ItemId == InventoryItemFactory.KinahItemId && item.Count == 10_000);
		Assert.Equal(28, player.InventoryItems.Count);
		var denial = Assert.Single(fixture.SentPackets);
		Assert.Equal(1300762, Assert.IsType<SmSystemMessage>(denial).MessageId);
		var packetEvent = Assert.Single(fixture.PacketEvents);
		Assert.Equal(1300762, AssertPacketEvent<SmSystemMessage>(packetEvent, "active").MessageId);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopLimitedItemPlanRecordsDisabledOutcomeAndSendsLiveDenial()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(
					501,
					SalesTime: "0 0 9 ? * MON",
					Items: [new GoodsListItemSummary(1001, SellLimit: 1, BuyLimit: 1)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500)));
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedBuyFromShopPlanner, plan.Status);
		var dispatch = Assert.IsType<CmBuyItemBuyFromShopDispatchDescriptor>(plan.BuyFromShopPlan!.Dispatch);
		var transactionPlan = Assert.IsType<TradeBuyTransactionPlan>(dispatch.BuyTransactionPlan);
		Assert.Equal(TradeBuyTransactionPlanStatus.BlockedLimitedItem, transactionPlan.Status);
		Assert.Equal(1001, transactionPlan.RejectedItem?.ItemId);
		Assert.Contains(TradeBuyTransactionStep.CheckLimitedItems, transactionPlan.Steps);
		Assert.False(transactionPlan.ShouldDispatchLiveSideEffects);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.BuyFromShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeBuyTransactionOutcomePlanStatus.DisabledNoTransaction, outcome.BuyFromShopOutcomePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		var denial = Assert.Single(fixture.SentPackets);
		Assert.Equal(1400353, Assert.IsType<SmSystemMessage>(denial).MessageId);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopLimitedItemSendsLiveDenialWithoutMutation()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(
					501,
					SalesTime: "0 0 9 ? * MON",
					Items: [new GoodsListItemSummary(1001, SellLimit: 1, BuyLimit: 1)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		Assert.Empty(fixture.BuyItemPlans);
		Assert.Empty(fixture.BuyItemSideEffectOutcomePlans);
		var kinah = Assert.Single(player.InventoryItems);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 10_000L), (kinah.ObjectId, kinah.ItemId, kinah.Count));
		var denial = Assert.Single(fixture.SentPackets);
		Assert.Equal(1400353, Assert.IsType<SmSystemMessage>(denial).MessageId);
		var packetEvent = Assert.Single(fixture.PacketEvents);
		Assert.Equal(1400353, AssertPacketEvent<SmSystemMessage>(packetEvent, "active").MessageId);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopSuccessfulLimitedItemUpdatesLiveCounter()
	{
		var tradeLists = CreateBuyTradeLists(
			new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50));
		var goodsLists = CreateBuyGoodsLists(
			new GoodsListSummary(
				501,
				SalesTime: "0 0 9 ? * MON",
				Items: [new GoodsListItemSummary(1001, SellLimit: 1, BuyLimit: 1)]));
		var limitedItemService = LimitedItemTradeService.Create(tradeLists, goodsLists);
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: tradeLists,
			buyItemGoodsLists: goodsLists,
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			limitedItemTradeService: limitedItemService,
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));
		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.Count == 9_750);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 8001 && item.ItemId == 1001 && item.Count == 1);
		Assert.Equal(2, player.InventoryItems.Count);
		var liveFact = Assert.Single(limitedItemService.GetLimitedItemFacts(700001, player.ObjectId));
		Assert.Equal((1001, 0, 1), (liveFact.ItemId, liveFact.SellLimit, liveFact.PlayerBuyCount));
		Assert.False(limitedItemService.CanBuy(700001, 1001, player.ObjectId, 1));
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => AssertInventoryAddItemPayload(
				Assert.IsType<SmInventoryAddItem>(packet),
				expectedObjectId: 8001,
				expectedItemId: 1001,
				expectedCount: 1,
				expectedAddType: SmInventoryAddItem.Buy),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => Assert.Equal(1400353, Assert.IsType<SmSystemMessage>(packet).MessageId));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopNotEnoughAbyssPointsSendsLiveDenialWithoutMutation()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, requiredAbyssPoints: 2_000, maxStackCount: 100, acquisitionType: "AP"),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.AbyssRank = player.AbyssRank with { Ap = 250 };
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Empty(fixture.BuyItemPlans);
		Assert.Empty(fixture.BuyItemSideEffectOutcomePlans);
		var kinah = Assert.Single(player.InventoryItems);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 10_000L), (kinah.ObjectId, kinah.ItemId, kinah.Count));
		Assert.Equal(250, player.AbyssRank.Ap);
		var denial = Assert.Single(fixture.SentPackets);
		Assert.Equal(1300927, Assert.IsType<SmSystemMessage>(denial).MessageId);
		var packetEvent = Assert.Single(fixture.PacketEvents);
		Assert.Equal(1300927, AssertPacketEvent<SmSystemMessage>(packetEvent, "active").MessageId);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopMissingRequiredItemSendsLiveDenialWithoutMutation()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(
					1001,
					price: 500,
					maxStackCount: 100,
					acquisitionItemId: 186000001,
					acquisitionItemCount: 2),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Empty(fixture.BuyItemPlans);
		Assert.Empty(fixture.BuyItemSideEffectOutcomePlans);
		var kinah = Assert.Single(player.InventoryItems);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 10_000L), (kinah.ObjectId, kinah.ItemId, kinah.Count));
		var denial = Assert.Single(fixture.SentPackets);
		Assert.Equal(1300927, Assert.IsType<SmSystemMessage>(denial).MessageId);
		var packetEvent = Assert.Single(fixture.PacketEvents);
		Assert.Equal(1300927, AssertPacketEvent<SmSystemMessage>(packetEvent, "active").MessageId);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopNegativeRequiredApSendsLiveDenialWithoutMutation()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateBuyTradeLists(
				new TradeListTemplateSummary(700001, [501], NpcType: "NORMAL", SellPriceRate: 50)),
			buyItemGoodsLists: CreateBuyGoodsLists(
				new GoodsListSummary(501, Items: [new GoodsListItemSummary(1001)])),
			buyItemItemTemplates: CreateItemTemplates(
				Template(1001, price: 500, requiredAbyssPoints: -100, maxStackCount: 100, acquisitionType: "AP"),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001),
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [2]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 1)]));

		Assert.Empty(fixture.BuyItemPlans);
		Assert.Empty(fixture.BuyItemSideEffectOutcomePlans);
		var kinah = Assert.Single(player.InventoryItems);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 10_000L), (kinah.ObjectId, kinah.ItemId, kinah.Count));
		Assert.Equal(0, player.AbyssRank.Ap);
		var denial = Assert.Single(fixture.SentPackets);
		Assert.Equal(1300927, Assert.IsType<SmSystemMessage>(denial).MessageId);
		var packetEvent = Assert.Single(fixture.PacketEvents);
		Assert.Equal(1300927, AssertPacketEvent<SmSystemMessage>(packetEvent, "active").MessageId);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcSellActionRecordsMissingSellOutcomeWithoutSideEffects()
	{
		await using var fixture = await BuyItemFixture.CreateAsync();
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedSellToShopPlanner, plan.Status);
		Assert.NotNull(plan.SellToShopPlan);
		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellToShop, plan.SellToShopPlan!.Status);
		Assert.NotNull(plan.SellToShopPlan.Dispatch);
		Assert.Null(plan.SellToShopPlan.Dispatch!.SellToShopPlan);
		Assert.False(plan.SellToShopPlan.Dispatch.DispatchesAbyssApSell);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellToShopOutcomeCreated, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.Equal(TradeSellToShopOutcomePlanStatus.MissingSellToShopPlan, outcome.SellToShopOutcomePlan!.Status);
		Assert.Null(outcome.SellForApToShopOutcomePlan);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldMutateKinah);
		Assert.False(outcome.WouldAddRepurchaseItems);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcSellActionClassifiesAbyssPurchaseTemplateForDisabledApOutcome()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateTradeLists(
				new TradeListTemplateSummary(700001, [129], NpcType: "ABYSS", BuyPriceRate: 35)));
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [103]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedSellToShopPlanner, plan.Status);
		Assert.NotNull(plan.SellToShopPlan);
		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellForApToShop, plan.SellToShopPlan!.Status);
		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.SellToShopPlan.Dispatch);
		Assert.True(dispatch.DispatchesAbyssApSell);
		Assert.Equal("ABYSS", dispatch.PurchaseTemplate?.NpcType);
		Assert.Null(dispatch.SellToShopPlan);
		Assert.Null(dispatch.SellForApToShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellForApToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellForApToShopOutcomePlanStatus.MissingSellForApToShopPlan, outcome.SellForApToShopOutcomePlan!.Status);
		Assert.Null(outcome.SellToShopOutcomePlan);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldMutateKinah);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcAbyssSellActionHydratesDisabledApSellPlanFromInventoryFacts()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateTradeLists(
				new TradeListTemplateSummary(700001, [129], NpcType: "ABYSS", BuyPriceRate: 35)),
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, requiredAbyssPoints: 1_000)),
			buyItemGoodsLists: CreateGoodsLists(
				new GoodsListSummary(129, Items: [new GoodsListItemSummary(100000001)])),
			playerEnterWorldRepository: playerRepository);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 2001,
				ItemId = 100000001,
				Count = 2,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [103]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedSellToShopPlanner, plan.Status);
		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellForApToShop, plan.SellToShopPlan!.Status);
		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.SellToShopPlan!.Dispatch);
		var apPlan = Assert.IsType<TradeSellForApToShopPlan>(dispatch.SellForApToShopPlan);
		Assert.Equal(TradeSellForApToShopPlanStatus.PlanCreated, apPlan.Status);
		Assert.Equal([2001], apPlan.DeletedItemObjectIds);
		var reward = Assert.Single(apPlan.AbyssPointRewards);
		Assert.Equal(700, reward.ApReward);
		Assert.Equal(700, apPlan.TotalAbyssPoints);
		Assert.False(apPlan.ShouldDispatchLiveSideEffects);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellForApToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellForApToShopOutcomePlanStatus.DisabledNoTransaction, outcome.SellForApToShopOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.True(outcome.SellForApToShopOutcomePlan.WouldMutateAbyssPoints);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Equal(1, playerRepository.SaveNpcShopApSellMutationCalls);
		var persistence = Assert.IsType<NpcShopApSellPersistenceCapture>(playerRepository.NpcShopApSellPersistence);
		Assert.Equal(player.ObjectId, persistence.PlayerObjectId);
		Assert.Equal(700, persistence.AbyssRank.Ap);
		Assert.Empty(persistence.SellerItemUpdates);
		Assert.Equal([2001], persistence.SellerDeletedItemObjectIds);
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 2001);
		Assert.Equal(700, player.AbyssRank.Ap);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), 2001, SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => Assert.Equal(1320000, Assert.IsType<SmSystemMessage>(packet).MessageId),
			packet => AssertAbyssRankPayload(Assert.IsType<SmAbyssRank>(packet), expectedAp: 700, expectedRank: 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcAbyssSellActionUpdatesPartialStackLive()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateTradeLists(
				new TradeListTemplateSummary(700001, [129], NpcType: "ABYSS", BuyPriceRate: 35)),
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, requiredAbyssPoints: 1_000, maxStackCount: 100)),
			buyItemGoodsLists: CreateGoodsLists(
				new GoodsListSummary(129, Items: [new GoodsListItemSummary(100000001)])),
			playerEnterWorldRepository: playerRepository);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 2001,
				ItemId = 100000001,
				Count = 3,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [103]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedSellToShopPlanner, plan.Status);
		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellForApToShop, plan.SellToShopPlan!.Status);
		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.SellToShopPlan!.Dispatch);
		var apPlan = Assert.IsType<TradeSellForApToShopPlan>(dispatch.SellForApToShopPlan);
		Assert.Equal(TradeSellForApToShopPlanStatus.PlanCreated, apPlan.Status);
		Assert.Empty(apPlan.DeletedItemObjectIds);
		var plannedUpdate = Assert.Single(apPlan.UpdatedItems);
		Assert.Equal((2001, 100000001, 1L), (plannedUpdate.ObjectId, plannedUpdate.ItemId, plannedUpdate.Count));
		var reward = Assert.Single(apPlan.AbyssPointRewards);
		Assert.Equal(700, reward.ApReward);
		Assert.Equal(700, apPlan.TotalAbyssPoints);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellForApToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellForApToShopOutcomePlanStatus.DisabledNoTransaction, outcome.SellForApToShopOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.True(outcome.SellForApToShopOutcomePlan.WouldMutateAbyssPoints);
		Assert.True(outcome.WouldSendPackets);
		Assert.Equal(1, playerRepository.SaveNpcShopApSellMutationCalls);
		var persistence = Assert.IsType<NpcShopApSellPersistenceCapture>(playerRepository.NpcShopApSellPersistence);
		Assert.Equal(player.ObjectId, persistence.PlayerObjectId);
		Assert.Equal(700, persistence.AbyssRank.Ap);
		var persistedUpdate = Assert.Single(persistence.SellerItemUpdates);
		Assert.Equal((2001, 100000001, 1L), (persistedUpdate.ObjectId, persistedUpdate.ItemId, persistedUpdate.Count));
		Assert.Empty(persistence.SellerDeletedItemObjectIds);
		Assert.Equal(1, player.InventoryItems.Single(item => item.ObjectId == 2001).Count);
		Assert.Equal(700, player.AbyssRank.Ap);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseItemUse, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType),
			packet => Assert.Equal(1320000, Assert.IsType<SmSystemMessage>(packet).MessageId),
			packet => AssertAbyssRankPayload(Assert.IsType<SmAbyssRank>(packet), expectedAp: 700, expectedRank: 1));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcAbyssSellActionBlocksDisabledApPlanWhenGoodsListRejectsItem()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateTradeLists(
				new TradeListTemplateSummary(700001, [129], NpcType: "ABYSS", BuyPriceRate: 35)),
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, requiredAbyssPoints: 1_000)),
			buyItemGoodsLists: CreateGoodsLists(
				new GoodsListSummary(129, Items: [new GoodsListItemSummary(100000002)])));
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 2001,
				ItemId = 100000001,
				Count = 1,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [103]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.SellToShopPlan!.Dispatch);
		var apPlan = Assert.IsType<TradeSellForApToShopPlan>(dispatch.SellForApToShopPlan);
		Assert.Equal(TradeSellForApToShopPlanStatus.BlockedInvalidPurchaseItem, apPlan.Status);
		Assert.Equal(2001, apPlan.RejectedItemObjectId);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellForApToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellForApToShopOutcomePlanStatus.SellForApToShopPlanNotReady, outcome.SellForApToShopOutcomePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcSellActionClassifiesNormalPurchaseTemplateForDisabledSellOutcome()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateTradeLists(
				new TradeListTemplateSummary(700001, [129], NpcType: "NORMAL", BuyPriceRate: 35)));
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());
		fixture.World.TryAddObject(
			9001,
			CreateNpc(
				objectId: 9001,
				templateId: 700001,
				position: new WorldPosition(210010000, 11, 0, 0, 0),
				functionDialogIds: [103]));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedSellToShopPlanner, plan.Status);
		Assert.NotNull(plan.SellToShopPlan);
		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellToShop, plan.SellToShopPlan!.Status);
		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.SellToShopPlan.Dispatch);
		Assert.False(dispatch.DispatchesAbyssApSell);
		Assert.Equal("NORMAL", dispatch.PurchaseTemplate?.NpcType);
		Assert.Null(dispatch.SellToShopPlan);
		Assert.Null(dispatch.SellForApToShopPlan);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellToShopOutcomePlanStatus.MissingSellToShopPlan, outcome.SellToShopOutcomePlan!.Status);
		Assert.Null(outcome.SellForApToShopOutcomePlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcSellActionUsesFunctionFactsToSkipUnsupportedNpc()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateTradeLists(
				new TradeListTemplateSummary(700001, [129], NpcType: "ABYSS", BuyPriceRate: 35)));
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());
		fixture.World.TryAddObject(
			9001,
			CreateNpc(objectId: 9001, templateId: 700001, position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedSellToShopPlanner, plan.Status);
		Assert.NotNull(plan.SellToShopPlan);
		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.SkippedNpcCannotBuyOrPurchase, plan.SellToShopPlan!.Status);
		Assert.Null(plan.SellToShopPlan.Dispatch);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellToShopOutcomePlanStatus.MissingSellToShopPlan, outcome.SellToShopOutcomePlan!.Status);
		Assert.Null(outcome.SellForApToShopOutcomePlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcSellActionHydratesDisabledNormalSellPlanFromInventoryFacts()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, mask: 1 << 2)));
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 2001,
				ItemId = 100000001,
				Count = 1,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 1_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(objectId: 9001, templateId: 700001, position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.SellToShopPlan!.Dispatch);
		var sellPlan = Assert.IsType<TradeSellToShopPlan>(dispatch.SellToShopPlan);
		Assert.False(dispatch.DispatchesAbyssApSell);
		Assert.Equal(TradeSellToShopPlanStatus.PlanCreated, sellPlan.Status);
		Assert.Equal([2001], sellPlan.SellerDeletedItemObjectIds);
		Assert.Single(sellPlan.RepurchaseItems);
		Assert.NotNull(sellPlan.KinahUpdate);
		Assert.Equal(1_200, sellPlan.KinahUpdate!.Count);
		Assert.False(sellPlan.IsLive);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellToShopOutcomePlanStatus.DisabledNoTransaction, outcome.SellToShopOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.True(outcome.WouldMutateKinah);
		Assert.True(outcome.WouldAddRepurchaseItems);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcSellActionExecutesNormalSellToShopLive()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, mask: 1 << 2),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			playerEnterWorldRepository: playerRepository,
			observeBuyItemPlans: false);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 2001,
				ItemId = 100000001,
				Count = 1,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 1_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(objectId: 9001, templateId: 700001, position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 1)]));

		Assert.Empty(fixture.BuyItemPlans);
		Assert.Empty(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(1, playerRepository.SaveNpcShopSellMutationCalls);
		var persistence = Assert.IsType<NpcShopSellPersistenceCapture>(playerRepository.NpcShopSellPersistence);
		Assert.Equal(player.ObjectId, persistence.PlayerObjectId);
		Assert.Empty(persistence.SellerItemUpdates);
		Assert.Equal([2001], persistence.SellerDeletedItemObjectIds);
		Assert.False(persistence.KinahWasCreated);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 1_200L), (persistence.KinahItem.ObjectId, persistence.KinahItem.ItemId, persistence.KinahItem.Count));
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 2001);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.ItemId == InventoryItemFactory.KinahItemId && item.Count == 1_200);
		var repurchase = Assert.Single(player.RepurchaseItems);
		Assert.Equal((2001, 100000001, 1L), (repurchase.Item.ObjectId, repurchase.Item.ItemId, repurchase.Item.Count));
		Assert.Equal(200, repurchase.RepurchasePrice);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), 2001, SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => Assert.Equal(SmInventoryUpdateItem.IncreaseKinahSell, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemPetMerchantSellActionExecutesSellToShopLive()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, mask: 1 << 2),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			playerEnterWorldRepository: playerRepository);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 2001,
				ItemId = 100000001,
				Count = 1,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 1_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(7001, new WorldPet(7001, HasMerchantFunction: true, MerchantSellModifier: 33));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 7001, tradeActionId: 17, [(2001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedPetSellToShopPlanner, plan.Status);
		Assert.Equal(33, plan.PetSellModifier);
		Assert.Equal(TradeSellToShopPlanStatus.PlanCreated, plan.PetSellToShopPlan!.Status);
		Assert.Equal(1, playerRepository.SaveNpcShopSellMutationCalls);
		var persistence = Assert.IsType<NpcShopSellPersistenceCapture>(playerRepository.NpcShopSellPersistence);
		Assert.Equal(player.ObjectId, persistence.PlayerObjectId);
		Assert.Empty(persistence.SellerItemUpdates);
		Assert.Equal([2001], persistence.SellerDeletedItemObjectIds);
		Assert.False(persistence.KinahWasCreated);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 1_330L), (persistence.KinahItem.ObjectId, persistence.KinahItem.ItemId, persistence.KinahItem.Count));
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 2001);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.ItemId == InventoryItemFactory.KinahItemId && item.Count == 1_330);
		var repurchase = Assert.Single(player.RepurchaseItems);
		Assert.Equal((2001, 100000001, 1L), (repurchase.Item.ObjectId, repurchase.Item.ItemId, repurchase.Item.Count));
		Assert.Equal(330, repurchase.RepurchasePrice);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), 2001, SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => Assert.Equal(SmInventoryUpdateItem.IncreaseKinahSell, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemActivePetMerchantSellUsesStaticPetTemplateRateLive()
	{
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, mask: 1 << 2),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemPetTemplates: CreatePetTemplates(
				new PetTemplateSummary(
					900210,
					"merchant pet",
					NameId: 1600210,
					ConditionReward: 0,
					Functions: [new PetFunctionSummary(3, PetFunctionType.Merchant, Slots: 0, RatePrice: 15)])),
			playerEnterWorldRepository: playerRepository);
		var player = CreatePlayer();
		player.HasPetSummon = true;
		player.PetSummonObjectId = 7001;
		player.PetSummonNpcId = 900210;
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 2001,
				ItemId = 100000001,
				Count = 1,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
			new InventoryItem
			{
				ObjectId = 3001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 1_000,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 7001, tradeActionId: 17, [(2001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedPetSellToShopPlanner, plan.Status);
		Assert.Equal(15, plan.PetSellModifier);
		Assert.Equal(TradeSellToShopPlanStatus.PlanCreated, plan.PetSellToShopPlan!.Status);
		Assert.Equal(1, playerRepository.SaveNpcShopSellMutationCalls);
		var persistence = Assert.IsType<NpcShopSellPersistenceCapture>(playerRepository.NpcShopSellPersistence);
		Assert.Equal(player.ObjectId, persistence.PlayerObjectId);
		Assert.Equal([2001], persistence.SellerDeletedItemObjectIds);
		Assert.Equal((3001, InventoryItemFactory.KinahItemId, 1_150L), (persistence.KinahItem.ObjectId, persistence.KinahItem.ItemId, persistence.KinahItem.Count));
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == 2001);
		Assert.Contains(player.InventoryItems, item => item.ObjectId == 3001 && item.ItemId == InventoryItemFactory.KinahItemId && item.Count == 1_150);
		var repurchase = Assert.Single(player.RepurchaseItems);
		Assert.Equal((2001, 100000001, 1L), (repurchase.Item.ObjectId, repurchase.Item.ItemId, repurchase.Item.Count));
		Assert.Equal(150, repurchase.RepurchasePrice);
		Assert.Collection(
			fixture.SentPackets,
			packet => AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(packet), 2001, SmDeleteItem.UseDeleteType),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 0),
			packet => Assert.Equal(SmInventoryUpdateItem.IncreaseKinahSell, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcSellActionBlocksDisabledNormalSellPlanWhenTemplateMaskIsNotSellable()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, mask: 0)));
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 2001,
				ItemId = 100000001,
				Count = 1,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(objectId: 9001, templateId: 700001, position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.SellToShopPlan!.Dispatch);
		var sellPlan = Assert.IsType<TradeSellToShopPlan>(dispatch.SellToShopPlan);
		Assert.Equal(TradeSellToShopPlanStatus.BlockedNotSellable, sellPlan.Status);
		Assert.Empty(sellPlan.SellerDeletedItemObjectIds);
		Assert.Null(sellPlan.KinahUpdate);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellToShopOutcomePlanStatus.DisabledNoTransaction, outcome.SellToShopOutcomePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldMutateKinah);
		Assert.False(outcome.WouldAddRepurchaseItems);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Equal(1300336, Assert.IsType<SmSystemMessage>(Assert.Single(fixture.SentPackets)).MessageId);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcSellActionHydratesPartialStackAndMissingKinahWithDiagnosticObjectIds()
	{
		var diagnosticIds = new Queue<int>([4001, 4002]);
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, mask: 1 << 2, maxStackCount: 10)),
			buyItemDiagnosticObjectIdProvider: diagnosticIds.Dequeue);
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 2001,
				ItemId = 100000001,
				Count = 5,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(objectId: 9001, templateId: 700001, position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.SellToShopPlan!.Dispatch);
		var sellPlan = Assert.IsType<TradeSellToShopPlan>(dispatch.SellToShopPlan);
		Assert.Equal(TradeSellToShopPlanStatus.PlanCreated, sellPlan.Status);
		Assert.Empty(sellPlan.SellerDeletedItemObjectIds);
		var sellerUpdate = Assert.Single(sellPlan.SellerItemUpdates);
		Assert.Equal(2001, sellerUpdate.ObjectId);
		Assert.Equal(3, sellerUpdate.Count);
		var repurchase = Assert.Single(sellPlan.RepurchaseItems);
		Assert.Equal(4001, repurchase.Item.ObjectId);
		Assert.Equal(2, repurchase.Item.Count);
		Assert.Equal(400, repurchase.RepurchasePrice);
		Assert.NotNull(sellPlan.KinahUpdate);
		Assert.Equal(4002, sellPlan.KinahUpdate!.ObjectId);
		Assert.Equal(InventoryItemFactory.KinahItemId, sellPlan.KinahUpdate.ItemId);
		Assert.Equal(400, sellPlan.KinahUpdate.Count);
		Assert.Empty(diagnosticIds);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellToShopOutcomePlanStatus.DisabledNoTransaction, outcome.SellToShopOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.True(outcome.WouldMutateKinah);
		Assert.True(outcome.WouldAddRepurchaseItems);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemNpcSellActionKeepsPartialStackBlockedWithoutDiagnosticObjectIds()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, mask: 1 << 2)));
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 2001,
				ItemId = 100000001,
				Count = 5,
				OwnerId = player.ObjectId,
				Location = 0,
				Slot = 65535,
			},
		];
		SetActivePlayerForPacketDispatch(fixture.Connection, player);
		fixture.World.TryAddObject(
			9001,
			CreateNpc(objectId: 9001, templateId: 700001, position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 1, [(2001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		var dispatch = Assert.IsType<CmBuyItemSellToShopDispatchDescriptor>(plan.SellToShopPlan!.Dispatch);
		var sellPlan = Assert.IsType<TradeSellToShopPlan>(dispatch.SellToShopPlan);
		Assert.Equal(TradeSellToShopPlanStatus.BlockedRepurchaseItemCreateFailed, sellPlan.Status);
		Assert.Empty(sellPlan.SellerDeletedItemObjectIds);
		Assert.Empty(sellPlan.RepurchaseItems);
		Assert.Null(sellPlan.KinahUpdate);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellToShopOutcomePlanStatus.SellToShopPlanNotReady, outcome.SellToShopOutcomePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemKnownListResolverCanRejectWorldObjectTarget()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(buyItemKnownObjectResolver: (_, _, _) => false);
		SetActivePlayerForPacketDispatch(fixture.Connection, CreatePlayer());
		fixture.World.TryAddObject(
			9001,
			CreateNpc(objectId: 9001, templateId: 700001, position: new WorldPosition(210010000, 11, 0, 0, 0)));

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: 9001, tradeActionId: 13, [(1001, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedUnknownTarget, plan.Status);
		Assert.Null(plan.BuyFromShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemPlayerMembershipResolverRejectsUnknownPlayerTarget()
	{
		var membership = new PlayerKnownListMembershipService();
		var activePlayer = CreatePlayer();
		var sellerPlayer = new Player { ObjectId = 9101, Name = "StoreSeller", Position = new WorldPosition(210010000, 10, 0, 0, 0) };
		await using var fixture = await BuyItemFixture.CreateAsync(
			CmBuyItemKnownListMembershipResolverAdapterService.CreateResolver(membership));
		SetActivePlayerForPacketDispatch(fixture.Connection, activePlayer);
		fixture.World.TryAddObject(sellerPlayer.ObjectId, sellerPlayer);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: sellerPlayer.ObjectId, tradeActionId: 0, [(1001, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedUnknownTarget, plan.Status);
		Assert.Null(plan.PrivateStorePurchasePlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemPlayerPrivateStoreSelectionRecordsDisabledOutcomeDiagnostic()
	{
		var membership = new PlayerKnownListMembershipService();
		var activePlayer = CreatePlayer();
		var sellerPlayer = new Player { ObjectId = 9101, Name = "StoreSeller", Position = new WorldPosition(210010000, 10, 0, 0, 0) };
		membership.UpsertKnownPlayers(
			activePlayer.ObjectId,
			[new PlayerKnownListMembershipCandidate(sellerPlayer.ObjectId, IsVisibleToOwner: true)]);
		await using var fixture = await BuyItemFixture.CreateAsync(
			CmBuyItemKnownListMembershipResolverAdapterService.CreateResolver(membership));
		SetActivePlayerForPacketDispatch(fixture.Connection, activePlayer);
		fixture.World.TryAddObject(sellerPlayer.ObjectId, sellerPlayer);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: sellerPlayer.ObjectId, tradeActionId: 0, [(0, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedPrivateStorePlanner, plan.Status);
		Assert.NotNull(plan.PrivateStoreBoughtItemsPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.PrivateStoreOutcomeCreated, outcome.Status);
		Assert.Same(plan, outcome.HandlerPlan);
		Assert.NotNull(outcome.PrivateStoreFacadePlan);
		Assert.NotNull(outcome.PrivateStoreOutcomePlan);
		Assert.Equal(PrivateStoreLiveExecutorFacadeStatus.BoughtItemsPlanNotReady, outcome.PrivateStoreFacadePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemPlayerPrivateStoreExecutesSingleItemPurchaseAndClosesStore()
	{
		var membership = new PlayerKnownListMembershipService();
		var activePlayer = CreatePlayer();
		activePlayer.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 8001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 20_000,
				OwnerId = activePlayer.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		var sellerPlayer = new Player
		{
			ObjectId = 9101,
			Name = "StoreSeller",
			Race = activePlayer.Race,
			IsOnline = true,
			CreatureState = PlayerCreatureState.PrivateShop,
			PrivateStoreMessage = "Practice wares",
			Position = new WorldPosition(210010000, 10, 0, 0, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 3001,
					ItemId = 100000001,
					Count = 1,
					OwnerId = 9101,
					Location = 0,
					Slot = 1,
				},
			],
			PrivateStoreItems =
			[
				new PrivateStoreListedItemSummary(
					StoreIndex: 0,
					ItemObjectId: 3001,
					ItemId: 100000001,
					Count: 1,
					PricePerItem: 10_000,
					ItemName: "Practice Sword"),
			],
		};
		membership.UpsertKnownPlayers(
			activePlayer.ObjectId,
			[new PlayerKnownListMembershipCandidate(sellerPlayer.ObjectId, IsVisibleToOwner: true)]);
		var playerRepository = new EmptyPlayerEnterWorldRepository();
		await using var fixture = await BuyItemFixture.CreateAsync(
			CmBuyItemKnownListMembershipResolverAdapterService.CreateResolver(membership),
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(9001),
			playerEnterWorldRepository: playerRepository);
		SetActivePlayerForPacketDispatch(fixture.Connection, activePlayer);
		fixture.World.TryAddObject(sellerPlayer.ObjectId, sellerPlayer);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: sellerPlayer.ObjectId, tradeActionId: 0, [(0, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedPrivateStorePlanner, plan.Status);
		Assert.NotNull(plan.PrivateStoreBoughtItemsPlan);
		var boughtItemsPlan = plan.PrivateStoreBoughtItemsPlan;
		Assert.Equal(PrivateStoreBoughtItemsPlanStatus.PlanCreated, boughtItemsPlan.Status);
		var boughtItem = Assert.Single(boughtItemsPlan.BoughtItems);
		Assert.Equal((0, 3001, 100000001, 1L, 10_000L), (boughtItem.StoreIndex, boughtItem.ItemObjectId, boughtItem.ItemId, boughtItem.Count, boughtItem.PricePerItem));
		Assert.NotNull(plan.PrivateStorePurchasePlan);
		var purchasePlan = plan.PrivateStorePurchasePlan;
		Assert.Equal(PrivateStorePurchasePlanStatus.PlanCreated, purchasePlan.Status);
		Assert.Equal([3001], purchasePlan.SellerDeletedItemObjectIds);
		Assert.Single(purchasePlan.BuyerAddedItems);
		Assert.Equal(10_000, purchasePlan.BuyerKinahUpdate!.Count);
		Assert.Equal(10_000, purchasePlan.SellerKinahUpdate!.Count);
		Assert.True(purchasePlan.SellerKinahWasCreated);
		Assert.True(purchasePlan.ShouldCloseSellerStore);
		Assert.Equal(1, playerRepository.SavePrivateStorePurchaseMutationCalls);
		Assert.NotNull(playerRepository.PrivateStorePurchasePersistence);
		var persistence = playerRepository.PrivateStorePurchasePersistence!;
		Assert.Equal(activePlayer.ObjectId, persistence.BuyerObjectId);
		Assert.Equal(sellerPlayer.ObjectId, persistence.SellerObjectId);
		Assert.Empty(persistence.SellerUpdatedItems);
		Assert.Equal([3001], persistence.SellerDeletedItemObjectIds);
		Assert.Empty(persistence.BuyerUpdatedItems);
		var persistedBuyerItem = Assert.Single(persistence.BuyerAddedItems);
		Assert.Equal((9001, 100000001, 1L, activePlayer.ObjectId, 0), (
			persistedBuyerItem.ObjectId,
			persistedBuyerItem.ItemId,
			persistedBuyerItem.Count,
			persistedBuyerItem.OwnerId,
			persistedBuyerItem.Location));
		Assert.Equal(10_000, persistence.BuyerKinahItem!.Count);
		Assert.Equal(10_000, persistence.SellerKinahItem!.Count);
		Assert.True(persistence.SellerKinahWasCreated);

		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.PrivateStoreOutcomeCreated, outcome.Status);
		Assert.Equal(PrivateStoreLiveExecutorFacadeStatus.DisabledNoSideEffects, outcome.PrivateStoreFacadePlan!.Status);
		Assert.Equal(PrivateStorePurchaseOutcomePlanStatus.DisabledNoTransaction, outcome.PrivateStoreOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.True(outcome.WouldMutateBuyerInventory);
		Assert.True(outcome.WouldMutateKinah);
		Assert.True(outcome.WouldSendPackets);
		Assert.True(outcome.WouldWriteExchangeLog);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(sellerPlayer.PrivateStoreItems);
		Assert.Equal(string.Empty, sellerPlayer.PrivateStoreMessage);
		Assert.False(sellerPlayer.IsInState(PlayerCreatureState.PrivateShop));
		Assert.True(sellerPlayer.IsInState(PlayerCreatureState.Active));
		Assert.DoesNotContain(sellerPlayer.InventoryItems, item => item.ObjectId == 3001);
		Assert.Contains(sellerPlayer.InventoryItems, item => item.ItemId == InventoryItemFactory.KinahItemId && item.Count == 10_000);
		Assert.Contains(activePlayer.InventoryItems, item => item.ObjectId == 9001 && item.ItemId == 100000001 && item.Count == 1);
		Assert.Contains(activePlayer.InventoryItems, item => item.ItemId == InventoryItemFactory.KinahItemId && item.Count == 10_000);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryAddItem>(packet),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType));
		Assert.Collection(
			fixture.Registry.DirectPackets,
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(sent.Packet), 3001, SmDeleteItem.UseDeleteType);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(sent.Packet), expectedItemsCount: 0);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				Assert.Equal(1400134, Assert.IsType<SmSystemMessage>(sent.Packet).MessageId);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertInventoryAddItemPayload(
					Assert.IsType<SmInventoryAddItem>(sent.Packet),
					expectedObjectId: 9002,
					expectedItemId: InventoryItemFactory.KinahItemId,
					expectedCount: 0,
					expectedAddType: SmInventoryAddItem.ItemCollect);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(sent.Packet), expectedItemsCount: 0);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				Assert.Equal(SmInventoryUpdateItem.IncreaseKinahCollect, Assert.IsType<SmInventoryUpdateItem>(sent.Packet).UpdateType);
			});
		var closeBroadcast = Assert.Single(fixture.Registry.VisibleBroadcasts);
		Assert.Equal(sellerPlayer.ObjectId, closeBroadcast.SourceObjectId);
		AssertClosePrivateShopEmotion(Assert.IsType<SmEmotion>(closeBroadcast.Packet), sellerPlayer.ObjectId);
		Assert.Collection(
			fixture.PacketEvents,
			packet => AssertPacketEvent<SmDeleteItem>(packet, "direct", sellerPlayer.ObjectId),
			packet => AssertPacketEvent<SmCubeUpdate>(packet, "direct", sellerPlayer.ObjectId),
			packet => AssertPacketEvent<SmInventoryAddItem>(packet, "active"),
			packet => AssertPacketEvent<SmCubeUpdate>(packet, "active"),
			packet => Assert.Equal(1400134, AssertPacketEvent<SmSystemMessage>(packet, "direct", sellerPlayer.ObjectId).MessageId),
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, AssertPacketEvent<SmInventoryUpdateItem>(packet, "active").UpdateType),
			packet => AssertPacketEvent<SmInventoryAddItem>(packet, "direct", sellerPlayer.ObjectId),
			packet => AssertPacketEvent<SmCubeUpdate>(packet, "direct", sellerPlayer.ObjectId),
			packet => Assert.Equal(SmInventoryUpdateItem.IncreaseKinahCollect, AssertPacketEvent<SmInventoryUpdateItem>(packet, "direct", sellerPlayer.ObjectId).UpdateType),
			packet => AssertPacketEvent<SmEmotion>(packet, "visible", sellerPlayer.ObjectId));
		Assert.Contains(
			fixture.Logger.Entries,
			entry =>
				entry.Level == LogLevel.Information
				&& entry.Message.Contains(
					"[PRIVATE STORE] > [Seller: StoreSeller] sold [Item: 100000001][Amount: 1] to [Buyer: BuyItemTester] for [Price: 10000]",
					StringComparison.Ordinal));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemPlayerPrivateStoreStaleSellerCountLogsAuditAndStops()
	{
		var membership = new PlayerKnownListMembershipService();
		var activePlayer = CreatePlayer();
		activePlayer.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 8001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 20_000,
				OwnerId = activePlayer.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		var sellerPlayer = new Player
		{
			ObjectId = 9101,
			Name = "StoreSeller",
			Race = activePlayer.Race,
			IsOnline = true,
			CreatureState = PlayerCreatureState.PrivateShop,
			PrivateStoreMessage = "Practice wares",
			Position = new WorldPosition(210010000, 10, 0, 0, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 3001,
					ItemId = 100000001,
					Count = 1,
					OwnerId = 9101,
					Location = 0,
					Slot = 1,
				},
			],
			PrivateStoreItems =
			[
				new PrivateStoreListedItemSummary(
					StoreIndex: 0,
					ItemObjectId: 3001,
					ItemId: 100000001,
					Count: 2,
					PricePerItem: 10_000,
					ItemName: "Practice Sword"),
			],
		};
		membership.UpsertKnownPlayers(
			activePlayer.ObjectId,
			[new PlayerKnownListMembershipCandidate(sellerPlayer.ObjectId, IsVisibleToOwner: true)]);
		await using var fixture = await BuyItemFixture.CreateAsync(
			CmBuyItemKnownListMembershipResolverAdapterService.CreateResolver(membership),
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)));
		SetActivePlayerForPacketDispatch(fixture.Connection, activePlayer);
		fixture.World.TryAddObject(sellerPlayer.ObjectId, sellerPlayer);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: sellerPlayer.ObjectId, tradeActionId: 0, [(0, 2)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedPrivateStorePlanner, plan.Status);
		Assert.NotNull(plan.PrivateStorePurchasePlan);
		var purchasePlan = plan.PrivateStorePurchasePlan;
		Assert.Equal(PrivateStorePurchasePlanStatus.BlockedSellerItemCountChanged, purchasePlan.Status);
		Assert.Equal("tried to buy more than players private store item stack count", purchasePlan.AuditMessage);
		var outcome = Assert.Single(fixture.BuyItemSideEffectOutcomePlans);
		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.PrivateStoreOutcomeCreated, outcome.Status);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.Empty(fixture.SentPackets);
		Assert.Empty(fixture.Registry.DirectPackets);
		Assert.Empty(fixture.Registry.VisibleBroadcasts);
		Assert.Contains(activePlayer.InventoryItems, item => item.ObjectId == 8001 && item.Count == 20_000);
		var sellerInventoryItem = Assert.Single(sellerPlayer.InventoryItems, item => item.ObjectId == 3001);
		Assert.Equal(1, sellerInventoryItem.Count);
		var storeItem = Assert.Single(sellerPlayer.PrivateStoreItems);
		Assert.Equal((0, 3001, 2L), (storeItem.StoreIndex, storeItem.ItemObjectId, storeItem.Count));
		Assert.True(sellerPlayer.IsInState(PlayerCreatureState.PrivateShop));
		Assert.Equal("Practice wares", sellerPlayer.PrivateStoreMessage);
		Assert.Contains(
			fixture.Logger.Entries,
			entry =>
				entry.Level == LogLevel.Warning
				&& entry.Message.Contains(
					"Player BuyItemTester (1001) tried to buy more than players private store item stack count",
					StringComparison.Ordinal));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemPlayerPrivateStoreMultiAddUsesPerItemCubeSnapshots()
	{
		var membership = new PlayerKnownListMembershipService();
		var activePlayer = CreatePlayer();
		activePlayer.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 8001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 20_000,
				OwnerId = activePlayer.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		var sellerPlayer = new Player
		{
			ObjectId = 9101,
			Name = "StoreSeller",
			Race = activePlayer.Race,
			IsOnline = true,
			CreatureState = PlayerCreatureState.PrivateShop,
			PrivateStoreMessage = "Practice wares",
			Position = new WorldPosition(210010000, 10, 0, 0, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 3001,
					ItemId = 100000001,
					Count = 1,
					OwnerId = 9101,
					Location = 0,
					Slot = 1,
				},
				new InventoryItem
				{
					ObjectId = 3002,
					ItemId = 100000002,
					Count = 1,
					OwnerId = 9101,
					Location = 0,
					Slot = 2,
				},
			],
			PrivateStoreItems =
			[
				new PrivateStoreListedItemSummary(
					StoreIndex: 0,
					ItemObjectId: 3001,
					ItemId: 100000001,
					Count: 1,
					PricePerItem: 4_000,
					ItemName: "Practice Sword"),
				new PrivateStoreListedItemSummary(
					StoreIndex: 1,
					ItemObjectId: 3002,
					ItemId: 100000002,
					Count: 1,
					PricePerItem: 5_000,
					ItemName: "Practice Dagger"),
			],
		};
		membership.UpsertKnownPlayers(
			activePlayer.ObjectId,
			[new PlayerKnownListMembershipCandidate(sellerPlayer.ObjectId, IsVisibleToOwner: true)]);
		await using var fixture = await BuyItemFixture.CreateAsync(
			CmBuyItemKnownListMembershipResolverAdapterService.CreateResolver(membership),
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000),
				Template(100000002, price: 1_000),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(9001));
		SetActivePlayerForPacketDispatch(fixture.Connection, activePlayer);
		fixture.World.TryAddObject(sellerPlayer.ObjectId, sellerPlayer);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: sellerPlayer.ObjectId, tradeActionId: 0, [(0, 1), (1, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.NotNull(plan.PrivateStorePurchasePlan);
		var purchasePlan = plan.PrivateStorePurchasePlan;
		Assert.Equal(PrivateStorePurchasePlanStatus.PlanCreated, purchasePlan.Status);
		Assert.Equal([3001, 3002], purchasePlan.SellerDeletedItemObjectIds);
		Assert.Equal(2, purchasePlan.BuyerAddedItems.Count);
		Assert.Equal(11_000, purchasePlan.BuyerKinahUpdate!.Count);
		Assert.Equal(9_000, purchasePlan.SellerKinahUpdate!.Count);
		Assert.True(purchasePlan.SellerKinahWasCreated);
		Assert.True(purchasePlan.ShouldCloseSellerStore);

		Assert.DoesNotContain(sellerPlayer.InventoryItems, item => item.ObjectId is 3001 or 3002);
		Assert.Contains(activePlayer.InventoryItems, item => item.ObjectId == 9001 && item.ItemId == 100000001);
		Assert.Contains(activePlayer.InventoryItems, item => item.ObjectId == 9002 && item.ItemId == 100000002);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryAddItem>(packet),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => Assert.IsType<SmInventoryAddItem>(packet),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 2),
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType));
		Assert.Collection(
			fixture.Registry.DirectPackets,
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(sent.Packet), 3001, SmDeleteItem.UseDeleteType);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(sent.Packet), expectedItemsCount: 1);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				Assert.Equal(1400134, Assert.IsType<SmSystemMessage>(sent.Packet).MessageId);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertDeleteItemPayload(Assert.IsType<SmDeleteItem>(sent.Packet), 3002, SmDeleteItem.UseDeleteType);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(sent.Packet), expectedItemsCount: 0);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				Assert.Equal(1400134, Assert.IsType<SmSystemMessage>(sent.Packet).MessageId);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertInventoryAddItemPayload(
					Assert.IsType<SmInventoryAddItem>(sent.Packet),
					expectedObjectId: 9003,
					expectedItemId: InventoryItemFactory.KinahItemId,
					expectedCount: 0,
					expectedAddType: SmInventoryAddItem.ItemCollect);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(sent.Packet), expectedItemsCount: 0);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				Assert.Equal(SmInventoryUpdateItem.IncreaseKinahCollect, Assert.IsType<SmInventoryUpdateItem>(sent.Packet).UpdateType);
			});
		var closeBroadcast = Assert.Single(fixture.Registry.VisibleBroadcasts);
		Assert.Equal(sellerPlayer.ObjectId, closeBroadcast.SourceObjectId);
		Assert.Collection(
			fixture.PacketEvents,
			packet => AssertPacketEvent<SmDeleteItem>(packet, "direct", sellerPlayer.ObjectId),
			packet => AssertPacketEvent<SmCubeUpdate>(packet, "direct", sellerPlayer.ObjectId),
			packet => AssertPacketEvent<SmInventoryAddItem>(packet, "active"),
			packet => AssertPacketEvent<SmCubeUpdate>(packet, "active"),
			packet => Assert.Equal(1400134, AssertPacketEvent<SmSystemMessage>(packet, "direct", sellerPlayer.ObjectId).MessageId),
			packet => AssertPacketEvent<SmDeleteItem>(packet, "direct", sellerPlayer.ObjectId),
			packet => AssertPacketEvent<SmCubeUpdate>(packet, "direct", sellerPlayer.ObjectId),
			packet => AssertPacketEvent<SmInventoryAddItem>(packet, "active"),
			packet => AssertPacketEvent<SmCubeUpdate>(packet, "active"),
			packet => Assert.Equal(1400134, AssertPacketEvent<SmSystemMessage>(packet, "direct", sellerPlayer.ObjectId).MessageId),
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, AssertPacketEvent<SmInventoryUpdateItem>(packet, "active").UpdateType),
			packet => AssertPacketEvent<SmInventoryAddItem>(packet, "direct", sellerPlayer.ObjectId),
			packet => AssertPacketEvent<SmCubeUpdate>(packet, "direct", sellerPlayer.ObjectId),
			packet => Assert.Equal(SmInventoryUpdateItem.IncreaseKinahCollect, AssertPacketEvent<SmInventoryUpdateItem>(packet, "direct", sellerPlayer.ObjectId).UpdateType),
			packet => AssertPacketEvent<SmEmotion>(packet, "visible", sellerPlayer.ObjectId));
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemPlayerPrivateStorePartialStackKeepsStoreOpenAndDecrementsPackCount()
	{
		var membership = new PlayerKnownListMembershipService();
		var activePlayer = CreatePlayer();
		activePlayer.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 8001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 20_000,
				OwnerId = activePlayer.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		var sellerPlayer = new Player
		{
			ObjectId = 9101,
			Name = "StoreSeller",
			Race = activePlayer.Race,
			IsOnline = true,
			CreatureState = PlayerCreatureState.PrivateShop,
			PrivateStoreMessage = "Practice wares",
			Position = new WorldPosition(210010000, 10, 0, 0, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 3001,
					ItemId = 182003001,
					Count = 5,
					OwnerId = 9101,
					Location = 0,
					Slot = 1,
					PackCount = 4,
				},
			],
			PrivateStoreItems =
			[
				new PrivateStoreListedItemSummary(
					StoreIndex: 0,
					ItemObjectId: 3001,
					ItemId: 182003001,
					Count: 5,
					PricePerItem: 100,
					ItemName: "Practice Bundle"),
			],
		};
		membership.UpsertKnownPlayers(
			activePlayer.ObjectId,
			[new PlayerKnownListMembershipCandidate(sellerPlayer.ObjectId, IsVisibleToOwner: true)]);
		await using var fixture = await BuyItemFixture.CreateAsync(
			CmBuyItemKnownListMembershipResolverAdapterService.CreateResolver(membership),
			buyItemItemTemplates: CreateItemTemplates(
				Template(182003001, price: 1, maxStackCount: 10),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(9001));
		SetActivePlayerForPacketDispatch(fixture.Connection, activePlayer);
		fixture.World.TryAddObject(sellerPlayer.ObjectId, sellerPlayer);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: sellerPlayer.ObjectId, tradeActionId: 0, [(0, 3)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.NotNull(plan.PrivateStorePurchasePlan);
		var purchasePlan = plan.PrivateStorePurchasePlan;
		Assert.Equal(PrivateStorePurchasePlanStatus.PlanCreated, purchasePlan.Status);
		Assert.Empty(purchasePlan.SellerDeletedItemObjectIds);
		var sellerPlanUpdate = Assert.Single(purchasePlan.SellerItemUpdates);
		Assert.Equal((3001, 2L, 3), (sellerPlanUpdate.ObjectId, sellerPlanUpdate.Count, sellerPlanUpdate.PackCount));
		Assert.True(purchasePlan.SellerKinahWasCreated);
		Assert.False(purchasePlan.ShouldCloseSellerStore);

		var storeItem = Assert.Single(sellerPlayer.PrivateStoreItems);
		Assert.Equal((0, 3001, 2L), (storeItem.StoreIndex, storeItem.ItemObjectId, storeItem.Count));
		Assert.Equal("Practice wares", sellerPlayer.PrivateStoreMessage);
		Assert.True(sellerPlayer.IsInState(PlayerCreatureState.PrivateShop));
		var sellerItem = Assert.Single(sellerPlayer.InventoryItems, item => item.ObjectId == 3001);
		Assert.Equal((2L, 3), (sellerItem.Count, sellerItem.PackCount));
		Assert.Contains(sellerPlayer.InventoryItems, item => item.ItemId == InventoryItemFactory.KinahItemId && item.Count == 300);
		var buyerItem = Assert.Single(activePlayer.InventoryItems, item => item.ObjectId == 9001);
		Assert.Equal((182003001, 3L, 0), (buyerItem.ItemId, buyerItem.Count, buyerItem.PackCount));
		Assert.Contains(activePlayer.InventoryItems, item => item.ItemId == InventoryItemFactory.KinahItemId && item.Count == 19_700);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.IsType<SmInventoryAddItem>(packet),
			packet => AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(packet), expectedItemsCount: 1),
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType));
		Assert.Collection(
			fixture.Registry.DirectPackets,
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				Assert.Equal(SmInventoryUpdateItem.DecreaseItemUse, Assert.IsType<SmInventoryUpdateItem>(sent.Packet).UpdateType);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				Assert.Equal(1400135, Assert.IsType<SmSystemMessage>(sent.Packet).MessageId);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertInventoryAddItemPayload(
					Assert.IsType<SmInventoryAddItem>(sent.Packet),
					expectedObjectId: 9002,
					expectedItemId: InventoryItemFactory.KinahItemId,
					expectedCount: 0,
					expectedAddType: SmInventoryAddItem.ItemCollect);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(sent.Packet), expectedItemsCount: 1);
			},
			sent =>
			{
				Assert.Equal(sellerPlayer.ObjectId, sent.PlayerObjectId);
				Assert.Equal(SmInventoryUpdateItem.IncreaseKinahCollect, Assert.IsType<SmInventoryUpdateItem>(sent.Packet).UpdateType);
			});
		Assert.Empty(fixture.Registry.VisibleBroadcasts);
	}

	[Fact]
	public async Task ProcessPacketAsync_CmBuyItemPlayerPrivateStoreMissingSellerInventoryKeepsStoreItemAndTransfersKinah()
	{
		var membership = new PlayerKnownListMembershipService();
		var activePlayer = CreatePlayer();
		activePlayer.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 8001,
				ItemId = InventoryItemFactory.KinahItemId,
				Count = 10_000,
				OwnerId = activePlayer.ObjectId,
				Location = 0,
				Slot = 0,
			},
		];
		var sellerPlayer = new Player
		{
			ObjectId = 9101,
			Name = "StoreSeller",
			Race = activePlayer.Race,
			IsOnline = true,
			CreatureState = PlayerCreatureState.PrivateShop,
			PrivateStoreMessage = "Practice wares",
			Position = new WorldPosition(210010000, 10, 0, 0, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 7001,
					ItemId = InventoryItemFactory.KinahItemId,
					Count = 500,
					OwnerId = 9101,
					Location = 0,
					Slot = 0,
				},
			],
			PrivateStoreItems =
			[
				new PrivateStoreListedItemSummary(
					StoreIndex: 0,
					ItemObjectId: 3001,
					ItemId: 100000001,
					Count: 1,
					PricePerItem: 4_000,
					ItemName: "Practice Sword"),
			],
		};
		membership.UpsertKnownPlayers(
			activePlayer.ObjectId,
			[new PlayerKnownListMembershipCandidate(sellerPlayer.ObjectId, IsVisibleToOwner: true)]);
		await using var fixture = await BuyItemFixture.CreateAsync(
			CmBuyItemKnownListMembershipResolverAdapterService.CreateResolver(membership),
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1),
				Template(InventoryItemFactory.KinahItemId, price: 1, maxStackCount: 10_000_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(9001));
		SetActivePlayerForPacketDispatch(fixture.Connection, activePlayer);
		fixture.World.TryAddObject(sellerPlayer.ObjectId, sellerPlayer);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateBuyItemPayload(sellerObjectId: sellerPlayer.ObjectId, tradeActionId: 0, [(0, 1)]));

		var plan = Assert.Single(fixture.BuyItemPlans);
		Assert.NotNull(plan.PrivateStorePurchasePlan);
		var purchasePlan = plan.PrivateStorePurchasePlan;
		Assert.Equal(PrivateStorePurchasePlanStatus.PlanCreated, purchasePlan.Status);
		var skippedItem = Assert.Single(purchasePlan.SkippedMissingSellerItems);
		Assert.Equal((0, 3001, 100000001, 1L, 4_000L), (skippedItem.StoreIndex, skippedItem.ItemObjectId, skippedItem.ItemId, skippedItem.Count, skippedItem.PricePerItem));
		Assert.Empty(purchasePlan.SellerDeletedItemObjectIds);
		Assert.Empty(purchasePlan.SellerItemUpdates);
		Assert.Empty(purchasePlan.BuyerAddedItems);
		Assert.False(purchasePlan.SellerKinahWasCreated);
		Assert.False(purchasePlan.ShouldCloseSellerStore);

		var storeItem = Assert.Single(sellerPlayer.PrivateStoreItems);
		Assert.Equal((0, 3001, 1L), (storeItem.StoreIndex, storeItem.ItemObjectId, storeItem.Count));
		Assert.Equal("Practice wares", sellerPlayer.PrivateStoreMessage);
		Assert.True(sellerPlayer.IsInState(PlayerCreatureState.PrivateShop));
		Assert.DoesNotContain(sellerPlayer.InventoryItems, item => item.ObjectId == 3001);
		Assert.Contains(sellerPlayer.InventoryItems, item => item.ItemId == InventoryItemFactory.KinahItemId && item.Count == 4_500);
		Assert.DoesNotContain(activePlayer.InventoryItems, item => item.ObjectId == 9001);
		Assert.Contains(activePlayer.InventoryItems, item => item.ItemId == InventoryItemFactory.KinahItemId && item.Count == 6_000);
		Assert.Collection(
			fixture.SentPackets,
			packet => Assert.Equal(SmInventoryUpdateItem.DecreaseKinahBuy, Assert.IsType<SmInventoryUpdateItem>(packet).UpdateType));
		var sellerPacket = Assert.Single(fixture.Registry.DirectPackets);
		Assert.Equal(sellerPlayer.ObjectId, sellerPacket.PlayerObjectId);
		Assert.Equal(SmInventoryUpdateItem.IncreaseKinahCollect, Assert.IsType<SmInventoryUpdateItem>(sellerPacket.Packet).UpdateType);
		Assert.Empty(fixture.Registry.VisibleBroadcasts);
	}

	private static Player CreatePlayer() =>
		new()
		{
			ObjectId = 1001,
			Name = "BuyItemTester",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 1,
			IsOnline = true,
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};

	private static WorldNpc CreateNpc(
		int objectId,
		int templateId,
		WorldPosition position,
		IReadOnlyList<int>? functionDialogIds = null)
	{
		var template = new NpcTemplateSummary(
			templateId,
			"Trade Npc",
			NameId: 0,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "NONE",
			Type: "NPC",
			FunctionDialogIds: functionDialogIds);
		return new WorldNpc(objectId, templateId, template, position);
	}

	private sealed record WorldPet(
		int ObjectId,
		bool HasMerchantFunction,
		int? MerchantSellModifier) : IWorldPetObject;

	private static TradeListTable CreateTradeLists(params TradeListTemplateSummary[] purchaseLists)
	{
		return new TradeListTable(
			Array.Empty<TradeListTemplateSummary>(),
			Array.Empty<TradeListTemplateSummary>(),
			purchaseLists);
	}

	private static TradeListTable CreateBuyTradeLists(params TradeListTemplateSummary[] tradeLists)
	{
		return new TradeListTable(
			tradeLists,
			Array.Empty<TradeListTemplateSummary>(),
			Array.Empty<TradeListTemplateSummary>());
	}

	private static GoodsListTable CreateGoodsLists(params GoodsListSummary[] purchaseLists)
	{
		return new GoodsListTable(
			Array.Empty<GoodsListSummary>(),
			Array.Empty<GoodsListSummary>(),
			purchaseLists);
	}

	private static GoodsListTable CreateBuyGoodsLists(params GoodsListSummary[] tradeLists)
	{
		return new GoodsListTable(
			tradeLists,
			Array.Empty<GoodsListSummary>(),
			Array.Empty<GoodsListSummary>());
	}

	private static ItemTemplateTable CreateItemTemplates(params ItemTemplateSummary[] templates)
	{
		return new ItemTemplateTable(templates);
	}

	private static PetTemplateTable CreatePetTemplates(params PetTemplateSummary[] templates)
	{
		return new PetTemplateTable(templates);
	}

	private static ItemTemplateSummary Template(
		int itemId,
		long price,
		int requiredAbyssPoints = 0,
		int mask = 0,
		int maxStackCount = 1,
		string acquisitionType = "",
		int acquisitionItemId = 0,
		int acquisitionItemCount = 0)
	{
		return new ItemTemplateSummary(
			itemId,
			$"Item {itemId}",
			DescriptionId: 1,
			Mask: mask,
			Level: 1,
			ItemGroup: "NORMAL",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: maxStackCount,
			Price: price,
			ValidEquipmentSlots: 0,
			RequiredAbyssPoints: requiredAbyssPoints,
			AcquisitionType: acquisitionType,
			AcquisitionItemId: acquisitionItemId,
			AcquisitionItemCount: acquisitionItemCount);
	}

	private static Func<int> Sequence(int first)
	{
		var next = first - 1;
		return () => ++next;
	}

	internal static Task InvokeProcessPacketAsyncForAdapterTests(GameServerConnection connection, byte[] payload) =>
		InvokeProcessPacketAsync(connection, payload);

	private static async Task InvokeProcessPacketAsync(GameServerConnection connection, byte[] payload)
	{
		var method = typeof(GameServerConnection).GetMethod("ProcessPacketAsync", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		using var packet = new PacketBuffer(payload);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [packet]));
		await task;
	}

	internal static void SetActivePlayerForPacketDispatchForAdapterTests(GameServerConnection connection, Player player) =>
		SetActivePlayerForPacketDispatch(connection, player);

	private static void SetActivePlayerForPacketDispatch(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		activePlayerField.SetValue(connection, player);
		SetConnectionState(connection, GameConnectionState.InGame);
	}

	private static void SetConnectionState(GameServerConnection connection, GameConnectionState state)
	{
		var stateField = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(stateField);
		stateField.SetValue(connection, state);
	}

	internal static byte[] CreateBuyItemPayloadForAdapterTests(
		int sellerObjectId,
		int tradeActionId,
		IReadOnlyList<(int ItemObjectId, long Count)> items) =>
		CreateBuyItemPayload(sellerObjectId, tradeActionId, items);

	private static byte[] CreateBuyItemPayload(
		int sellerObjectId,
		int tradeActionId,
		IReadOnlyList<(int ItemObjectId, long Count)> items)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(51);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		buffer.WriteD(sellerObjectId);
		buffer.WriteH(tradeActionId);
		buffer.WriteH(items.Count);
		foreach (var (itemObjectId, count) in items)
		{
			buffer.WriteD(itemObjectId);
			buffer.WriteQ(count);
		}

		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static void AssertClosePrivateShopEmotion(SmEmotion packet, int expectedPlayerObjectId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedPlayerObjectId, reader.ReadD());
		Assert.Equal((int)Aion.GameServer.Model.EmotionType.ClosePrivateShop, reader.ReadC());
		Assert.Equal((int)PlayerCreatureState.Active, reader.ReadH());
		Assert.Equal(0f, reader.ReadF());
	}

	private static void AssertDeleteItemPayload(SmDeleteItem packet, int expectedObjectId, int expectedDeleteType)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedObjectId, reader.ReadD());
		Assert.Equal(expectedDeleteType, reader.ReadC());
	}

	private static TPacket AssertPacketEvent<TPacket>(PacketEvent packetEvent, string expectedRecipient, int? expectedPlayerObjectId = null)
		where TPacket : GameServerPacket
	{
		Assert.Equal(expectedRecipient, packetEvent.Recipient);
		Assert.Equal(expectedPlayerObjectId, packetEvent.PlayerObjectId);
		return Assert.IsType<TPacket>(packetEvent.Packet);
	}

	private static void AssertInventoryAddItemPayload(
		SmInventoryAddItem packet,
		int expectedObjectId,
		int expectedItemId,
		long expectedCount,
		int expectedAddType)
	{
		var addTypeField = typeof(SmInventoryAddItem).GetField("_addType", BindingFlags.Instance | BindingFlags.NonPublic);
		var itemsField = typeof(SmInventoryAddItem).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(addTypeField);
		Assert.NotNull(itemsField);
		Assert.Equal(expectedAddType, Assert.IsType<int>(addTypeField.GetValue(packet)));

		var items = Assert.IsAssignableFrom<IReadOnlyList<SmInventoryAddItem.InventoryPacketItem>>(itemsField.GetValue(packet));
		var packetItem = Assert.Single(items);
		Assert.Equal(expectedObjectId, packetItem.Item.ObjectId);
		Assert.Equal(expectedItemId, packetItem.Item.ItemId);
		Assert.Equal(expectedCount, packetItem.Item.Count);
	}

	private static void AssertCubeUpdatePayload(SmCubeUpdate packet, int expectedItemsCount)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(0, reader.ReadC());
		Assert.Equal(0, reader.ReadC());
		Assert.Equal(expectedItemsCount, reader.ReadD());
		Assert.Equal(0, reader.ReadC());
		Assert.Equal(0, reader.ReadC());
		Assert.Equal(0, reader.ReadC());
	}

	private static void AssertAbyssRankPayload(SmAbyssRank packet, int expectedAp, int expectedRank)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedAp, reader.ReadQ());
		reader.ReadD();
		Assert.Equal(expectedRank, reader.ReadD());
	}

	private static void AssertInvalidGoodsMessage(SmMessage packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		Assert.Equal("Some items are not allowed to be sold from this NPC.", reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	internal sealed class BuyItemFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private BuyItemFixture(
			TcpClient client,
			GameServerConnection connection,
			GameWorld world,
			CapturingConnectionRegistry registry,
			List<CmBuyItemHandlerCompositionPlan> buyItemPlans,
			List<CmBuyItemSideEffectOutcomePlan> buyItemSideEffectOutcomePlans,
			List<GameServerPacket> sentPackets,
			List<PacketEvent> packetEvents,
			CapturingLogger logger)
		{
			_client = client;
			Connection = connection;
			World = world;
			Registry = registry;
			BuyItemPlans = buyItemPlans;
			BuyItemSideEffectOutcomePlans = buyItemSideEffectOutcomePlans;
			SentPackets = sentPackets;
			PacketEvents = packetEvents;
			Logger = logger;
		}

		public GameServerConnection Connection { get; }

		public GameWorld World { get; }

		public CapturingConnectionRegistry Registry { get; }

		public List<CmBuyItemHandlerCompositionPlan> BuyItemPlans { get; }

		public List<CmBuyItemSideEffectOutcomePlan> BuyItemSideEffectOutcomePlans { get; }

		public List<GameServerPacket> SentPackets { get; }

		public List<PacketEvent> PacketEvents { get; }

		public CapturingLogger Logger { get; }

		public static async Task<BuyItemFixture> CreateAsync(
			Func<Player, int, object?, bool?>? buyItemKnownObjectResolver = null,
			TradeListTable? buyItemTradeLists = null,
			ItemTemplateTable? buyItemItemTemplates = null,
			GoodsListTable? buyItemGoodsLists = null,
			PetTemplateTable? buyItemPetTemplates = null,
			LimitedItemTradeService? limitedItemTradeService = null,
			long? buyItemCurrentSellLimit = null,
			Func<int>? buyItemDiagnosticObjectIdProvider = null,
			GameServerOptions? options = null,
			PriceInfluenceRates? buyItemPriceInfluenceRates = null,
			IPlayerEnterWorldRepository? playerEnterWorldRepository = null,
			bool observeBuyItemPlans = true)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var crypt = new GameCrypt(() => 0x01020304);
				crypt.EnableKey();
				var world = new GameWorld(NullLogger<GameWorld>.Instance);
				world.Initialize();
				var buyItemPlans = new List<CmBuyItemHandlerCompositionPlan>();
				var buyItemSideEffectOutcomePlans = new List<CmBuyItemSideEffectOutcomePlan>();
				var sentPackets = new List<GameServerPacket>();
				var packetEvents = new List<PacketEvent>();
				var registry = new CapturingConnectionRegistry(packetEvents);
				var logger = new CapturingLogger();
				var gameServerOptions = options ?? new GameServerOptions();
				var playerEnterWorldService = playerEnterWorldRepository == null
					? null
					: new PlayerEnterWorldService(
						gameServerOptions,
						playerEnterWorldRepository,
						world,
						NullLogger<PlayerEnterWorldService>.Instance);
				var fixture = new BuyItemFixture(
					client,
					new GameServerConnection(
						logger,
						serverClient,
						"cm-buy-item-test",
						new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
						options: gameServerOptions,
						world: world,
						playerEnterWorldService: playerEnterWorldService,
						connectionRegistry: registry,
						crypt: crypt,
						sentPacketObserver: packet =>
						{
							sentPackets.Add(packet);
							packetEvents.Add(new PacketEvent("active", null, packet));
						},
						cmBuyItemHandlerCompositionPlanObserver: observeBuyItemPlans ? buyItemPlans.Add : null,
						cmBuyItemSideEffectOutcomePlanObserver: observeBuyItemPlans ? buyItemSideEffectOutcomePlans.Add : null,
						buyItemKnownObjectResolver: buyItemKnownObjectResolver,
						buyItemTradeLists: buyItemTradeLists,
						buyItemItemTemplates: buyItemItemTemplates,
						buyItemGoodsLists: buyItemGoodsLists,
						buyItemPetTemplates: buyItemPetTemplates,
						limitedItemTradeService: limitedItemTradeService,
						buyItemCurrentSellLimit: buyItemCurrentSellLimit,
						buyItemDiagnosticObjectIdProvider: buyItemDiagnosticObjectIdProvider,
						buyItemPriceInfluenceRates: buyItemPriceInfluenceRates),
					world,
					registry,
					buyItemPlans,
					buyItemSideEffectOutcomePlans,
					sentPackets,
					packetEvents,
					logger);
				return fixture;
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}

	internal sealed record DirectRegistryPacket(int PlayerObjectId, GameServerPacket Packet);

	internal sealed record VisibleRegistryBroadcast(WorldPosition Position, int SourceObjectId, GameServerPacket Packet, bool IncludeSourcePlayer);

	internal sealed record PacketEvent(string Recipient, int? PlayerObjectId, GameServerPacket Packet);

	internal sealed record CapturedLog(LogLevel Level, string Message, Exception? Exception);

	internal sealed class CapturingLogger : ILogger
	{
		public List<CapturedLog> Entries { get; } = [];

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull =>
			NullScope.Instance;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			Entries.Add(new CapturedLog(logLevel, formatter(state, exception), exception));
		}

		private sealed class NullScope : IDisposable
		{
			public static NullScope Instance { get; } = new();

			public void Dispose()
			{
			}
		}
	}

	internal sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		private readonly List<PacketEvent> _packetEvents;

		public CapturingConnectionRegistry(List<PacketEvent> packetEvents)
		{
			_packetEvents = packetEvents;
		}

		public List<DirectRegistryPacket> DirectPackets { get; } = [];

		public List<VisibleRegistryBroadcast> VisibleBroadcasts { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			DirectPackets.Add(new DirectRegistryPacket(playerObjectId, packet));
			_packetEvents.Add(new PacketEvent("direct", playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null) =>
			Task.FromResult(0);

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			VisibleBroadcasts.Add(new VisibleRegistryBroadcast(sourcePosition, sourceObjectId, packet, includeSourcePlayer));
			_packetEvents.Add(new PacketEvent("visible", sourceObjectId, packet));
			return Task.FromResult(1);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null) =>
			Task.FromResult(0);

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null) =>
			Task.FromResult(0);

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates) =>
			Task.FromResult(0);

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail) =>
			Task.FromResult(false);

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah) =>
			Task.FromResult(false);
	}
}
