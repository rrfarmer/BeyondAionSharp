using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmBuyItemRepurchaseRunPlanServiceTests
{
	[Fact]
	public void CreatePlan_WouldDispatchRepurchaseForNpcActionTwoAfterJavaGates()
	{
		var readPlan = ReadPlan([101, 102]);

		var plan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			Input(
				TargetKind: CmBuyItemRunTargetKind.Npc,
				ReadPlan: readPlan));

		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.WouldDispatchRepurchase, plan.Status);
		Assert.False(plan.IsLive);
		Assert.Null(plan.AuditReason);

		var dispatch = Assert.IsType<CmBuyItemRepurchaseDispatchDescriptor>(plan.Dispatch);
		Assert.False(dispatch.IsLive);
		Assert.Equal(SellerObjectId, dispatch.SellerObjectId);
		Assert.Equal([101, 102], dispatch.RequestedItemObjectIds);
		Assert.Null(dispatch.RepurchasePlan);
	}

	[Fact]
	public void CreatePlan_CarriesExistingRepurchasePlanAsWouldDispatchPayload()
	{
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

		var plan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			Input(
				TargetKind: CmBuyItemRunTargetKind.Npc,
				ReadPlan: ReadPlan([101]),
				RepurchasePlan: repurchasePlan));

		var dispatch = Assert.IsType<CmBuyItemRepurchaseDispatchDescriptor>(plan.Dispatch);
		Assert.Same(repurchasePlan, dispatch.RepurchasePlan);
		Assert.Equal([101], dispatch.RequestedItemObjectIds);
	}

	[Fact]
	public void CreatePlan_SkipsWhenPacketWasAudited()
	{
		var plan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			Input(
				IsAudit: true,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				ReadPlan: ReadPlan([101])));

		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.SkippedAudit, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_SkipsWhenReadPlanAudited()
	{
		var auditedRead = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			SellerObjectId,
			CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId,
			declaredAmount: 37,
			readItems: [],
			repurchasableItemObjectIds: new HashSet<int>());

		var plan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			Input(
				TargetKind: CmBuyItemRunTargetKind.Npc,
				ReadPlan: auditedRead));

		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.SkippedAudit, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_SkipsWhenPlayerMissing()
	{
		var plan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			Input(
				PlayerPresent: false,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				ReadPlan: ReadPlan([101])));

		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.SkippedMissingPlayer, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_SkipsNonRepurchaseAction()
	{
		var plan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			Input(
				TradeActionId: 1,
				TargetKind: CmBuyItemRunTargetKind.Npc,
				ReadPlan: ReadPlan([101])));

		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.SkippedNonRepurchaseAction, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_SkipsUnknownKnownListTarget()
	{
		var plan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			Input(
				TargetKind: CmBuyItemRunTargetKind.Unknown,
				ReadPlan: ReadPlan([101])));

		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.SkippedUnknownTarget, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Theory]
	[InlineData(CmBuyItemRunTargetKind.Player)]
	[InlineData(CmBuyItemRunTargetKind.Pet)]
	[InlineData(CmBuyItemRunTargetKind.Other)]
	public void CreatePlan_SkipsNonNpcTargetsForActionTwo(CmBuyItemRunTargetKind targetKind)
	{
		var plan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			Input(
				TargetKind: targetKind,
				ReadPlan: ReadPlan([101])));

		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.SkippedNonNpcTarget, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_AuditsInteractionNotAllowedBeforeNpcCanBuyGate()
	{
		var plan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			Input(
				TargetKind: CmBuyItemRunTargetKind.Npc,
				InteractionAllowed: false,
				NpcCanBuy: false,
				ReadPlan: ReadPlan([101])));

		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.AuditInteractionNotAllowed, plan.Status);
		Assert.Equal("might be abusing CM_BUY_ITEM: no right trading with npc", plan.AuditReason);
		Assert.Null(plan.Dispatch);
	}

	[Fact]
	public void CreatePlan_SkipsNpcThatCannotBuy()
	{
		var plan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			Input(
				TargetKind: CmBuyItemRunTargetKind.Npc,
				NpcCanBuy: false,
				ReadPlan: ReadPlan([101])));

		Assert.Equal(CmBuyItemRepurchaseRunPlanStatus.SkippedNpcCannotBuy, plan.Status);
		Assert.Null(plan.Dispatch);
	}

	private static CmBuyItemRepurchaseRunPlanInput Input(
		bool IsAudit = false,
		bool PlayerPresent = true,
		int TradeActionId = CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId,
		CmBuyItemRunTargetKind TargetKind = CmBuyItemRunTargetKind.Unknown,
		bool InteractionAllowed = true,
		bool NpcCanBuy = true,
		CmBuyItemRepurchaseReadPlan? ReadPlan = null,
		RepurchasePlan? RepurchasePlan = null)
	{
		return new CmBuyItemRepurchaseRunPlanInput(
			IsAudit,
			PlayerPresent,
			SellerObjectId,
			TradeActionId,
			TargetKind,
			InteractionAllowed,
			NpcCanBuy,
			ReadPlan,
			RepurchasePlan);
	}

	private static CmBuyItemRepurchaseReadPlan ReadPlan(IReadOnlyList<int> repurchaseItemObjectIds)
	{
		return new CmBuyItemRepurchaseReadPlan(
			CmBuyItemRepurchaseReadPlanStatus.PlanCreated,
			SellerObjectId,
			CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId,
			DeclaredAmount: repurchaseItemObjectIds.Count,
			ProcessedItems: repurchaseItemObjectIds.Select(itemObjectId => new CmBuyItemReadItem(itemObjectId, Count: 1)).ToArray(),
			RepurchaseItemObjectIds: repurchaseItemObjectIds,
			AuditItem: null,
			"CM_BUY_ITEM.readImpl action 2");
	}

	private const int SellerObjectId = 7001;
}
