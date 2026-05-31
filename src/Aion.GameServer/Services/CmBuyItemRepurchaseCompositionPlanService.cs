using Aion.GameServer.Network.Aion.ClientPackets;

namespace Aion.GameServer.Services;

public enum CmBuyItemRepurchaseCompositionPlanStatus
{
	WouldDispatchRepurchase,
	ReadAudit,
	RunSkipped,
	RunAudit,
}

public enum CmBuyItemRepurchaseCompositionStep
{
	ReadParsedClientPacketValues,
	CreateRepurchaseReadPlan,
	CreateRepurchaseRunPlan,
}

public sealed record CmBuyItemRepurchaseCompositionInput(
	CmBuyItem Packet,
	bool PlayerPresent,
	CmBuyItemRunTargetKind TargetKind,
	IReadOnlySet<int> RepurchasableItemObjectIds,
	bool InteractionAllowed = true,
	bool NpcCanBuy = true,
	RepurchasePlan? RepurchasePlan = null);

public sealed record CmBuyItemRepurchaseCompositionPlan(
	CmBuyItemRepurchaseCompositionPlanStatus Status,
	CmBuyItem Packet,
	CmBuyItemRepurchaseReadPlan ReadPlan,
	CmBuyItemRepurchaseRunPlan RunPlan,
	IReadOnlyList<CmBuyItemRepurchaseCompositionStep> Steps,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class CmBuyItemRepurchaseCompositionPlanService
{
	public static CmBuyItemRepurchaseCompositionPlan CreatePlan(CmBuyItemRepurchaseCompositionInput input)
	{
		// Java parity: CM_BUY_ITEM readImpl prepares a RepurchaseList, then runImpl
		// applies target/NPC gates before RepurchaseService.repurchaseFromShop.
		// This composition is intentionally non-live and only chains planner outputs.
		var readItems = input.Packet.Items
			.Select(item => new CmBuyItemReadItem(item.ItemObjectId, item.Count))
			.ToList();
		if (input.Packet.AuditItem is { } auditItem)
			readItems.Add(new CmBuyItemReadItem(auditItem.ItemObjectId, auditItem.Count));

		var readPlan = CmBuyItemRepurchaseReadPlanService.CreatePlan(
			input.Packet.SellerObjectId,
			input.Packet.TradeActionId,
			input.Packet.Amount,
			readItems,
			input.RepurchasableItemObjectIds);

		var runPlan = CmBuyItemRepurchaseRunPlanService.CreatePlan(
			new CmBuyItemRepurchaseRunPlanInput(
				input.Packet.IsAudit,
				input.PlayerPresent,
				input.Packet.SellerObjectId,
				input.Packet.TradeActionId,
				input.TargetKind,
				input.InteractionAllowed,
				input.NpcCanBuy,
				readPlan,
				input.RepurchasePlan));

		return new CmBuyItemRepurchaseCompositionPlan(
			MapStatus(readPlan, runPlan),
			input.Packet,
			readPlan,
			runPlan,
			[
				CmBuyItemRepurchaseCompositionStep.ReadParsedClientPacketValues,
				CmBuyItemRepurchaseCompositionStep.CreateRepurchaseReadPlan,
				CmBuyItemRepurchaseCompositionStep.CreateRepurchaseRunPlan,
			],
			ShouldDispatchLiveSideEffects: false,
			"CM_BUY_ITEM.readImpl -> RepurchaseList; CM_BUY_ITEM.runImpl action 2 -> RepurchaseService.repurchaseFromShop");
	}

	private static CmBuyItemRepurchaseCompositionPlanStatus MapStatus(
		CmBuyItemRepurchaseReadPlan readPlan,
		CmBuyItemRepurchaseRunPlan runPlan)
	{
		if (readPlan.Status != CmBuyItemRepurchaseReadPlanStatus.PlanCreated)
			return CmBuyItemRepurchaseCompositionPlanStatus.ReadAudit;
		return runPlan.Status switch
		{
			CmBuyItemRepurchaseRunPlanStatus.WouldDispatchRepurchase => CmBuyItemRepurchaseCompositionPlanStatus.WouldDispatchRepurchase,
			CmBuyItemRepurchaseRunPlanStatus.AuditInteractionNotAllowed => CmBuyItemRepurchaseCompositionPlanStatus.RunAudit,
			_ => CmBuyItemRepurchaseCompositionPlanStatus.RunSkipped,
		};
	}
}
