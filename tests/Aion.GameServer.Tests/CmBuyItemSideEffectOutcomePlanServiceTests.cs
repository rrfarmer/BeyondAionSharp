using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemSideEffectOutcomePlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_ComposesPrivateStoreFinalOutcomeWithoutDispatch()
	{
		var handlerPlan = CreatePrivateStoreHandlerPlan(CreatePrivateStorePurchasePlan());

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.PrivateStoreOutcomeCreated, outcome.Status);
		Assert.Same(handlerPlan, outcome.HandlerPlan);
		Assert.NotNull(outcome.PrivateStoreFacadePlan);
		Assert.NotNull(outcome.PrivateStoreOutcomePlan);
		Assert.Null(outcome.PetMerchantSellFacadePlan);
		Assert.Null(outcome.PetMerchantSellOutcomePlan);
		Assert.Null(outcome.BuyFromShopOutcomePlan);
		Assert.Null(outcome.RepurchaseOutcomePlan);
		Assert.Null(outcome.SellToShopOutcomePlan);
		Assert.Null(outcome.SellForApToShopOutcomePlan);
		Assert.Equal(PrivateStorePurchaseOutcomePlanStatus.DisabledNoTransaction, outcome.PrivateStoreOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.True(outcome.WouldMutateBuyerInventory);
		Assert.True(outcome.WouldMutateKinah);
		Assert.False(outcome.WouldAddRepurchaseItems);
		Assert.True(outcome.WouldSendPackets);
		Assert.True(outcome.WouldWriteExchangeLog);
		Assert.False(outcome.WouldWriteAuditLog);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreateDisabledPlan_ComposesPetMerchantFinalOutcomeWithoutDispatch()
	{
		var sellPlan = CreatePetSellPlan();
		var handlerPlan = CreatePetMerchantHandlerPlan(sellPlan);

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.PetMerchantSellOutcomeCreated, outcome.Status);
		Assert.Same(handlerPlan, outcome.HandlerPlan);
		Assert.Null(outcome.PrivateStoreFacadePlan);
		Assert.Null(outcome.PrivateStoreOutcomePlan);
		Assert.Null(outcome.BuyFromShopOutcomePlan);
		Assert.Null(outcome.RepurchaseOutcomePlan);
		Assert.Null(outcome.SellToShopOutcomePlan);
		Assert.Null(outcome.SellForApToShopOutcomePlan);
		Assert.NotNull(outcome.PetMerchantSellFacadePlan);
		Assert.NotNull(outcome.PetMerchantSellOutcomePlan);
		Assert.Equal(PetMerchantSellOutcomePlanStatus.DisabledNoTransaction, outcome.PetMerchantSellOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldMutateBuyerInventory);
		Assert.True(outcome.WouldMutateKinah);
		Assert.True(outcome.WouldAddRepurchaseItems);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.WouldWriteExchangeLog);
		Assert.False(outcome.WouldWriteAuditLog);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreateDisabledPlan_ComposesBuyFromShopFinalOutcomeWithoutDispatch()
	{
		var transactionPlan = CreateBuyTransactionPlan();
		var handlerPlan = CreateBuyFromShopHandlerPlan(transactionPlan);

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.BuyFromShopOutcomeCreated, outcome.Status);
		Assert.Same(handlerPlan, outcome.HandlerPlan);
		Assert.Null(outcome.PrivateStoreFacadePlan);
		Assert.Null(outcome.PrivateStoreOutcomePlan);
		Assert.Null(outcome.PetMerchantSellFacadePlan);
		Assert.Null(outcome.PetMerchantSellOutcomePlan);
		Assert.NotNull(outcome.BuyFromShopOutcomePlan);
		Assert.Null(outcome.RepurchaseOutcomePlan);
		Assert.Null(outcome.SellToShopOutcomePlan);
		Assert.Null(outcome.SellForApToShopOutcomePlan);
		Assert.Equal(TradeBuyTransactionOutcomePlanStatus.DisabledNoTransaction, outcome.BuyFromShopOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldMutateSellerInventory);
		Assert.True(outcome.WouldMutateBuyerInventory);
		Assert.True(outcome.WouldMutateKinah);
		Assert.False(outcome.WouldAddRepurchaseItems);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.WouldWriteExchangeLog);
		Assert.False(outcome.WouldWriteAuditLog);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreateDisabledPlan_BuyFromShopSelectionWithoutTransactionPlanCarriesMissingOutcome()
	{
		var handlerPlan = CreateBuyFromShopHandlerPlan(buyTransactionPlan: null);

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.BuyFromShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeBuyTransactionOutcomePlanStatus.MissingTransactionPlan, outcome.BuyFromShopOutcomePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldWriteAuditLog);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreateDisabledPlan_ComposesBuyFromShopAuditOutcomeWithoutDispatch()
	{
		var transactionPlan = new TradeBuyTransactionPlan(
			TradeBuyTransactionPlanStatus.AuditNegativeRequiredAp,
			[TradeBuyTransactionStep.CheckRequiredApExploit],
			RequiredKinah: 0,
			RequiredAbyssPoints: -100,
			RequiredItems: [],
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performBuyTransaction -> tradeList.getRequiredAp() < 0",
			AuditReason: "possibly used packet hack: tradeList.getRequiredAp() < 0");
		var handlerPlan = CreateBuyFromShopHandlerPlan(transactionPlan);

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.BuyFromShopOutcomeCreated, outcome.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldSendPackets);
		Assert.True(outcome.WouldWriteAuditLog);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreateDisabledPlan_ComposesRepurchaseFinalOutcomeWithoutDispatch()
	{
		var repurchasePlan = CreateRepurchasePlan();
		var handlerPlan = CreateRepurchaseHandlerPlan(repurchasePlan);

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.RepurchaseOutcomeCreated, outcome.Status);
		Assert.Same(handlerPlan, outcome.HandlerPlan);
		Assert.Null(outcome.PrivateStoreFacadePlan);
		Assert.Null(outcome.PrivateStoreOutcomePlan);
		Assert.Null(outcome.PetMerchantSellFacadePlan);
		Assert.Null(outcome.PetMerchantSellOutcomePlan);
		Assert.Null(outcome.BuyFromShopOutcomePlan);
		Assert.NotNull(outcome.RepurchaseOutcomePlan);
		Assert.Null(outcome.SellToShopOutcomePlan);
		Assert.Null(outcome.SellForApToShopOutcomePlan);
		Assert.Equal(RepurchaseOutcomePlanStatus.DisabledNoTransaction, outcome.RepurchaseOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldMutateSellerInventory);
		Assert.True(outcome.WouldMutateBuyerInventory);
		Assert.True(outcome.WouldMutateKinah);
		Assert.False(outcome.WouldAddRepurchaseItems);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.WouldWriteExchangeLog);
		Assert.False(outcome.WouldWriteAuditLog);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
		Assert.Contains(outcome.RepurchaseOutcomePlan.Steps, step => step.Kind == RepurchaseOutcomeStepKind.RemoveRepurchaseItems);
		Assert.Equal(
			[
				RepurchaseSuccessOperationKind.DecreaseKinah,
				RepurchaseSuccessOperationKind.AddItem,
				RepurchaseSuccessOperationKind.RemoveRepurchaseItem,
			],
			outcome.RepurchaseOutcomePlan.SuccessOperations.Select(operation => operation.Kind));
		Assert.Contains(outcome.RepurchaseOutcomePlan.PacketIntents, intent => intent.Kind == RepurchasePacketIntentKind.SendKinahUpdate);
		Assert.Contains(outcome.RepurchaseOutcomePlan.PacketIntents, intent => intent.Kind == RepurchasePacketIntentKind.SendRepurchasedItemAdd);
		Assert.Null(outcome.RepurchaseOutcomePlan.StateItemRemovalPlan);
	}

	[Fact]
	public void CreateDisabledPlan_RepurchaseOutcomeCarriesSuppliedStateRemovalSnapshot()
	{
		var repurchasePlan = CreateRepurchasePlan();
		var handlerPlan = CreateRepurchaseHandlerPlan(repurchasePlan);
		var playerObjectId = 7001;
		var removedItem = new RepurchaseSourceItem(
			new InventoryItem { ObjectId = 2001, ItemId = 100000001, Count = 1, OwnerId = playerObjectId },
			RepurchasePrice: 330);
		var remainingItem = new RepurchaseSourceItem(
			new InventoryItem { ObjectId = 2002, ItemId = 100000002, Count = 1, OwnerId = playerObjectId },
			RepurchasePrice: 120);
		var currentSnapshot = new RepurchaseStateSnapshot(
			playerObjectId,
			[removedItem, remainingItem],
			"test current RepurchaseService state");

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(
			handlerPlan,
			playerObjectId,
			[currentSnapshot]);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.RepurchaseOutcomeCreated, outcome.Status);
		Assert.NotNull(outcome.RepurchaseOutcomePlan!.StateItemRemovalPlan);
		var stateRemoval = outcome.RepurchaseOutcomePlan.StateItemRemovalPlan!;
		Assert.Equal(RepurchaseStateItemRemovalPlanStatus.SnapshotUpdated, stateRemoval.Status);
		Assert.Equal([2001], stateRemoval.RemovedItemObjectIds);
		Assert.Empty(stateRemoval.MissingItemObjectIds);
		Assert.Equal([2002], stateRemoval.UpdatedSnapshot!.RepurchaseItems.Select(item => item.Item.ObjectId));
		Assert.False(stateRemoval.DidRemoveItems);
		Assert.False(stateRemoval.IsLive);
	}

	[Fact]
	public void CreateDisabledPlan_RepurchaseSelectionWithoutExecutionPlanCarriesMissingOutcome()
	{
		var handlerPlan = CreateRepurchaseHandlerPlan(repurchasePlan: null);

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.RepurchaseOutcomeCreated, outcome.Status);
		Assert.Equal(RepurchaseOutcomePlanStatus.MissingRepurchasePlan, outcome.RepurchaseOutcomePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreateDisabledPlan_ComposesApSellFinalOutcomeWithoutDispatch()
	{
		var apSellPlan = CreateApSellPlan();
		var handlerPlan = CreateApSellHandlerPlan(apSellPlan);

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellForApToShopOutcomeCreated, outcome.Status);
		Assert.Same(handlerPlan, outcome.HandlerPlan);
		Assert.Null(outcome.PrivateStoreFacadePlan);
		Assert.Null(outcome.PrivateStoreOutcomePlan);
		Assert.Null(outcome.PetMerchantSellFacadePlan);
		Assert.Null(outcome.PetMerchantSellOutcomePlan);
		Assert.Null(outcome.BuyFromShopOutcomePlan);
		Assert.Null(outcome.RepurchaseOutcomePlan);
		Assert.NotNull(outcome.SellForApToShopOutcomePlan);
		Assert.Equal(TradeSellForApToShopOutcomePlanStatus.DisabledNoTransaction, outcome.SellForApToShopOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldMutateBuyerInventory);
		Assert.False(outcome.WouldMutateKinah);
		Assert.False(outcome.WouldAddRepurchaseItems);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.WouldWriteExchangeLog);
		Assert.False(outcome.WouldWriteAuditLog);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreateDisabledPlan_ComposesSellToShopFinalOutcomeWithoutDispatch()
	{
		var sellPlan = CreateSellToShopPlan();
		var handlerPlan = CreateSellToShopHandlerPlan(sellPlan);

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellToShopOutcomeCreated, outcome.Status);
		Assert.Same(handlerPlan, outcome.HandlerPlan);
		Assert.Null(outcome.PrivateStoreFacadePlan);
		Assert.Null(outcome.PrivateStoreOutcomePlan);
		Assert.Null(outcome.PetMerchantSellFacadePlan);
		Assert.Null(outcome.PetMerchantSellOutcomePlan);
		Assert.Null(outcome.BuyFromShopOutcomePlan);
		Assert.Null(outcome.RepurchaseOutcomePlan);
		Assert.NotNull(outcome.SellToShopOutcomePlan);
		Assert.Null(outcome.SellForApToShopOutcomePlan);
		Assert.Equal(TradeSellToShopOutcomePlanStatus.DisabledNoTransaction, outcome.SellToShopOutcomePlan!.Status);
		Assert.True(outcome.WouldWritePersistence);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldMutateBuyerInventory);
		Assert.True(outcome.WouldMutateKinah);
		Assert.True(outcome.WouldAddRepurchaseItems);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.WouldWriteExchangeLog);
		Assert.False(outcome.WouldWriteAuditLog);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreateDisabledPlan_SellToShopSelectionWithoutSellPlanCarriesMissingOutcome()
	{
		var handlerPlan = CreateSellToShopHandlerPlan(sellPlan: null);

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellToShopOutcomePlanStatus.MissingSellToShopPlan, outcome.SellToShopOutcomePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreateDisabledPlan_ApSellSelectionWithoutApPlanCarriesMissingOutcome()
	{
		var handlerPlan = CreateApSellHandlerPlan(apSellPlan: null);

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.SellForApToShopOutcomeCreated, outcome.Status);
		Assert.Equal(TradeSellForApToShopOutcomePlanStatus.MissingSellForApToShopPlan, outcome.SellForApToShopOutcomePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreateDisabledPlan_BlockedPrivateStoreSelectionCarriesTerminalOutcome()
	{
		var packet = CreatePacket(0, [new CmBuyItemEntry(4, 1)]);
		var handlerPlan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Player,
				PrivateStoreItems: []));

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.PrivateStoreOutcomeCreated, outcome.Status);
		Assert.Equal(PrivateStoreLiveExecutorFacadeStatus.BoughtItemsPlanNotReady, outcome.PrivateStoreFacadePlan!.Status);
		Assert.Equal(PrivateStorePurchaseOutcomePlanStatus.FacadeNotReady, outcome.PrivateStoreOutcomePlan!.Status);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreateDisabledPlan_BlockedPrivateStoreAuditIntentPropagatesToOutcome()
	{
		var handlerPlan = CreatePrivateStoreHandlerPlan(CreateBlockedPrivateStoreAuditPurchasePlan());

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.PrivateStoreOutcomeCreated, outcome.Status);
		Assert.Equal(PrivateStoreLiveExecutorFacadeStatus.PurchasePlanNotReady, outcome.PrivateStoreFacadePlan!.Status);
		Assert.Equal(PrivateStorePurchaseOutcomePlanStatus.FacadeNotReady, outcome.PrivateStoreOutcomePlan!.Status);
		Assert.True(outcome.WouldWriteAuditLog);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreateDisabledPlan_NonOutcomeHandlerPlanDoesNotCreateFinalOutcome()
	{
		var handlerPlan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				CreatePacket(99, [new CmBuyItemEntry(100000001, 1)]),
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc));

		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.HandlerNotOutcomeEligible, outcome.Status);
		Assert.Same(handlerPlan, outcome.HandlerPlan);
		Assert.Null(outcome.PrivateStoreOutcomePlan);
		Assert.Null(outcome.PetMerchantSellOutcomePlan);
		Assert.Null(outcome.BuyFromShopOutcomePlan);
		Assert.Null(outcome.RepurchaseOutcomePlan);
		Assert.Null(outcome.SellToShopOutcomePlan);
		Assert.Null(outcome.SellForApToShopOutcomePlan);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreateDisabledPlan_MissingHandlerPlanDoesNotCreateFinalOutcome()
	{
		var outcome = CmBuyItemSideEffectOutcomePlanService.CreateDisabledPlan(null);

		Assert.Equal(CmBuyItemSideEffectOutcomePlanStatus.MissingHandlerPlan, outcome.Status);
		Assert.Null(outcome.HandlerPlan);
		Assert.Null(outcome.PrivateStoreOutcomePlan);
		Assert.Null(outcome.PetMerchantSellOutcomePlan);
		Assert.Null(outcome.BuyFromShopOutcomePlan);
		Assert.Null(outcome.RepurchaseOutcomePlan);
		Assert.Null(outcome.SellToShopOutcomePlan);
		Assert.Null(outcome.SellForApToShopOutcomePlan);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	private static CmBuyItemHandlerCompositionPlan CreatePrivateStoreHandlerPlan(PrivateStorePurchasePlan purchasePlan)
	{
		return CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				CreatePacket(0, [new CmBuyItemEntry(0, 1)]),
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Player,
				PrivateStoreItems:
				[
					new PrivateStoreListedItemSummary(0, ItemObjectId: 3001, ItemId: 100000001, Count: 1, PricePerItem: 10_000, ItemName: "Practice Sword"),
				],
				PrivateStorePurchasePlan: purchasePlan));
	}

	private static CmBuyItemHandlerCompositionPlan CreatePetMerchantHandlerPlan(TradeSellToShopPlan sellPlan)
	{
		return CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				CreatePacket(17, [new CmBuyItemEntry(2001, 1)]),
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Pet,
				PetHasMerchantFunction: true,
				PetSellModifier: 33,
				PetSellToShopPlan: sellPlan));
	}

	private static CmBuyItemHandlerCompositionPlan CreateBuyFromShopHandlerPlan(TradeBuyTransactionPlan? buyTransactionPlan)
	{
		return CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]),
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				SellTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL"),
				BuyTransactionPlan: buyTransactionPlan));
	}

	private static CmBuyItemHandlerCompositionPlan CreateRepurchaseHandlerPlan(RepurchasePlan? repurchasePlan)
	{
		return CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				CreatePacket(2, [new CmBuyItemEntry(2001, 1)]),
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				NpcCanBuy: true,
				RepurchasableItemObjectIds: new HashSet<int> { 2001 },
				RepurchasePlan: repurchasePlan));
	}

	private static CmBuyItemHandlerCompositionPlan CreateSellToShopHandlerPlan(TradeSellToShopPlan? sellPlan)
	{
		return CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				CreatePacket(1, [new CmBuyItemEntry(2001, 1)]),
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				NpcCanBuy: true,
				PurchaseTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL", BuyPriceRate: 35),
				SellToShopPlan: sellPlan));
	}

	private static CmBuyItemHandlerCompositionPlan CreateApSellHandlerPlan(TradeSellForApToShopPlan? apSellPlan)
	{
		return CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				CreatePacket(1, [new CmBuyItemEntry(2001, 1)]),
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				NpcCanPurchase: true,
				PurchaseTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "ABYSS", BuyPriceRate: 35),
				SellForApToShopPlan: apSellPlan));
	}

	private static PrivateStorePurchasePlan CreatePrivateStorePurchasePlan()
	{
		var sellerItem = new InventoryItem { ObjectId = 3001, ItemId = 100000001, Count = 0, OwnerId = 7001 };
		var buyerItem = new InventoryItem { ObjectId = 4001, ItemId = 100000001, Count = 1, OwnerId = 8001 };
		var buyerKinah = new InventoryItem { ObjectId = 5001, ItemId = InventoryItemFactory.KinahItemId, Count = 90_000, OwnerId = 8001 };
		var sellerKinah = new InventoryItem { ObjectId = 5002, ItemId = InventoryItemFactory.KinahItemId, Count = 10_000, OwnerId = 7001 };
		var notification = PrivateStoreSellNotificationPlanService.CreatePlan(1, "Practice Sword");

		return new PrivateStorePurchasePlan(
			PrivateStorePurchasePlanStatus.PlanCreated,
			BoughtItems: [new PrivateStorePurchaseItemRequest(0, 3001, 100000001, Count: 1, PricePerItem: 10_000, ItemName: "Practice Sword")],
			SellerItemUpdates: [],
			SellerDeletedItemObjectIds: [sellerItem.ObjectId],
			BuyerAddedItems: [buyerItem],
			BuyerUpdatedItems: [],
			BuyerKinahUpdate: buyerKinah,
			SellerKinahUpdate: sellerKinah,
			BuyerMessages: [],
			SellerMessages: [notification.NotificationMessage!],
			WouldWriteAuditLog: false,
			AuditMessage: null,
			ShouldCloseSellerStore: true,
			"PrivateStoreService.sellStoreItem");
	}

	private static PrivateStorePurchasePlan CreateBlockedPrivateStoreAuditPurchasePlan()
	{
		return new PrivateStorePurchasePlan(
			PrivateStorePurchasePlanStatus.BlockedSellerItemCountChanged,
			BoughtItems: [new PrivateStorePurchaseItemRequest(0, 3001, 100000001, Count: 2, PricePerItem: 10_000, ItemName: "Practice Sword")],
			SellerItemUpdates: [],
			SellerDeletedItemObjectIds: [],
			BuyerAddedItems: [],
			BuyerUpdatedItems: [],
			BuyerKinahUpdate: null,
			SellerKinahUpdate: null,
			BuyerMessages: [],
			SellerMessages: [],
			WouldWriteAuditLog: true,
			AuditMessage: "tried to buy more than players private store item stack count",
			ShouldCloseSellerStore: false,
			"PrivateStoreService.sellStoreItem -> item.getItemCount() < boughtItem.getCount() -> audit and return");
	}

	private static TradeSellToShopPlan CreatePetSellPlan()
	{
		return new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.PlanCreated,
			SellerDeletedItemObjectIds: [2001],
			SellerItemUpdates: [],
			RepurchaseItems: [new RepurchaseSourceItem(new InventoryItem { ObjectId = 2001, ItemId = 100000001, Count = 1 }, RepurchasePrice: 330)],
			KinahUpdate: new InventoryItem { ObjectId = 3001, ItemId = InventoryItemFactory.KinahItemId, Count = 1_330 },
			"TradeService.performSellToShop");
	}

	private static TradeSellToShopPlan CreateSellToShopPlan()
	{
		return new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.PlanCreated,
			SellerDeletedItemObjectIds: [2001],
			SellerItemUpdates: [],
			RepurchaseItems: [new RepurchaseSourceItem(new InventoryItem { ObjectId = 2001, ItemId = 100000001, Count = 1 }, RepurchasePrice: 330)],
			KinahUpdate: new InventoryItem { ObjectId = 3001, ItemId = InventoryItemFactory.KinahItemId, Count = 1_330 },
			"TradeService.performSellToShop");
	}

	private static RepurchasePlan CreateRepurchasePlan()
	{
		return new RepurchasePlan(
			RepurchasePlanStatus.PlanCreated,
			RequestedItemObjectIds: [2001],
			RepurchasedItemObjectIds: [2001],
			MissingRepurchaseItemObjectIds: [],
			InsufficientKinahItemObjectIds: [],
			AddedItems: [new InventoryItem { ObjectId = 4001, ItemId = 100000001, Count = 1 }],
			UpdatedItems: [],
			KinahUpdate: new InventoryItem { ObjectId = 3001, ItemId = InventoryItemFactory.KinahItemId, Count = 1_000 },
			RemovedRepurchaseItemObjectIds: [2001],
			Messages: [],
			AuditMessages: [],
			"RepurchaseService.repurchaseFromShop");
	}

	private static TradeBuyTransactionPlan CreateBuyTransactionPlan()
	{
		var mutation = new TradeBuyTransactionMutationDescriptor(
			RequiredKinah: 1_000,
			RequiredAbyssPoints: 500,
			RequiredItems: [new TradeBuyTransactionRequiredItem(186000001, 2)],
			AddedItems: [new TradeBuyTransactionItemRequest(100000001, 1, UnitBuyPrice: 1_000, RequiredApPerItem: 500, AcquisitionType: "AP", RequiredItemId: 186000001, RequiredItemCountPerItem: 2)],
			LimitedItemUpdateItemIds: [100000001],
			JavaSource: "TradeService.performBuyTransaction steps 6-7",
			IsLive: false);

		return new TradeBuyTransactionPlan(
			TradeBuyTransactionPlanStatus.WouldApplyBuyTransaction,
			[
				TradeBuyTransactionStep.CheckPlayerCanTrade,
				TradeBuyTransactionStep.ValidateBuyItems,
				TradeBuyTransactionStep.SnapshotInventoryFreeSlots,
				TradeBuyTransactionStep.ClassifyTradeNpcRates,
				TradeBuyTransactionStep.CalculateKinahPrice,
				TradeBuyTransactionStep.CalculateAbyssRewardRequirements,
				TradeBuyTransactionStep.CheckRequiredApExploit,
				TradeBuyTransactionStep.CheckInventoryFreeSlots,
				TradeBuyTransactionStep.CheckLimitedItems,
				TradeBuyTransactionStep.PlanCostSubtraction,
				TradeBuyTransactionStep.PlanItemAddsAndLimitUpdates,
			],
			RequiredKinah: 1_000,
			RequiredAbyssPoints: 500,
			RequiredItems: [new TradeBuyTransactionRequiredItem(186000001, 2)],
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performBuyTransaction",
			Mutation: mutation);
	}

	private static TradeSellForApToShopPlan CreateApSellPlan()
	{
		return new TradeSellForApToShopPlan(
			TradeSellForApToShopPlanStatus.PlanCreated,
			[
				TradeSellForApToShopStep.CheckSellingApItemsEnabled,
				TradeSellForApToShopStep.CheckPlayerCanTrade,
				TradeSellForApToShopStep.FindInventoryItem,
				TradeSellForApToShopStep.ValidatePurchaseTemplateGoods,
				TradeSellForApToShopStep.PlanInventoryDecrease,
				TradeSellForApToShopStep.PlanAbyssPointReward,
			],
			DeletedItemObjectIds: [2001],
			SkippedDeleteFailedItemObjectIds: [],
			AbyssPointRewards: [new TradeSellForApToShopApReward(2001, ItemId: 100000001, Count: 1, RequiredApPerItem: 1_000, ApReward: 350)],
			TotalAbyssPoints: 350,
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performSellForAPToShop");
	}

	private static CmBuyItem CreatePacket(int tradeActionId, IReadOnlyList<CmBuyItemEntry> entries)
	{
		using var buffer = new PacketBuffer();
		buffer.WriteD(SellerObjectId);
		buffer.WriteH(tradeActionId);
		buffer.WriteH(entries.Count);
		foreach (var entry in entries)
		{
			buffer.WriteD(entry.ItemObjectId);
			buffer.WriteQ(entry.Count);
		}

		var packet = new CmBuyItem(51, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private const int SellerObjectId = 7001;
}
