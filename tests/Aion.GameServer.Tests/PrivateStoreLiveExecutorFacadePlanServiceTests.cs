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
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		Assert.False(facade.WouldMutateSellerInventory);
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
