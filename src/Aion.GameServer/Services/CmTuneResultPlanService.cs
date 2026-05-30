using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum CmTuneResultPlanStatus
{
	NoTargetItem,
	Accepted,
	AcceptedWithoutPendingResultAudited,
	AttributeOnlyCancelForcedApply,
	Cancelled,
}

public sealed record CmTuneResultPlan(
	CmTuneResultPlanStatus Status,
	TuneResultApplicationPlan? ApplicationPlan,
	InventoryItem? ResultingTargetItem,
	SmSystemMessage? ResponseMessage,
	SmInventoryUpdateItem? InventoryUpdatePacket,
	string? AuditMessage,
	string JavaSource,
	bool IsLive = false);

public static class CmTuneResultPlanService
{
	public static CmTuneResultPlan CreatePlan(
		InventoryItem? targetItem,
		ItemTemplateSummary? targetTemplate,
		PendingTuneResult? pendingResult,
		bool hasAccepted,
		string targetItemName)
	{
		// Java parity: network/aion/clientpackets/CM_TUNE_RESULT.runImpl.
		if (targetItem == null || targetTemplate == null)
		{
			return new CmTuneResultPlan(
				CmTuneResultPlanStatus.NoTargetItem,
				ApplicationPlan: null,
				ResultingTargetItem: null,
				ResponseMessage: null,
				InventoryUpdatePacket: null,
				AuditMessage: null,
				JavaSource: "CM_TUNE_RESULT.runImpl -> item lookup by object id returned null -> return");
		}

		var auditInvalidEvent = !hasAccepted && pendingResult?.IsAttributeOnly == true;
		if (hasAccepted || auditInvalidEvent)
		{
			var applicationPlan = TuneResultApplicationPlanService.CreatePlan(targetItem, pendingResult);
			var status = auditInvalidEvent
				? CmTuneResultPlanStatus.AttributeOnlyCancelForcedApply
				: applicationPlan.Status == TuneResultApplicationPlanStatus.MissingPendingResultAudited
					? CmTuneResultPlanStatus.AcceptedWithoutPendingResultAudited
					: CmTuneResultPlanStatus.Accepted;
			var auditMessage = auditInvalidEvent
				? "tried to cancel a attribute re-identification which is not possible by default"
				: applicationPlan.AuditMessage;
			var javaSource = auditInvalidEvent
				? "CM_TUNE_RESULT.runImpl -> !hasAccepted && pendingTuneResult.isAttributeOnly() -> AuditLogger.log(...), applyTuneResult(...), STR_MSG_ITEM_REIDENTIFY_APPLY_YES, SM_INVENTORY_UPDATE_ITEM"
				: hasAccepted
					? "CM_TUNE_RESULT.runImpl -> hasAccepted -> applyTuneResult(...), STR_MSG_ITEM_REIDENTIFY_APPLY_YES, SM_INVENTORY_UPDATE_ITEM"
					: "CM_TUNE_RESULT.runImpl -> accepted branch fell through unexpectedly";

			return new CmTuneResultPlan(
				status,
				applicationPlan,
				applicationPlan.ResultingTargetItem,
				SmSystemMessage.ItemReidentifyApplyYes(targetItemName),
				new SmInventoryUpdateItem(applicationPlan.ResultingTargetItem, targetTemplate, SmInventoryUpdateItem.DecreaseItemUse),
				auditMessage,
				javaSource);
		}

		return new CmTuneResultPlan(
			CmTuneResultPlanStatus.Cancelled,
			ApplicationPlan: null,
			ResultingTargetItem: targetItem,
			ResponseMessage: SmSystemMessage.ItemReidentifyApplyNo(),
			InventoryUpdatePacket: new SmInventoryUpdateItem(targetItem, targetTemplate, SmInventoryUpdateItem.DecreaseItemUse),
			AuditMessage: null,
			JavaSource: "CM_TUNE_RESULT.runImpl -> !hasAccepted -> clear pending result, STR_MSG_ITEM_REIDENTIFY_APPLY_NO, SM_INVENTORY_UPDATE_ITEM");
	}
}
