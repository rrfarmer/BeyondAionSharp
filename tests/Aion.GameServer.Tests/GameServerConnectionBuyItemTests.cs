using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Data;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
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
	public async Task ProcessPacketAsync_CmBuyItemNpcRepurchaseHydratesDisabledExecutionPlanFromSnapshot()
	{
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000)),
			buyItemDiagnosticObjectIdProvider: Sequence(8001));
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
		Assert.Single(repurchasePlan.AddedItems);
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
		Assert.Empty(fixture.SentPackets);
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
	public async Task ProcessPacketAsync_CmBuyItemNpcBuyFromShopBlocksDisabledLimitedItemPlan()
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
		Assert.Empty(fixture.SentPackets);
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
		await using var fixture = await BuyItemFixture.CreateAsync(
			buyItemTradeLists: CreateTradeLists(
				new TradeListTemplateSummary(700001, [129], NpcType: "ABYSS", BuyPriceRate: 35)),
			buyItemItemTemplates: CreateItemTemplates(
				Template(100000001, price: 1_000, requiredAbyssPoints: 1_000)),
			buyItemGoodsLists: CreateGoodsLists(
				new GoodsListSummary(129, Items: [new GoodsListItemSummary(100000001)])));
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
		Assert.Empty(fixture.SentPackets);
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
		Assert.Empty(fixture.SentPackets);
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
			long? buyItemCurrentSellLimit = null,
			Func<int>? buyItemDiagnosticObjectIdProvider = null,
			GameServerOptions? options = null,
			PriceInfluenceRates? buyItemPriceInfluenceRates = null,
			IPlayerEnterWorldRepository? playerEnterWorldRepository = null)
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
						cmBuyItemHandlerCompositionPlanObserver: buyItemPlans.Add,
						cmBuyItemSideEffectOutcomePlanObserver: buyItemSideEffectOutcomePlans.Add,
						buyItemKnownObjectResolver: buyItemKnownObjectResolver,
						buyItemTradeLists: buyItemTradeLists,
						buyItemItemTemplates: buyItemItemTemplates,
						buyItemGoodsLists: buyItemGoodsLists,
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
