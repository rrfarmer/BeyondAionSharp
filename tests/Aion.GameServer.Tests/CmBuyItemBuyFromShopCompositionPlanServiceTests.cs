using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemBuyFromShopCompositionPlanServiceTests
{
	[Theory]
	[InlineData(13)]
	[InlineData(14)]
	[InlineData(15)]
	[InlineData(16)]
	public void CreatePlan_ChainsBuyFromShopActionsToDispatchDescriptor(int tradeActionId)
	{
		var packet = CreatePacket(tradeActionId, [new CmBuyItemEntry(100000001, 1), new CmBuyItemEntry(100000002, 2)]);
		var tradeTemplate = new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL");

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				TradeTemplate: tradeTemplate,
				NpcCanSell: true));

		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.WouldDispatchBuyFromShop, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Equal([100000001, 100000002], plan.TradeItems.Select(item => item.ItemId).ToArray());
		Assert.Contains(CmBuyItemBuyFromShopCompositionStep.ClassifyTradeNpcType, plan.Steps);

		var dispatch = Assert.IsType<CmBuyItemBuyFromShopDispatchDescriptor>(plan.Dispatch);
		Assert.Equal(tradeActionId, dispatch.TradeActionId);
		Assert.True(dispatch.UseKinah);
		Assert.Same(tradeTemplate, dispatch.TradeTemplate);
		Assert.Null(dispatch.BuyTransactionPlan);
		Assert.False(dispatch.IsLive);
	}

	[Fact]
	public void CreatePlan_CarriesOptionalBuyTransactionPlanToDispatchDescriptor()
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]);
		var tradeTemplate = new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL");
		var buyTransactionPlan = new TradeBuyTransactionPlan(
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
			RequiredAbyssPoints: 0,
			RequiredItems: [],
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performBuyTransaction");

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				TradeTemplate: tradeTemplate,
				BuyTransactionPlan: buyTransactionPlan));

		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.WouldDispatchBuyFromShop, plan.Status);
		Assert.Contains(CmBuyItemBuyFromShopCompositionStep.AttachBuyTransactionPlan, plan.Steps);
		var dispatch = Assert.IsType<CmBuyItemBuyFromShopDispatchDescriptor>(plan.Dispatch);
		Assert.Same(buyTransactionPlan, dispatch.BuyTransactionPlan);
		Assert.False(dispatch.BuyTransactionPlan!.IsLive);
	}

	[Theory]
	[InlineData("NORMAL", true)]
	[InlineData("ABYSS_KINAH", true)]
	[InlineData("ABYSS", false)]
	[InlineData("REWARD", false)]
	public void CreatePlan_MapsTradeNpcTypeToJavaUseKinahBranch(string npcType, bool expectedUseKinah)
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]);

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				TradeTemplate: new TradeListTemplateSummary(203060, [129], NpcType: npcType)));

		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.WouldDispatchBuyFromShop, plan.Status);
		Assert.Equal(expectedUseKinah, plan.Dispatch!.UseKinah);
	}

	[Fact]
	public void CreatePlan_UnknownTradeNpcTypeReportsServiceDefaultBranch()
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]);

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				TradeTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "CUSTOM")));

		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.UnknownTradeNpcType, plan.Status);
		Assert.Null(plan.Dispatch);
		Assert.Contains(CmBuyItemBuyFromShopCompositionStep.ClassifyTradeNpcType, plan.Steps);
	}

	[Fact]
	public void CreatePlan_UnknownTradeNpcTypeDoesNotAttachBuyTransactionPayload()
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]);
		var buyTransactionPlan = new TradeBuyTransactionPlan(
			TradeBuyTransactionPlanStatus.WouldApplyBuyTransaction,
			[TradeBuyTransactionStep.CheckPlayerCanTrade],
			RequiredKinah: 0,
			RequiredAbyssPoints: 0,
			RequiredItems: [],
			ShouldDispatchLiveSideEffects: false,
			"TradeService.performBuyTransaction");

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				TradeTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "CUSTOM"),
				BuyTransactionPlan: buyTransactionPlan));

		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.UnknownTradeNpcType, plan.Status);
		Assert.Null(plan.Dispatch);
		Assert.DoesNotContain(CmBuyItemBuyFromShopCompositionStep.AttachBuyTransactionPlan, plan.Steps);
	}

	[Fact]
	public void CreatePlan_ParserAuditStopsBeforeRunDispatch()
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1), new CmBuyItemEntry(0, 1)]);

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				TradeTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL")));

		Assert.True(packet.IsAudit);
		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.ReadAudit, plan.Status);
		Assert.Equal([100000001], plan.TradeItems.Select(item => item.ItemId).ToArray());
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_NonBuyFromShopActionSkipsBeforeTargetDispatch()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(100000001, 1)]);

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				TradeTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL")));

		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.SkippedNonBuyFromShopAction, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Theory]
	[InlineData(CmBuyItemRunTargetKind.Unknown, CmBuyItemBuyFromShopCompositionPlanStatus.SkippedUnknownTarget)]
	[InlineData(CmBuyItemRunTargetKind.Player, CmBuyItemBuyFromShopCompositionPlanStatus.SkippedNonNpcTarget)]
	[InlineData(CmBuyItemRunTargetKind.Pet, CmBuyItemBuyFromShopCompositionPlanStatus.SkippedNonNpcTarget)]
	public void CreatePlan_AppliesTargetBranchBeforeNpcGates(
		CmBuyItemRunTargetKind targetKind,
		CmBuyItemBuyFromShopCompositionPlanStatus expectedStatus)
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]);

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: targetKind,
				TradeTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL"),
				InteractionAllowed: false,
				NpcCanSell: false));

		Assert.Equal(expectedStatus, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_InteractionAuditWinsBeforeNpcCanSell()
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]);

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				TradeTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL"),
				InteractionAllowed: false,
				NpcCanSell: false));

		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.RunAudit, plan.Status);
		Assert.Equal("might be abusing CM_BUY_ITEM: no right trading with npc", plan.AuditReason);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_SkipsNpcThatCannotSell()
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]);

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				TradeTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL"),
				NpcCanSell: false));

		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.SkippedNpcCannotSell, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_MissingPlayerSkipsLikeJavaRunImpl()
	{
		var packet = CreatePacket(13, [new CmBuyItemEntry(100000001, 1)]);

		var plan = CmBuyItemBuyFromShopCompositionPlanService.CreatePlan(
			new CmBuyItemBuyFromShopCompositionInput(
				packet,
				PlayerPresent: false,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				TradeTemplate: new TradeListTemplateSummary(203060, [129], NpcType: "NORMAL")));

		Assert.Equal(CmBuyItemBuyFromShopCompositionPlanStatus.SkippedMissingPlayer, plan.Status);
		Assert.Null(plan.Dispatch);
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
