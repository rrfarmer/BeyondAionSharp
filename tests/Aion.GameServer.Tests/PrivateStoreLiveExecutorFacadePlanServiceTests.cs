using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PrivateStoreLiveExecutorFacadePlanServiceTests
{
	[Fact]
	public void CreateDisabledPlan_RecordsJavaSideEffectBoundariesWithoutDispatch()
	{
		var handlerPlan = CreatePrivateStoreHandlerPlan(CreatePurchasePlan());

		var facade = PrivateStoreLiveExecutorFacadePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(PrivateStoreLiveExecutorFacadeStatus.DisabledNoSideEffects, facade.Status);
		Assert.Same(handlerPlan, facade.HandlerPlan);
		Assert.Same(handlerPlan.PrivateStorePurchasePlan, facade.PurchasePlan);
		Assert.Equal(PrivateStorePersistenceAdapterStatus.DisabledNoWrites, facade.PersistenceAdapterPlan.Status);
		Assert.Same(facade.PurchasePlan, facade.PersistenceAdapterPlan.PurchasePlan);
		Assert.True(facade.PersistenceAdapterPlan.WouldWriteRepository);
		Assert.False(facade.PersistenceAdapterPlan.DidWriteRepository);
		Assert.False(facade.PersistenceAdapterPlan.ShouldDispatchLiveSideEffects);
		Assert.Equal(PrivateStoreSendAdapterStatus.DisabledNoPackets, facade.SendAdapterPlan.Status);
		Assert.Same(facade.PurchasePlan, facade.SendAdapterPlan.PurchasePlan);
		Assert.True(facade.SendAdapterPlan.WouldSendPackets);
		Assert.False(facade.SendAdapterPlan.DidSendPackets);
		Assert.True(facade.SendAdapterPlan.WouldWriteExchangeLog);
		Assert.False(facade.SendAdapterPlan.DidWriteExchangeLog);
		Assert.False(facade.SendAdapterPlan.ShouldDispatchLiveSideEffects);
		Assert.True(facade.IsDisabled);
		Assert.False(facade.IsLive);
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		Assert.True(facade.WouldMutateSellerInventory);
		Assert.False(facade.DidMutateSellerInventory);
		Assert.True(facade.WouldMutateBuyerInventory);
		Assert.False(facade.DidMutateBuyerInventory);
		Assert.True(facade.WouldSendSellerMessages);
		Assert.False(facade.DidSendSellerMessages);
		Assert.True(facade.WouldWriteExchangeLog);
		Assert.False(facade.DidWriteExchangeLog);
		Assert.True(facade.WouldMutateBuyerKinah);
		Assert.False(facade.DidMutateBuyerKinah);
		Assert.True(facade.WouldMutateSellerKinah);
		Assert.False(facade.DidMutateSellerKinah);
		Assert.True(facade.WouldCloseSellerStore);
		Assert.False(facade.DidCloseSellerStore);
		Assert.Collection(
			facade.Operations.Select(operation => operation.Kind),
			kind => Assert.Equal(PrivateStoreLiveExecutorOperationKind.DecreaseSellerItem, kind),
			kind => Assert.Equal(PrivateStoreLiveExecutorOperationKind.UpdateSellerStoreItem, kind),
			kind => Assert.Equal(PrivateStoreLiveExecutorOperationKind.AddBuyerItem, kind),
			kind => Assert.Equal(PrivateStoreLiveExecutorOperationKind.SendSellerNotification, kind),
			kind => Assert.Equal(PrivateStoreLiveExecutorOperationKind.LogPrivateStoreSale, kind),
			kind => Assert.Equal(PrivateStoreLiveExecutorOperationKind.DecreaseBuyerKinah, kind),
			kind => Assert.Equal(PrivateStoreLiveExecutorOperationKind.IncreaseSellerKinah, kind),
			kind => Assert.Equal(PrivateStoreLiveExecutorOperationKind.CloseSellerStore, kind));
		Assert.All(facade.Operations, operation => Assert.Equal(PrivateStoreLiveExecutorOperationStatus.NotAttemptedDisabled, operation.Status));
	}

	[Fact]
	public void CreateDisabledPersistenceAdapterPlan_RecordsRepositoryWriteIntentsWithoutWriting()
	{
		var purchasePlan = CreatePurchasePlan();

		var adapter = PrivateStorePersistenceAdapterPlanService.CreateDisabledPlan(purchasePlan);

		Assert.Equal(PrivateStorePersistenceAdapterStatus.DisabledNoWrites, adapter.Status);
		Assert.Same(purchasePlan, adapter.PurchasePlan);
		Assert.True(adapter.WouldWriteRepository);
		Assert.False(adapter.DidWriteRepository);
		Assert.False(adapter.ShouldDispatchLiveSideEffects);
		Assert.False(adapter.IsLive);
		Assert.Collection(
			adapter.Operations.Select(operation => operation.Kind),
			kind => Assert.Equal(PrivateStorePersistenceOperationKind.DeleteSellerItem, kind),
			kind => Assert.Equal(PrivateStorePersistenceOperationKind.SaveBuyerAddedItem, kind),
			kind => Assert.Equal(PrivateStorePersistenceOperationKind.SaveBuyerKinah, kind),
			kind => Assert.Equal(PrivateStorePersistenceOperationKind.SaveSellerKinah, kind),
			kind => Assert.Equal(PrivateStorePersistenceOperationKind.UpdateSellerStoreItem, kind),
			kind => Assert.Equal(PrivateStorePersistenceOperationKind.CloseSellerStore, kind));
		Assert.All(adapter.Operations, operation =>
		{
			Assert.True(operation.WouldWrite);
			Assert.False(operation.DidWrite);
		});
	}

	[Fact]
	public void CreateDisabledSendAdapterPlan_RecordsPacketAndLogIntentsWithoutSending()
	{
		var purchasePlan = CreatePurchasePlan();

		var adapter = PrivateStoreSendAdapterPlanService.CreateDisabledPlan(purchasePlan);

		Assert.Equal(PrivateStoreSendAdapterStatus.DisabledNoPackets, adapter.Status);
		Assert.Same(purchasePlan, adapter.PurchasePlan);
		Assert.True(adapter.WouldSendPackets);
		Assert.False(adapter.DidSendPackets);
		Assert.True(adapter.WouldWriteExchangeLog);
		Assert.False(adapter.DidWriteExchangeLog);
		Assert.False(adapter.ShouldDispatchLiveSideEffects);
		Assert.False(adapter.IsLive);
		Assert.Collection(
			adapter.Intents.Select(intent => intent.Kind),
			kind => Assert.Equal(PrivateStoreSendIntentKind.SendSellerItemDelete, kind),
			kind => Assert.Equal(PrivateStoreSendIntentKind.SendBuyerItemAdd, kind),
			kind => Assert.Equal(PrivateStoreSendIntentKind.SendBuyerKinahUpdate, kind),
			kind => Assert.Equal(PrivateStoreSendIntentKind.SendSellerKinahUpdate, kind),
			kind => Assert.Equal(PrivateStoreSendIntentKind.SendSellerNotification, kind),
			kind => Assert.Equal(PrivateStoreSendIntentKind.BroadcastSellerStoreClose, kind),
			kind => Assert.Equal(PrivateStoreSendIntentKind.WriteExchangeLog, kind));
		Assert.All(adapter.Intents, intent =>
		{
			Assert.True(intent.WouldSend);
			Assert.False(intent.DidSend);
		});
	}

	[Fact]
	public void CreateDisabledAdapterPlans_MissingPurchasePlanStopsBeforeIntents()
	{
		var persistence = PrivateStorePersistenceAdapterPlanService.CreateDisabledPlan(null);
		var send = PrivateStoreSendAdapterPlanService.CreateDisabledPlan(null);

		Assert.Equal(PrivateStorePersistenceAdapterStatus.MissingPurchasePlan, persistence.Status);
		Assert.Empty(persistence.Operations);
		Assert.False(persistence.WouldWriteRepository);
		Assert.False(persistence.DidWriteRepository);
		Assert.False(persistence.ShouldDispatchLiveSideEffects);
		Assert.False(persistence.IsLive);
		Assert.Equal(PrivateStoreSendAdapterStatus.MissingPurchasePlan, send.Status);
		Assert.Empty(send.Intents);
		Assert.False(send.WouldSendPackets);
		Assert.False(send.DidSendPackets);
		Assert.False(send.WouldWriteExchangeLog);
		Assert.False(send.DidWriteExchangeLog);
		Assert.False(send.ShouldDispatchLiveSideEffects);
		Assert.False(send.IsLive);
	}

	[Fact]
	public void CreateDisabledAdapterPlans_BlockedPurchasePlanStopsBeforeIntents()
	{
		var blockedPlan = CreateBlockedPurchasePlan();

		var persistence = PrivateStorePersistenceAdapterPlanService.CreateDisabledPlan(blockedPlan);
		var send = PrivateStoreSendAdapterPlanService.CreateDisabledPlan(blockedPlan);

		Assert.Equal(PrivateStorePersistenceAdapterStatus.PurchasePlanNotReady, persistence.Status);
		Assert.Same(blockedPlan, persistence.PurchasePlan);
		Assert.Empty(persistence.Operations);
		Assert.False(persistence.WouldWriteRepository);
		Assert.False(persistence.DidWriteRepository);
		Assert.False(persistence.ShouldDispatchLiveSideEffects);
		Assert.False(persistence.IsLive);
		Assert.Equal(PrivateStoreSendAdapterStatus.PurchasePlanNotReady, send.Status);
		Assert.Same(blockedPlan, send.PurchasePlan);
		Assert.Empty(send.Intents);
		Assert.False(send.WouldSendPackets);
		Assert.False(send.DidSendPackets);
		Assert.False(send.WouldWriteExchangeLog);
		Assert.False(send.DidWriteExchangeLog);
		Assert.False(send.ShouldDispatchLiveSideEffects);
		Assert.False(send.IsLive);
	}

	[Fact]
	public void CreateDisabledOutcomePlan_GroupsFacadeAdaptersWithoutCommitting()
	{
		var facade = PrivateStoreLiveExecutorFacadePlanService.CreateDisabledPlan(CreatePrivateStoreHandlerPlan(CreatePurchasePlan()));

		var outcome = PrivateStorePurchaseOutcomePlanService.CreateDisabledPlan(facade);

		Assert.Equal(PrivateStorePurchaseOutcomePlanStatus.DisabledNoTransaction, outcome.Status);
		Assert.Same(facade, outcome.FacadePlan);
		Assert.Same(facade.PersistenceAdapterPlan, outcome.PersistenceAdapterPlan);
		Assert.Same(facade.SendAdapterPlan, outcome.SendAdapterPlan);
		Assert.True(outcome.WouldWritePersistence);
		Assert.False(outcome.DidWritePersistence);
		Assert.True(outcome.WouldSendPackets);
		Assert.False(outcome.DidSendPackets);
		Assert.True(outcome.WouldWriteExchangeLog);
		Assert.False(outcome.DidWriteExchangeLog);
		Assert.True(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.DidCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
		Assert.Collection(
			outcome.Steps.Select(step => step.Kind),
			kind => Assert.Equal(PrivateStorePurchaseOutcomeStepKind.PersistRepositoryWrites, kind),
			kind => Assert.Equal(PrivateStorePurchaseOutcomeStepKind.DispatchPacketAndLogIntents, kind),
			kind => Assert.Equal(PrivateStorePurchaseOutcomeStepKind.CommitTransactionBoundary, kind));
		Assert.All(outcome.Steps, step =>
		{
			Assert.True(step.WouldRun);
			Assert.False(step.DidRun);
		});
	}

	[Fact]
	public void CreateDisabledOutcomePlan_MissingFacadeStopsBeforeTransactionBoundary()
	{
		var outcome = PrivateStorePurchaseOutcomePlanService.CreateDisabledPlan(null);

		Assert.Equal(PrivateStorePurchaseOutcomePlanStatus.MissingFacadePlan, outcome.Status);
		Assert.Null(outcome.FacadePlan);
		Assert.Null(outcome.PersistenceAdapterPlan);
		Assert.Null(outcome.SendAdapterPlan);
		Assert.Empty(outcome.Steps);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldWriteExchangeLog);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreateDisabledOutcomePlan_BlockedFacadeCarriesTerminalAdaptersWithoutCommitting()
	{
		var facade = PrivateStoreLiveExecutorFacadePlanService.CreateDisabledPlan(CreatePrivateStoreHandlerPlan(CreateBlockedPurchasePlan()));

		var outcome = PrivateStorePurchaseOutcomePlanService.CreateDisabledPlan(facade);

		Assert.Equal(PrivateStorePurchaseOutcomePlanStatus.FacadeNotReady, outcome.Status);
		Assert.Same(facade, outcome.FacadePlan);
		Assert.Same(facade.PersistenceAdapterPlan, outcome.PersistenceAdapterPlan);
		Assert.Same(facade.SendAdapterPlan, outcome.SendAdapterPlan);
		Assert.Equal(PrivateStorePersistenceAdapterStatus.PurchasePlanNotReady, outcome.PersistenceAdapterPlan!.Status);
		Assert.Equal(PrivateStoreSendAdapterStatus.PurchasePlanNotReady, outcome.SendAdapterPlan!.Status);
		Assert.Empty(outcome.Steps);
		Assert.False(outcome.WouldWritePersistence);
		Assert.False(outcome.WouldSendPackets);
		Assert.False(outcome.WouldWriteExchangeLog);
		Assert.False(outcome.WouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldCommitTransactionBoundary);
		Assert.False(outcome.ShouldDispatchLiveSideEffects);
		Assert.False(outcome.IsLive);
	}

	[Fact]
	public void CreateDisabledPlan_BlockedBoughtItemsPlanStopsBeforeSideEffects()
	{
		var packet = CreatePacket(0, [new CmBuyItemEntry(4, 1)]);
		var handlerPlan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Player,
				PrivateStoreItems: []));

		var facade = PrivateStoreLiveExecutorFacadePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(PrivateStoreLiveExecutorFacadeStatus.BoughtItemsPlanNotReady, facade.Status);
		Assert.Equal(PrivateStoreBoughtItemsPlanStatus.BlockedInvalidStoreIndex, facade.BoughtItemsPlan!.Status);
		Assert.Null(facade.PurchasePlan);
		Assert.Equal(PrivateStorePersistenceAdapterStatus.MissingPurchasePlan, facade.PersistenceAdapterPlan.Status);
		Assert.Equal(PrivateStoreSendAdapterStatus.MissingPurchasePlan, facade.SendAdapterPlan.Status);
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		Assert.False(facade.WouldMutateSellerInventory);
		var operation = Assert.Single(facade.Operations);
		Assert.Equal(PrivateStoreLiveExecutorOperationStatus.NotAttemptedCompositionNotReady, operation.Status);
	}

	[Fact]
	public void CreateDisabledPlan_BlockedPurchasePlanCarriesBlockedAdapterPlans()
	{
		var handlerPlan = CreatePrivateStoreHandlerPlan(CreateBlockedPurchasePlan());

		var facade = PrivateStoreLiveExecutorFacadePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(PrivateStoreLiveExecutorFacadeStatus.PurchasePlanNotReady, facade.Status);
		Assert.Equal(PrivateStorePurchasePlanStatus.BlockedInsufficientKinah, facade.PurchasePlan!.Status);
		Assert.Equal(PrivateStorePersistenceAdapterStatus.PurchasePlanNotReady, facade.PersistenceAdapterPlan.Status);
		Assert.Same(facade.PurchasePlan, facade.PersistenceAdapterPlan.PurchasePlan);
		Assert.Empty(facade.PersistenceAdapterPlan.Operations);
		Assert.False(facade.PersistenceAdapterPlan.ShouldDispatchLiveSideEffects);
		Assert.Equal(PrivateStoreSendAdapterStatus.PurchasePlanNotReady, facade.SendAdapterPlan.Status);
		Assert.Same(facade.PurchasePlan, facade.SendAdapterPlan.PurchasePlan);
		Assert.Empty(facade.SendAdapterPlan.Intents);
		Assert.False(facade.SendAdapterPlan.ShouldDispatchLiveSideEffects);
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		var operation = Assert.Single(facade.Operations);
		Assert.Equal(PrivateStoreLiveExecutorOperationStatus.NotAttemptedCompositionNotReady, operation.Status);
	}

	[Fact]
	public void CreateDisabledPlan_NonPrivateStoreHandlerPlanIsNotEligible()
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]);
		var handlerPlan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc));

		var facade = PrivateStoreLiveExecutorFacadePlanService.CreateDisabledPlan(handlerPlan);

		Assert.Equal(PrivateStoreLiveExecutorFacadeStatus.HandlerNotPrivateStore, facade.Status);
		Assert.Same(handlerPlan, facade.HandlerPlan);
		Assert.Null(facade.BoughtItemsPlan);
		Assert.Null(facade.PurchasePlan);
		Assert.Equal(PrivateStorePersistenceAdapterStatus.MissingPurchasePlan, facade.PersistenceAdapterPlan.Status);
		Assert.Equal(PrivateStoreSendAdapterStatus.MissingPurchasePlan, facade.SendAdapterPlan.Status);
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		var operation = Assert.Single(facade.Operations);
		Assert.Equal(PrivateStoreLiveExecutorOperationStatus.NotAttemptedNotPrivateStore, operation.Status);
	}

	[Fact]
	public void CreateDisabledPlan_MissingHandlerPlanDoesNotDispatch()
	{
		var facade = PrivateStoreLiveExecutorFacadePlanService.CreateDisabledPlan(null);

		Assert.Equal(PrivateStoreLiveExecutorFacadeStatus.MissingHandlerPlan, facade.Status);
		Assert.Null(facade.HandlerPlan);
		Assert.Equal(PrivateStorePersistenceAdapterStatus.MissingPurchasePlan, facade.PersistenceAdapterPlan.Status);
		Assert.Equal(PrivateStoreSendAdapterStatus.MissingPurchasePlan, facade.SendAdapterPlan.Status);
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		var operation = Assert.Single(facade.Operations);
		Assert.Equal(PrivateStoreLiveExecutorOperationStatus.NotAttemptedMissingPlan, operation.Status);
	}

	private static CmBuyItemHandlerCompositionPlan CreatePrivateStoreHandlerPlan(PrivateStorePurchasePlan purchasePlan)
	{
		var packet = CreatePacket(0, [new CmBuyItemEntry(0, 1)]);
		return CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Player,
				PrivateStoreItems:
				[
					new PrivateStoreListedItemSummary(0, ItemObjectId: 3001, ItemId: 100000001, Count: 1, PricePerItem: 10_000, ItemName: "Practice Sword"),
				],
				PrivateStorePurchasePlan: purchasePlan));
	}

	private static PrivateStorePurchasePlan CreatePurchasePlan()
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
			ShouldCloseSellerStore: true,
			"PrivateStoreService.sellStoreItem");
	}

	private static PrivateStorePurchasePlan CreateBlockedPurchasePlan()
	{
		return new PrivateStorePurchasePlan(
			PrivateStorePurchasePlanStatus.BlockedInsufficientKinah,
			BoughtItems: [new PrivateStorePurchaseItemRequest(0, 3001, 100000001, Count: 1, PricePerItem: 10_000, ItemName: "Practice Sword")],
			SellerItemUpdates: [],
			SellerDeletedItemObjectIds: [],
			BuyerAddedItems: [],
			BuyerUpdatedItems: [],
			BuyerKinahUpdate: null,
			SellerKinahUpdate: null,
			BuyerMessages: [],
			SellerMessages: [],
			ShouldCloseSellerStore: false,
			"PrivateStoreService.sellStoreItem -> price > buyer.getInventory().getKinah() -> return");
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
