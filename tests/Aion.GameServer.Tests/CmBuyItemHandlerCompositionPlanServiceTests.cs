using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
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

	[Theory]
	[InlineData(0, CmBuyItemHandlerCompositionPlanStatus.UnsupportedPrivateStorePlayerSale)]
	[InlineData(1, CmBuyItemHandlerCompositionPlanStatus.SkippedPlayerTargetNonPrivateStoreAction)]
	public void CreatePlan_ClassifiesPlayerTargetBranchWithoutLivePrivateStoreMutation(
		int actionId,
		CmBuyItemHandlerCompositionPlanStatus expectedStatus)
	{
		var packet = CreatePacket(actionId, [new CmBuyItemEntry(1, 1)]);

		var plan = CmBuyItemHandlerCompositionPlanService.CreatePlan(
			new CmBuyItemHandlerCompositionInput(packet, PlayerPresent: true, TargetKind: CmBuyItemRunTargetKind.Player));

		Assert.Equal(expectedStatus, plan.Status);
		Assert.Contains(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch, plan.Steps);
		Assert.Null(plan.SellToShopPlan);
		Assert.Null(plan.RepurchasePlan);
		Assert.Null(plan.BuyFromShopPlan);
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

	[Theory]
	[InlineData(17, true, CmBuyItemHandlerCompositionPlanStatus.UnsupportedPetSellToShop)]
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
				PetHasMerchantFunction: petHasMerchantFunction));

		Assert.Equal(expectedStatus, plan.Status);
		Assert.Contains(CmBuyItemHandlerCompositionStep.ClassifyUnsupportedBranch, plan.Steps);
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

	private const int SellerObjectId = 7001;
}
