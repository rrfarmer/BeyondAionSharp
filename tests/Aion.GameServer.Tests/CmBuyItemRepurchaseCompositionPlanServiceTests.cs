using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemRepurchaseCompositionPlanServiceTests
{
	[Fact]
	public void CreatePlan_ChainsParsedPacketThroughReadAndRunPlansWithoutLiveSideEffects()
	{
		var packet = CreatePacket(2, [new CmBuyItemEntry(101, 1), new CmBuyItemEntry(999, 1), new CmBuyItemEntry(102, 5)]);

		var plan = CmBuyItemRepurchaseCompositionPlanService.CreatePlan(
			new CmBuyItemRepurchaseCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				RepurchasableItemObjectIds: new HashSet<int> { 101, 102 },
				InteractionAllowed: true,
				NpcCanBuy: true));

		Assert.Equal(CmBuyItemRepurchaseCompositionPlanStatus.WouldDispatchRepurchase, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Equal(
			[
				CmBuyItemRepurchaseCompositionStep.ReadParsedClientPacketValues,
				CmBuyItemRepurchaseCompositionStep.CreateRepurchaseReadPlan,
				CmBuyItemRepurchaseCompositionStep.CreateRepurchaseRunPlan,
			],
			plan.Steps);
		Assert.Same(packet, plan.Packet);
		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.PlanCreated, plan.ReadPlan.Status);
		Assert.Equal([101, 102], plan.ReadPlan.RepurchaseItemObjectIds);
		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.WouldDispatchRepurchase, plan.RunPlan.Status);
		Assert.Equal([101, 102], plan.RunPlan.Dispatch!.RequestedItemObjectIds);
	}

	[Fact]
	public void CreatePlan_PreservesInvalidParserAuditThroughReadAndRunPlans()
	{
		var packet = CreatePacket(2, [new CmBuyItemEntry(101, 1), new CmBuyItemEntry(0, 1)]);

		var plan = CmBuyItemRepurchaseCompositionPlanService.CreatePlan(
			new CmBuyItemRepurchaseCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				RepurchasableItemObjectIds: new HashSet<int> { 101 }));

		Assert.True(packet.IsAudit);
		Assert.Equal(CmBuyItemRepurchaseCompositionPlanStatus.ReadAudit, plan.Status);
		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.AuditInvalidItem, plan.ReadPlan.Status);
		Assert.Equal(new CmBuyItemReadItem(0, 1), plan.ReadPlan.AuditItem);
		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.SkippedAudit, plan.RunPlan.Status);
		Assert.Null(plan.RunPlan.Dispatch);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreatePlan_AmountAuditStopsBeforeDispatch()
	{
		var packet = CreatePacketWithAmount(2, declaredAmount: 37);

		var plan = CmBuyItemRepurchaseCompositionPlanService.CreatePlan(
			new CmBuyItemRepurchaseCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				RepurchasableItemObjectIds: new HashSet<int> { 101 }));

		Assert.Equal(CmBuyItemRepurchaseCompositionPlanStatus.ReadAudit, plan.Status);
		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.AuditAmountOutOfRange, plan.ReadPlan.Status);
		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.SkippedAudit, plan.RunPlan.Status);
		Assert.Empty(plan.ReadPlan.RepurchaseItemObjectIds);
	}

	[Fact]
	public void CreatePlan_NonRepurchaseActionRunsThroughSkipWithoutRepurchaseDispatch()
	{
		var packet = CreatePacket(1, [new CmBuyItemEntry(101, 1)]);

		var plan = CmBuyItemRepurchaseCompositionPlanService.CreatePlan(
			new CmBuyItemRepurchaseCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				RepurchasableItemObjectIds: new HashSet<int> { 101 }));

		Assert.Equal(CmBuyItemRepurchaseCompositionPlanStatus.RunSkipped, plan.Status);
		Assert.Equal(CmBuyItemRepurchaseReadPlanStatus.PlanCreated, plan.ReadPlan.Status);
		Assert.False(plan.ReadPlan.IsRepurchaseAction);
		Assert.Empty(plan.ReadPlan.RepurchaseItemObjectIds);
		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.SkippedNonRepurchaseAction, plan.RunPlan.Status);
	}

	[Fact]
	public void CreatePlan_InteractionAuditWinsBeforeNpcCanBuyLikeJavaRunImpl()
	{
		var packet = CreatePacket(2, [new CmBuyItemEntry(101, 1)]);

		var plan = CmBuyItemRepurchaseCompositionPlanService.CreatePlan(
			new CmBuyItemRepurchaseCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				RepurchasableItemObjectIds: new HashSet<int> { 101 },
				InteractionAllowed: false,
				NpcCanBuy: false));

		Assert.Equal(CmBuyItemRepurchaseCompositionPlanStatus.RunAudit, plan.Status);
		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.AuditInteractionNotAllowed, plan.RunPlan.Status);
		Assert.Equal("might be abusing CM_BUY_ITEM: no right trading with npc", plan.RunPlan.AuditReason);
		Assert.Null(plan.RunPlan.Dispatch);
	}

	[Fact]
	public void CreatePlan_CarriesOptionalRepurchasePlanToDispatchDescriptor()
	{
		var packet = CreatePacket(2, [new CmBuyItemEntry(101, 1)]);
		var repurchasePlan = new RepurchasePlan(
			RepurchasePlanStatus.PlanCreated,
			RequestedItemObjectIds: [101],
			RepurchasedItemObjectIds: [101],
			MissingRepurchaseItemObjectIds: [],
			InsufficientKinahItemObjectIds: [],
			AddedItems: [],
			UpdatedItems: [],
			KinahUpdate: null,
			RemovedRepurchaseItemObjectIds: [101],
			Messages: [],
			AuditMessages: [],
			"RepurchaseService.repurchaseFromShop");

		var plan = CmBuyItemRepurchaseCompositionPlanService.CreatePlan(
			new CmBuyItemRepurchaseCompositionInput(
				packet,
				PlayerPresent: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				RepurchasableItemObjectIds: new HashSet<int> { 101 },
				RepurchasePlan: repurchasePlan));

		Assert.Equal(CmBuyItemRepurchaseCompositionPlanStatus.WouldDispatchRepurchase, plan.Status);
		Assert.Same(repurchasePlan, plan.RunPlan.Dispatch!.RepurchasePlan);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
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

	private static CmBuyItem CreatePacketWithAmount(int tradeActionId, int declaredAmount)
	{
		using var buffer = new PacketBuffer();
		buffer.WriteD(SellerObjectId);
		buffer.WriteH(tradeActionId);
		buffer.WriteH(declaredAmount);
		buffer.WriteD(101);
		buffer.WriteQ(1);

		var packet = new CmBuyItem(51, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private const int SellerObjectId = 7001;
}
