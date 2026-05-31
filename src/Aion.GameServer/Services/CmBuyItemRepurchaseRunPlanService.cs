namespace Aion.GameServer.Services;

public enum CmBuyItemRepurchaseRunPlanStatus
{
	WouldDispatchRepurchase,
	SkippedAudit,
	SkippedMissingPlayer,
	SkippedNonRepurchaseAction,
	SkippedUnknownTarget,
	SkippedNonNpcTarget,
	AuditInteractionNotAllowed,
	SkippedNpcCannotBuy,
}

public enum CmBuyItemRunTargetKind
{
	Unknown,
	Player,
	Npc,
	Pet,
	Other,
}

public sealed record CmBuyItemRepurchaseRunPlanInput(
	bool IsAudit,
	bool PlayerPresent,
	int SellerObjectId,
	int TradeActionId,
	CmBuyItemRunTargetKind TargetKind,
	bool InteractionAllowed = true,
	bool NpcCanBuy = true,
	CmBuyItemRepurchaseReadPlan? ReadPlan = null,
	RepurchasePlan? RepurchasePlan = null);

public sealed record CmBuyItemRepurchaseDispatchDescriptor(
	int SellerObjectId,
	IReadOnlyList<int> RequestedItemObjectIds,
	RepurchasePlan? RepurchasePlan,
	string JavaSource,
	bool IsLive = false);

public sealed record CmBuyItemRepurchaseRunPlan(
	CmBuyItemRepurchaseRunPlanStatus Status,
	string JavaSource,
	bool IsLive,
	CmBuyItemRepurchaseDispatchDescriptor? Dispatch = null,
	string? AuditReason = null);

public static class CmBuyItemRepurchaseRunPlanService
{
	public static CmBuyItemRepurchaseRunPlan CreatePlan(CmBuyItemRepurchaseRunPlanInput input)
	{
		// Java parity: network/aion/clientpackets/CM_BUY_ITEM.runImpl action 2.
		// This planner models only run-side dispatch gating; it does not perform
		// known-list lookup, DialogService calls, socket sends, audits, or inventory mutation.
		if (input.IsAudit || input.ReadPlan?.Status is
			CmBuyItemRepurchaseReadPlanStatus.AuditAmountOutOfRange or CmBuyItemRepurchaseReadPlanStatus.AuditInvalidItem)
		{
			return NotPlanned(
				CmBuyItemRepurchaseRunPlanStatus.SkippedAudit,
				"CM_BUY_ITEM.runImpl -> if (isAudit || player == null) return");
		}

		if (!input.PlayerPresent)
		{
			return NotPlanned(
				CmBuyItemRepurchaseRunPlanStatus.SkippedMissingPlayer,
				"CM_BUY_ITEM.runImpl -> if (isAudit || player == null) return");
		}

		if (input.TradeActionId != CmBuyItemRepurchaseReadPlanService.RepurchaseTradeActionId)
		{
			return NotPlanned(
				CmBuyItemRepurchaseRunPlanStatus.SkippedNonRepurchaseAction,
				"CM_BUY_ITEM.runImpl -> action handled by non-repurchase branch");
		}

		if (input.TargetKind == CmBuyItemRunTargetKind.Unknown)
		{
			return NotPlanned(
				CmBuyItemRepurchaseRunPlanStatus.SkippedUnknownTarget,
				"CM_BUY_ITEM.runImpl -> player.getKnownList().getObject(sellerObjId) == null -> return");
		}

		if (input.TargetKind != CmBuyItemRunTargetKind.Npc)
		{
			return NotPlanned(
				CmBuyItemRepurchaseRunPlanStatus.SkippedNonNpcTarget,
				"CM_BUY_ITEM.runImpl -> action 2 dispatch exists only inside target instanceof Npc branch");
		}

		if (!input.InteractionAllowed)
		{
			return NotPlanned(
				CmBuyItemRepurchaseRunPlanStatus.AuditInteractionNotAllowed,
				"CM_BUY_ITEM.runImpl -> !DialogService.isInteractionAllowed(player, npc) -> audit and return",
				"might be abusing CM_BUY_ITEM: no right trading with npc");
		}

		if (!input.NpcCanBuy)
		{
			return NotPlanned(
				CmBuyItemRepurchaseRunPlanStatus.SkippedNpcCannotBuy,
				"CM_BUY_ITEM.runImpl action 2 -> if (npc.canBuy()) RepurchaseService.repurchaseFromShop");
		}

		var requestedIds = input.ReadPlan?.RepurchaseItemObjectIds ?? Array.Empty<int>();
		return new CmBuyItemRepurchaseRunPlan(
			CmBuyItemRepurchaseRunPlanStatus.WouldDispatchRepurchase,
			"CM_BUY_ITEM.runImpl action 2 -> RepurchaseService.getInstance().repurchaseFromShop(player, repurchaseList)",
			IsLive: false,
			new CmBuyItemRepurchaseDispatchDescriptor(
				input.SellerObjectId,
				requestedIds,
				input.RepurchasePlan,
				"RepurchaseService.repurchaseFromShop(player, repurchaseList)",
				IsLive: false));
	}

	private static CmBuyItemRepurchaseRunPlan NotPlanned(
		CmBuyItemRepurchaseRunPlanStatus status,
		string javaSource,
		string? auditReason = null)
	{
		return new CmBuyItemRepurchaseRunPlan(
			status,
			javaSource,
			IsLive: false,
			Dispatch: null,
			auditReason);
	}
}
