using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed record QuestDialogNpcTargetBranchRuntimeSnapshot(
	int PlayerObjectId,
	int TargetObjectId,
	int DialogActionId,
	int LastPage,
	int QuestId,
	int ExtendedRewardIndex,
	bool DialogActionKnown = true,
	bool TargetExists = false,
	bool TargetIsCreature = false,
	bool TargetIsNpc = false,
	NpcTemplateSummary? TargetNpcTemplate = null,
	bool InteractionAllowed = true,
	NpcDialogInteractionAllowedInput? InteractionInput = null,
	QuestDialogNpcControllerDispatchFacts? ControllerDispatchFacts = null,
	NpcDialogTradeListFactAdapterInput? TradeListFactInput = null);

public sealed record QuestDialogNpcTargetBranchInputAssemblyPlan(
	QuestDialogNpcTargetBranchInput Input,
	QuestDialogNpcTargetBranchPlan BranchPlan,
	string JavaSource,
	NpcDialogInteractionAllowedPlan? InteractionPlan = null,
	NpcDialogTradeListFactAdapterPlan? TradeListFactAdapterPlan = null,
	NpcDialogControllerDispatchPlan? ControllerDispatchPlan = null,
	bool IsLive = false);

public sealed record QuestDialogNpcControllerDispatchFacts(
	bool IsInTalkRange = true,
	bool NpcAiHandledDialogSelect = false,
	NpcDialogServiceSelectFacts? DialogServiceFacts = null);

public static class QuestDialogNpcTargetBranchInputAssemblyPlanService
{
	public static QuestDialogNpcTargetBranchInputAssemblyPlan CreatePlan(
		QuestDialogNpcTargetBranchRuntimeSnapshot snapshot,
		NpcTemplateTable npcTemplates,
		TradeListTable? tradeLists = null,
		GoodsListTable? goodsLists = null)
	{
		ArgumentNullException.ThrowIfNull(npcTemplates);

		// Java parity breadcrumb: CM_DIALOG_SELECT.runImpl asks
		// DataManager.NPC_DATA.isFunctionDialog(dialogActionId) globally, then
		// checks the target NpcTemplate.supportsAction(dialogActionId) per NPC.
		// If provided, interaction facts are planned through DialogService.isInteractionAllowed.
		// If provided, controller dispatch facts are planned only after the branch reaches
		// target.getController().onDialogSelect(...).
		// If provided, trade-list facts are derived only for the Java DialogService fallback path.
		// This is still a non-live input adapter only; known-list, audits, packets,
		// and live controller dispatch remain explicit dependencies.
		var interactionPlan = snapshot.InteractionInput == null
			? null
			: NpcDialogInteractionAllowedPlanService.CreatePlan(snapshot.InteractionInput);
		var interactionAllowed = interactionPlan?.IsAllowed ?? snapshot.InteractionAllowed;
		var input = new QuestDialogNpcTargetBranchInput(
			snapshot.PlayerObjectId,
			snapshot.TargetObjectId,
			snapshot.DialogActionId,
			snapshot.LastPage,
			snapshot.QuestId,
			snapshot.ExtendedRewardIndex,
			snapshot.DialogActionKnown,
			snapshot.TargetExists,
			snapshot.TargetIsCreature,
			snapshot.TargetIsNpc,
			npcTemplates.IsFunctionDialog(snapshot.DialogActionId),
			snapshot.TargetNpcTemplate?.SupportsDialogAction(snapshot.DialogActionId) == true,
			interactionAllowed);
		var branchPlan = QuestDialogNpcTargetBranchPlanService.CreatePlan(input);
		var tradeListFactAdapterPlan = CreateTradeListFactAdapterPlan(snapshot, branchPlan, tradeLists, goodsLists);
		var controllerDispatchPlan = CreateControllerDispatchPlan(snapshot, branchPlan, tradeListFactAdapterPlan);

		return new QuestDialogNpcTargetBranchInputAssemblyPlan(
			input,
			branchPlan,
			"CM_DIALOG_SELECT.runImpl -> NpcData.isFunctionDialog + NpcTemplate.supportsAction + DialogService.isInteractionAllowed + target.getController().onDialogSelect",
			interactionPlan,
			tradeListFactAdapterPlan,
			controllerDispatchPlan,
			IsLive: false);
	}

	private static NpcDialogTradeListFactAdapterPlan? CreateTradeListFactAdapterPlan(
		QuestDialogNpcTargetBranchRuntimeSnapshot snapshot,
		QuestDialogNpcTargetBranchPlan branchPlan,
		TradeListTable? tradeLists,
		GoodsListTable? goodsLists)
	{
		if (branchPlan.Dispatch == null
			|| snapshot.ControllerDispatchFacts == null
			|| snapshot.ControllerDispatchFacts.DialogServiceFacts != null
			|| snapshot.TradeListFactInput == null
			|| tradeLists == null
			|| goodsLists == null
			|| !snapshot.TargetIsNpc
			|| !snapshot.ControllerDispatchFacts.IsInTalkRange
			|| snapshot.ControllerDispatchFacts.NpcAiHandledDialogSelect)
		{
			return null;
		}

		return NpcDialogTradeListFactAdapterService.CreatePlan(snapshot.TradeListFactInput, tradeLists, goodsLists);
	}

	private static NpcDialogControllerDispatchPlan? CreateControllerDispatchPlan(
		QuestDialogNpcTargetBranchRuntimeSnapshot snapshot,
		QuestDialogNpcTargetBranchPlan branchPlan,
		NpcDialogTradeListFactAdapterPlan? tradeListFactAdapterPlan)
	{
		if (snapshot.ControllerDispatchFacts == null || branchPlan.Dispatch == null)
		{
			return null;
		}

		var dialogServiceFacts = snapshot.ControllerDispatchFacts.DialogServiceFacts ?? tradeListFactAdapterPlan?.Facts;
		return NpcDialogControllerDispatchPlanService.CreatePlan(
			new NpcDialogControllerDispatchInput(
				branchPlan.Dispatch,
				snapshot.TargetIsNpc,
				snapshot.ControllerDispatchFacts.IsInTalkRange,
				snapshot.ControllerDispatchFacts.NpcAiHandledDialogSelect,
				dialogServiceFacts));
	}
}
