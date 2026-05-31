using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PetMerchantSellLiveExecutorFacadePlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_RecordsJavaPetSellSideEffectBoundariesWithoutDispatch()
	{
		var sellPlan = new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.PlanCreated,
			SellerDeletedItemObjectIds: [2001],
			SellerItemUpdates: [],
			RepurchaseItems: [new RepurchaseSourceItem(new InventoryItem { ObjectId = 2001, ItemId = 100000001, Count = 1 }, RepurchasePrice: 330)],
			KinahUpdate: new InventoryItem { ObjectId = 3001, ItemId = InventoryItemFactory.KinahItemId, Count = 1_330 },
			"TradeService.performSellToShop");
		var handlerPlan = CreatePetMerchantHandlerPlan(petSellModifier: 33, sellPlan);

		var facade = PetMerchantSellLiveExecutorFacadePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(PetMerchantSellLiveExecutorFacadeStatus.DisabledNoSideEffects, facade.Status);
		Assert.Same(handlerPlan, facade.HandlerPlan);
		Assert.Same(sellPlan, facade.SellToShopPlan);
		Assert.Equal(33, facade.PetSellModifier);
		Assert.True(facade.IsDisabled);
		Assert.False(facade.IsLive);
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		Assert.Equal(PetMerchantSellPersistenceAdapterStatus.DisabledNoWrites, facade.PersistenceAdapterPlan.Status);
		Assert.Equal(PetMerchantSellSendAdapterStatus.DisabledNoPackets, facade.SendAdapterPlan.Status);
		Assert.True(facade.PersistenceAdapterPlan.WouldWriteRepository);
		Assert.False(facade.PersistenceAdapterPlan.DidWriteRepository);
		Assert.True(facade.SendAdapterPlan.WouldSendPackets);
		Assert.False(facade.SendAdapterPlan.WouldSendAutoSellNotification);
		Assert.True(facade.WouldMutateSellerInventory);
		Assert.False(facade.DidMutateSellerInventory);
		Assert.True(facade.WouldAddRepurchaseItems);
		Assert.False(facade.DidAddRepurchaseItems);
		Assert.True(facade.WouldMutateKinah);
		Assert.False(facade.DidMutateKinah);
		Assert.Collection(
			facade.Operations.Select(operation => operation.Kind),
			kind => Assert.Equal(PetMerchantSellLiveExecutorOperationKind.ApplySellerInventoryMutation, kind),
			kind => Assert.Equal(PetMerchantSellLiveExecutorOperationKind.AddRepurchaseItems, kind),
			kind => Assert.Equal(PetMerchantSellLiveExecutorOperationKind.IncreaseKinah, kind));
		Assert.All(facade.Operations, operation => Assert.Equal(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedDisabled, operation.Status));
	}

	[Fact]
	public void CreateDisabledOutcomePlan_GroupsPetSellFacadeWithoutCommitting()
	{
		var sellPlan = CreateSellPlan();
		var facade = PetMerchantSellLiveExecutorFacadePlanService.CreateDisabledPlan(CreatePetMerchantHandlerPlan(petSellModifier: 33, sellPlan));

		var outcome = PetMerchantSellOutcomePlanService.CreateDisabledPlan(facade);

		Assert.Equal(PetMerchantSellOutcomePlanStatus.DisabledNoTransaction, outcome.Status);
		Assert.Same(facade, outcome.FacadePlan);
		Assert.Same(facade.PersistenceAdapterPlan, outcome.PersistenceAdapterPlan);
		Assert.Same(facade.SendAdapterPlan, outcome.SendAdapterPlan);
		Assert.Same(sellPlan, outcome.SellToShopPlan);
		Assert.True(outcome.WouldWritePersistence);
		Assert.False(outcome.DidWritePersistence);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.DidSendPackets);
		Assert.False(outcome.WouldSendAutoSellNotification);
		Assert.False(outcome.DidSendAutoSellNotification);
		Assert.True(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.DidMutateSellerInventory);
		Assert.True(outcome.WouldAddRepurchaseItems);
		Assert.False(outcome.DidAddRepurchaseItems);
		Assert.True(outcome.WouldMutateKinah);
		Assert.False(outcome.DidMutateKinah);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.DidCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
		Assert.Collection(
			outcome.Steps.Select(step => step.Kind),
			kind => Assert.Equal(PetMerchantSellOutcomeStepKind.PersistRepositoryWrites, kind),
			kind => Assert.Equal(PetMerchantSellOutcomeStepKind.DispatchPacketIntents, kind),
			kind => Assert.Equal(PetMerchantSellOutcomeStepKind.CommitTransactionBoundary, kind));
		Assert.All(outcome.Steps, step =>
		{
			Assert.True(step.WouldRun);
			Assert.False(step.DidRun);
		});
	}

	[Fact]
	public void CreateDisabledOutcomePlan_MissingFacadeStopsBeforeTransactionBoundary()
	{
		var outcome = PetMerchantSellOutcomePlanService.CreateDisabledPlan(null);

		Assert.Equal(PetMerchantSellOutcomePlanStatus.MissingFacadePlan, outcome.Status);
		Assert.Null(outcome.FacadePlan);
		Assert.Null(outcome.PersistenceAdapterPlan);
		Assert.Null(outcome.SendAdapterPlan);
		Assert.Null(outcome.SellToShopPlan);
		Assert.Empty(outcome.Steps);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldSendAutoSellNotification);
		Assert.False(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldAddRepurchaseItems);
		Assert.False(outcome.WouldMutateKinah);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreateDisabledOutcomePlan_BlockedFacadeCarriesSellPlanWithoutCommitting()
	{
		var sellPlan = new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.BlockedCannotTrade,
			SellerDeletedItemObjectIds: [],
			SellerItemUpdates: [],
			RepurchaseItems: [],
			KinahUpdate: null,
			"TradeService.performSellToShop -> !PlayerRestrictions.canTrade(player) -> false");
		var facade = PetMerchantSellLiveExecutorFacadePlanService.CreateDisabledPlan(CreatePetMerchantHandlerPlan(petSellModifier: 33, sellPlan));

		var outcome = PetMerchantSellOutcomePlanService.CreateDisabledPlan(facade);

		Assert.Equal(PetMerchantSellOutcomePlanStatus.FacadeNotReady, outcome.Status);
		Assert.Same(facade, outcome.FacadePlan);
		Assert.Same(facade.PersistenceAdapterPlan, outcome.PersistenceAdapterPlan);
		Assert.Same(facade.SendAdapterPlan, outcome.SendAdapterPlan);
		Assert.Same(sellPlan, outcome.SellToShopPlan);
		Assert.Empty(outcome.Steps);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldSendAutoSellNotification);
		Assert.False(outcome.WouldMutateSellerInventory);
		Assert.False(outcome.WouldAddRepurchaseItems);
		Assert.False(outcome.WouldMutateKinah);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreateDisabledPlan_NonPetMerchantHandlerPlanIsNotEligible()
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]);
		var handlerPlan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc));

		var facade = PetMerchantSellLiveExecutorFacadePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(PetMerchantSellLiveExecutorFacadeStatus.HandlerNotPetMerchantSell, facade.Status);
		Assert.Same(handlerPlan, facade.HandlerPlan);
		Assert.Null(facade.SellToShopPlan);
		Assert.Equal(PetMerchantSellPersistenceAdapterStatus.MissingSellToShopPlan, facade.PersistenceAdapterPlan.Status);
		Assert.Equal(PetMerchantSellSendAdapterStatus.MissingSellToShopPlan, facade.SendAdapterPlan.Status);
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		var operation = Assert.Single(facade.Operations);
		Assert.Equal(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedNotPetMerchantSell, operation.Status);
	}

	[Fact]
	public void CreateDisabledPlan_MissingSellPlanStopsBeforeSideEffects()
	{
		var handlerPlan = CreatePetMerchantHandlerPlan(petSellModifier: 33, sellPlan: null);

		var facade = PetMerchantSellLiveExecutorFacadePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(PetMerchantSellLiveExecutorFacadeStatus.MissingSellToShopPlan, facade.Status);
		Assert.Equal(33, facade.PetSellModifier);
		Assert.Null(facade.SellToShopPlan);
		Assert.Equal(PetMerchantSellPersistenceAdapterStatus.MissingSellToShopPlan, facade.PersistenceAdapterPlan.Status);
		Assert.Equal(PetMerchantSellSendAdapterStatus.MissingSellToShopPlan, facade.SendAdapterPlan.Status);
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		var operation = Assert.Single(facade.Operations);
		Assert.Equal(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedCompositionNotReady, operation.Status);
	}

	[Fact]
	public void CreateDisabledPlan_BlockedSellPlanStopsBeforeSideEffects()
	{
		var sellPlan = new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.BlockedCannotTrade,
			SellerDeletedItemObjectIds: [],
			SellerItemUpdates: [],
			RepurchaseItems: [],
			KinahUpdate: null,
			"TradeService.performSellToShop -> !PlayerRestrictions.canTrade(player) -> false");
		var handlerPlan = CreatePetMerchantHandlerPlan(petSellModifier: 33, sellPlan);

		var facade = PetMerchantSellLiveExecutorFacadePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(PetMerchantSellLiveExecutorFacadeStatus.SellToShopPlanNotReady, facade.Status);
		Assert.Same(sellPlan, facade.SellToShopPlan);
		Assert.Equal(PetMerchantSellPersistenceAdapterStatus.SellToShopPlanNotReady, facade.PersistenceAdapterPlan.Status);
		Assert.Equal(PetMerchantSellSendAdapterStatus.SellToShopPlanNotReady, facade.SendAdapterPlan.Status);
		Assert.False(facade.WouldMutateSellerInventory);
		Assert.False(facade.WouldAddRepurchaseItems);
		Assert.False(facade.WouldMutateKinah);
		var operation = Assert.Single(facade.Operations);
		Assert.Equal(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedCompositionNotReady, operation.Status);
	}

	[Fact]
	public void CreateDisabledPlan_MissingHandlerPlanDoesNotDispatch()
	{
		var facade = PetMerchantSellLiveExecutorFacadePlanService.CreateDisabledPlan(null);

		Assert.Equal(PetMerchantSellLiveExecutorFacadeStatus.MissingHandlerPlan, facade.Status);
		Assert.Null(facade.HandlerPlan);
		Assert.Equal(PetMerchantSellPersistenceAdapterStatus.MissingSellToShopPlan, facade.PersistenceAdapterPlan.Status);
		Assert.Equal(PetMerchantSellSendAdapterStatus.MissingSellToShopPlan, facade.SendAdapterPlan.Status);
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		var operation = Assert.Single(facade.Operations);
		Assert.Equal(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedMissingPlan, operation.Status);
	}

	[Fact]
	public void CreateDisabledPersistenceAdapter_RecordsTradeServiceRepositoryWritesWithoutWriting()
	{
		var sellPlan = new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.PlanCreated,
			SellerDeletedItemObjectIds: [2001],
			SellerItemUpdates: [new InventoryItem { ObjectId = 2002, ItemId = 182003001, Count = 2, OwnerId = 1001 }],
			RepurchaseItems:
			[
				new RepurchaseSourceItem(new InventoryItem { ObjectId = 2001, ItemId = 100000001, Count = 1, OwnerId = 1001 }, RepurchasePrice: 330),
				new RepurchaseSourceItem(new InventoryItem { ObjectId = 4001, ItemId = 182003001, Count = 3, OwnerId = 1001 }, RepurchasePrice: 90),
			],
			KinahUpdate: new InventoryItem { ObjectId = 3001, ItemId = InventoryItemFactory.KinahItemId, Count = 1_420, OwnerId = 1001 },
			"TradeService.performSellToShop");

		var adapter = PetMerchantSellPersistenceAdapterPlanService.CreateDisabledPlan(sellPlan);

		Assert.Equal(PetMerchantSellPersistenceAdapterStatus.DisabledNoWrites, adapter.Status);
		Assert.Same(sellPlan, adapter.SellToShopPlan);
		Assert.True(adapter.WouldWriteRepository);
		Assert.False(adapter.DidWriteRepository);
		Assert.False(adapter.ShouldDispatchLiveSideEffects);
		Assert.False(adapter.IsLive);
		Assert.Collection(
			adapter.Operations.Select(operation => operation.Kind),
			kind => Assert.Equal(PetMerchantSellPersistenceOperationKind.SaveSellerItemUpdate, kind),
			kind => Assert.Equal(PetMerchantSellPersistenceOperationKind.DeleteSellerItem, kind),
			kind => Assert.Equal(PetMerchantSellPersistenceOperationKind.SaveRepurchaseItem, kind),
			kind => Assert.Equal(PetMerchantSellPersistenceOperationKind.SaveRepurchaseItem, kind),
			kind => Assert.Equal(PetMerchantSellPersistenceOperationKind.SaveKinah, kind));
		Assert.All(adapter.Operations, operation =>
		{
			Assert.True(operation.WouldWrite);
			Assert.False(operation.DidWrite);
		});
	}

	[Fact]
	public void CreateDisabledSendAdapter_RecordsCmBuyItemPetMerchantPacketIntentsWithoutAutoSellNotification()
	{
		var sellPlan = new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.PlanCreated,
			SellerDeletedItemObjectIds: [2001],
			SellerItemUpdates: [new InventoryItem { ObjectId = 2002, ItemId = 182003001, Count = 2, OwnerId = 1001 }],
			RepurchaseItems: [],
			KinahUpdate: new InventoryItem { ObjectId = 3001, ItemId = InventoryItemFactory.KinahItemId, Count = 1_420, OwnerId = 1001 },
			"TradeService.performSellToShop");

		var adapter = PetMerchantSellSendAdapterPlanService.CreateDisabledPlan(sellPlan);

		Assert.Equal(PetMerchantSellSendAdapterStatus.DisabledNoPackets, adapter.Status);
		Assert.Same(sellPlan, adapter.SellToShopPlan);
		Assert.True(adapter.WouldSendPackets);
		Assert.False(adapter.DidSendPackets);
		Assert.False(adapter.WouldSendAutoSellNotification);
		Assert.False(adapter.DidSendAutoSellNotification);
		Assert.False(adapter.ShouldDispatchLiveSideEffects);
		Assert.False(adapter.IsLive);
		Assert.Collection(
			adapter.Intents.Select(intent => intent.Kind),
			kind => Assert.Equal(PetMerchantSellSendIntentKind.SendSellerItemUpdate, kind),
			kind => Assert.Equal(PetMerchantSellSendIntentKind.SendSellerItemDelete, kind),
			kind => Assert.Equal(PetMerchantSellSendIntentKind.SendKinahUpdate, kind));
		Assert.All(adapter.Intents, intent =>
		{
			Assert.True(intent.WouldSend);
			Assert.False(intent.DidSend);
		});
	}

	[Fact]
	public void CreateDisabledAdapters_MissingAndBlockedSellPlansStopBeforeSideEffects()
	{
		var blockedSellPlan = new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.BlockedNotSellable,
			SellerDeletedItemObjectIds: [],
			SellerItemUpdates: [],
			RepurchaseItems: [],
			KinahUpdate: null,
			"TradeService.performSellToShop -> !item.isSellable()");

		var missingPersistence = PetMerchantSellPersistenceAdapterPlanService.CreateDisabledPlan(null);
		var missingSend = PetMerchantSellSendAdapterPlanService.CreateDisabledPlan(null);
		var blockedPersistence = PetMerchantSellPersistenceAdapterPlanService.CreateDisabledPlan(blockedSellPlan);
		var blockedSend = PetMerchantSellSendAdapterPlanService.CreateDisabledPlan(blockedSellPlan);

		Assert.Equal(PetMerchantSellPersistenceAdapterStatus.MissingSellToShopPlan, missingPersistence.Status);
		Assert.Equal(PetMerchantSellSendAdapterStatus.MissingSellToShopPlan, missingSend.Status);
		Assert.Equal(PetMerchantSellPersistenceAdapterStatus.SellToShopPlanNotReady, blockedPersistence.Status);
		Assert.Equal(PetMerchantSellSendAdapterStatus.SellToShopPlanNotReady, blockedSend.Status);
		Assert.All(new[] { missingPersistence, blockedPersistence }, adapter =>
		{
			Assert.Empty(adapter.Operations);
			Assert.False(adapter.WouldWriteRepository);
		});
		Assert.All(new[] { missingSend, blockedSend }, adapter =>
		{
			Assert.Empty(adapter.Intents);
			Assert.False(adapter.WouldSendPackets);
			Assert.False(adapter.WouldSendAutoSellNotification);
		});
	}

	private static CmBuyItemHandlerCompositionPlan CreatePetMerchantHandlerPlan(int? petSellModifier, TradeSellToShopPlan? sellPlan)
	{
		var packet = CreatePacket(17, [new CmBuyItemEntry(2001, 1)]);
		return CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Pet,
				PetHasMerchantFunction: true,
				PetSellModifier: petSellModifier,
				PetSellToShopPlan: sellPlan));
	}

	private static TradeSellToShopPlan CreateSellPlan()
	{
		return new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.PlanCreated,
			SellerDeletedItemObjectIds: [2001],
			SellerItemUpdates: [],
			RepurchaseItems: [new RepurchaseSourceItem(new InventoryItem { ObjectId = 2001, ItemId = 100000001, Count = 1 }, RepurchasePrice: 330)],
			KinahUpdate: new InventoryItem { ObjectId = 3001, ItemId = InventoryItemFactory.KinahItemId, Count = 1_330 },
			"TradeService.performSellToShop");
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
