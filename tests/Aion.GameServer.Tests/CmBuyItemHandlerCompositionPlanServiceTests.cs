using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemHandlerCompositionPlanServiceTests
{
	[Fact]
	public void CreatePlan_SelectsSellToShopPlannerForNpcActionOne()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(200, 2)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				NpcCanBuy: true));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedSellToShopPlanner, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Contains(CmBuyItemHandlerCompositionStep.InvokeSellToShopPlanner, plan.Steps);
		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellToShop, plan.SellToShopPlan!.Status);
		Assert.Null(plan.RepurchasePlan);
		Assert.Null(plan.BuyFromShopPlan);
	}

	[Fact]
	public void CreatePlan_ForwardsApSellPlanForAbyssPurchaseTemplate()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(200, 1)]);
		var apSellPlan = CreateTradeSellForApToShopPlan();

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				NpcCanPurchase: true,
				PurchaseTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "ABYSS"),
				SellForApToShopPlan: apSellPlan));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedSellToShopPlanner, plan.Status);
		Assert.Equal(CmBuyItemSellToShopCompositionPlanStatus.WouldDispatchSellForApToShop, plan.SellToShopPlan!.Status);
		Assert.Contains(CmBuyItemSellToShopCompositionStep.AttachSellForApToShopPlan, plan.SellToShopPlan.Steps);
		Assert.Same(apSellPlan, plan.SellToShopPlan.Dispatch!.SellForApToShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreatePlan_SelectsRepurchasePlannerForNpcActionTwo()
	{
		var packet = CreatePacket(2, [new CmBuyItemEntry(101, 1), new CmBuyItemEntry(102, 1)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				RepurchasableItemObjectIds: new HashSet<int> { 101 }));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedRepurchasePlanner, plan.Status);
		Assert.Contains(CmBuyItemHandlerCompositionStep.InvokeRepurchasePlanner, plan.Steps);
		Assert.Equal(CmBuyItemRepurchaseCompositionPlanStatus.WouldDispatchRepurchase, plan.RepurchasePlan!.Status);
		Assert.Equal([101], plan.RepurchasePlan.ReadPlan.RepurchaseItemObjectIds);
		Assert.Null(plan.SellToShopPlan);
		Assert.Null(plan.BuyFromShopPlan);
	}

	[Theory]
	[InlineData(13)]
	[InlineData(14)]
	[InlineData(15)]
	[InlineData(16)]
	public void CreatePlan_SelectsBuyFromShopPlannerForNpcActionsThirteenThroughSixteen(int actionId)
	{
		var packet = CreatePacket(actionId, [new CmBuyItemEntry(100000001, 1)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				SellTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "ABYSS")));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedBuyFromShopPlanner, plan.Status);
		Assert.Contains(CmBuyItemHandlerCompositionStep.InvokeBuyFromShopPlanner, plan.Steps);
		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.WouldDispatchBuyFromShop, plan.BuyFromShopPlan!.Status);
		Assert.False(plan.BuyFromShopPlan.Dispatch!.UseKinah);
		Assert.Null(plan.SellToShopPlan);
		Assert.Null(plan.RepurchasePlan);
	}

	[Fact]
	public void CreatePlan_ParserAuditStopsBeforeBranchSelection()
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1), new CmBuyItemEntry(0, 1)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				SellTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL")));

		Assert.True(packet.IsAudit);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedReadAudit, plan.Status);
		Assert.DoesNotContain(CmBuyItemHandlerCompositionStep.InvokeBuyFromShopPlanner, plan.Steps);
		Assert.Null(plan.BuyFromShopPlan);
	}

	[Fact]
	public void CreatePlan_MissingPlayerAndUnknownTargetStopBeforeBranchSelection()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(200, 1)]);

		var missingPlayerPlan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(packet, PlayerPresent: false, TargetKind: CmBuyItemRunTargetKind.Npc));
		var unknownTargetPlan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(packet, PlayerPresent: true, TargetKind: CmBuyItemRunTargetKind.Unknown));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedMissingPlayer, missingPlayerPlan.Status);
		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedUnknownTarget, unknownTargetPlan.Status);
		Assert.Null(missingPlayerPlan.SellToShopPlan);
		Assert.Null(unknownTargetPlan.SellToShopPlan);
	}

	[Fact]
	public void CreatePlan_SelectsPrivateStorePlannerForPlayerActionZero()
	{
		var packet = CreatePacket(0, [new CmBuyItemEntry(1, 2)]);
		var purchasePlan = CreatePrivateStorePurchasePlan();

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Player,
				PrivateStoreItems:
				[
					new PrivateStoreListedItemSummary(0, ItemObjectId: 3001, ItemId: 100000001, Count: 1, PricePerItem: 10_000, ItemName: "Practice Sword"),
					new PrivateStoreListedItemSummary(1, ItemObjectId: 3002, ItemId: 182003001, Count: 5, PricePerItem: 300, ItemName: "Practice Bundle"),
				],
				PrivateStorePurchasePlan: purchasePlan));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedPrivateStorePlanner, plan.Status);
		Assert.Contains(CmBuyItemHandlerCompositionStep.InvokePrivateStorePlanner, plan.Steps);
		Assert.Equal(PrivateStoreBoughtItemsPlanStatus.PlanCreated, plan.PrivateStoreBoughtItemsPlan!.Status);
		var boughtItem = Assert.Single(plan.PrivateStoreBoughtItemsPlan.BoughtItems);
		Assert.Equal((1, 3002, 182003001, 2L, 300L), (boughtItem.StoreIndex, boughtItem.ItemObjectId, boughtItem.ItemId, boughtItem.Count, boughtItem.PricePerItem));
		Assert.Same(purchasePlan, plan.PrivateStorePurchasePlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Null(plan.SellToShopPlan);
		Assert.Null(plan.RepurchasePlan);
		Assert.Null(plan.BuyFromShopPlan);
	}

	[Fact]
	public void CreatePlan_PlayerActionZeroCarriesBlockedPrivateStoreSelectionWithoutLiveMutation()
	{
		var packet = CreatePacket(0, [new CmBuyItemEntry(4, 1)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Player,
				PrivateStoreItems: []));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedPrivateStorePlanner, plan.Status);
		Assert.Equal(PrivateStoreBoughtItemsPlanStatus.BlockedInvalidStoreIndex, plan.PrivateStoreBoughtItemsPlan!.Status);
		Assert.Null(plan.PrivateStorePurchasePlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreatePlan_PlayerTargetNonPrivateStoreActionSkipsPrivateStorePlanner()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(1, 1)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(packet, PlayerPresent: true, TargetKind: CmBuyItemRunTargetKind.Player));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedPlayerTargetNonPrivateStoreAction, plan.Status);
		Assert.Contains(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch, plan.Steps);
		Assert.Null(plan.PrivateStoreBoughtItemsPlan);
		Assert.Null(plan.PrivateStorePurchasePlan);
	}

	[Fact]
	public void CreatePlan_NpcInteractionAuditWinsBeforeSwitchLikeJava()
	{
		var packet = CreatePacket(99, [new CmBuyItemEntry(1, 1)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				InteractionAllowed: false));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.RunAudit, plan.Status);
		Assert.Equal("might be abusing CM_BUY_ITEM: no right trading with npc", plan.AuditReason);
		Assert.DoesNotContain(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch, plan.Steps);
	}

	[Fact]
	public void CreatePlan_NpcUnknownActionReportsJavaDefaultBranchAfterInteraction()
	{
		var packet = CreatePacket(99, [new CmBuyItemEntry(1, 1)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(packet, PlayerPresent: true, TargetKind: CmBuyItemRunTargetKind.Npc));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedNpcUnsupportedAction, plan.Status);
		Assert.Contains(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch, plan.Steps);
		Assert.Contains("Unknown shop action", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_NpcActionEighteenIsNotBuyAgainRepurchaseInJavaCmBuyItem()
	{
		var packet = CreatePacket(18, [new CmBuyItemEntry(101, 1)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				RepurchasableItemObjectIds: new HashSet<int> { 101 }));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SkippedNpcUnsupportedAction, plan.Status);
		Assert.Null(plan.RepurchasePlan);
		Assert.Contains("Unknown shop action", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_SelectsPetSellToShopPlannerForMerchantActionSeventeen()
	{
		var packet = CreatePacket(17, [new CmBuyItemEntry(200, 1)]);
		var sellPlan = CreateTradeSellToShopPlan();

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Pet,
				PetHasMerchantFunction: true,
				PetSellModifier: 33,
				PetSellToShopPlan: sellPlan));

		Assert.Equal(CmBuyItemHandlerCompositionPlanStatus.SelectedPetSellToShopPlanner, plan.Status);
		Assert.Contains(CmBuyItemHandlerCompositionStep.InvokePetSellToShopPlanner, plan.Steps);
		Assert.DoesNotContain(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch, plan.Steps);
		Assert.Equal(33, plan.PetSellModifier);
		Assert.Same(sellPlan, plan.PetSellToShopPlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Null(plan.SellToShopPlan);
		Assert.Null(plan.RepurchasePlan);
		Assert.Null(plan.BuyFromShopPlan);
	}

	[Theory]
	[InlineData(17, false, CmBuyItemHandlerCompositionPlanStatus.SkippedPetWithoutMerchantFunction)]
	[InlineData(1, true, CmBuyItemHandlerCompositionPlanStatus.SkippedPetNonSellAction)]
	public void CreatePlan_ClassifiesPetBranchWithoutLivePetSellMutation(
		int actionId,
		bool petHasMerchantFunction,
		CmBuyItemHandlerCompositionPlanStatus expectedStatus)
	{
		var packet = CreatePacket(actionId, [new CmBuyItemEntry(200, 1)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Pet,
				PetHasMerchantFunction: petHasMerchantFunction,
				PetSellModifier: 33,
				PetSellToShopPlan: CreateTradeSellToShopPlan()));

		Assert.Equal(expectedStatus, plan.Status);
		Assert.Contains(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch, plan.Steps);
		Assert.Null(plan.PetSellModifier);
		Assert.Null(plan.PetSellToShopPlan);
		Assert.Null(plan.SellToShopPlan);
		Assert.Null(plan.RepurchasePlan);
		Assert.Null(plan.BuyFromShopPlan);
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

	private static PrivateStorePurchasePlan CreatePrivateStorePurchasePlan()
	{
		return new PrivateStorePurchasePlan(
			PrivateStorePurchasePlanStatus.PlanCreated,
			BoughtItems: [],
			SkippedMissingSellerItems: [],
			SellerItemUpdates: [],
			SellerDeletedItemObjectIds: [],
			BuyerAddedItems: [],
			BuyerUpdatedItems: [],
			BuyerKinahUpdate: null,
			SellerKinahUpdate: null,
			BuyerMessages: Array.Empty<SmSystemMessage>(),
			SellerMessages: Array.Empty<SmSystemMessage>(),
			WouldWriteAuditLog: false,
			AuditMessage: null,
			ShouldCloseSellerStore: false,
			"PrivateStoreService.sellStoreItem");
	}

	private static TradeSellToShopPlan CreateTradeSellToShopPlan()
	{
		return new TradeSellToShopPlan(
			TradeSellToShopPlanStatus.PlanCreated,
			SellerDeletedItemObjectIds: [],
			SellerItemUpdates: [],
			RepurchaseItems: [],
			KinahUpdate: null,
			"TradeService.performSellToShop");
	}

	private static TradeSellForApToShopPlan CreateTradeSellForApToShopPlan()
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
			DeletedItemObjectIds: [200],
			SkippedDeleteFailedItemObjectIds: [],
			AbyssPointRewards: [new TradeSellForApToShopApReward(200, ItemId: 100000001, Count: 1, RequiredApPerItem: 1_000, ApReward: 350)],
			TotalAbyssPoints: 350,
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performSellForAPToShop");
	}

	private const int SellerObjectId = 7001;
}
