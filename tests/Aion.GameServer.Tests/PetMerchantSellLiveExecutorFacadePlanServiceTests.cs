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
		Assert.False(facade.ShouldDispatchLiveSideEffects);
		var operation = Assert.Single(facade.Operations);
		Assert.Equal(PetMerchantSellLiveExecutorOperationStatus.NotAttemptedMissingPlan, operation.Status);
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
