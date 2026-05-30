using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public enum CmTuneRuntimePlanStatus
{
	NoTargetItem,
	IdentifyTargetItem,
	MissingTuningScroll,
	MissingTuningAction,
	GuardBlocked,
	ExecuteTuning,
	AuditAlreadyIdentifiedWithoutScroll,
}

public sealed record CmTuneResolvedTuningAction(
	InventoryItem TuningScrollItem,
	ItemTemplateSummary TuningScrollTemplate,
	InventoryItem TargetItem,
	ItemTemplateSummary TargetTemplate,
	TuningActionTargetType TargetType,
	bool ShouldNotReduceTuneCount,
	TuningActionGuardPlan GuardPlan);

public sealed record CmTuneRuntimePlan(
	CmTuneRuntimePlanStatus Status,
	CmTuneResolvedTuningAction? ResolvedAction,
	TuningActionGuardPlan? GuardPlan,
	string? AuditMessage,
	string JavaSource,
	bool IsLive = false);

public static class CmTuneRuntimePlanService
{
	public static CmTuneRuntimePlan CreatePlan(
		InventoryItem? targetItem,
		ItemTemplateSummary? targetTemplate,
		int tuningScrollObjectId,
		InventoryItem? tuningScrollItem,
		ItemTemplateSummary? tuningScrollTemplate,
		string tuningScrollName,
		string targetItemName)
	{
		// Java parity: network/aion/clientpackets/CM_TUNE.runImpl.
		if (targetItem == null || targetTemplate == null)
		{
			return new CmTuneRuntimePlan(
				CmTuneRuntimePlanStatus.NoTargetItem,
				ResolvedAction: null,
				GuardPlan: null,
				AuditMessage: null,
				JavaSource: "CM_TUNE.runImpl -> item lookup by object id returned null -> return");
		}

		if (!targetItem.IsIdentified)
		{
			return new CmTuneRuntimePlan(
				CmTuneRuntimePlanStatus.IdentifyTargetItem,
				ResolvedAction: null,
				GuardPlan: null,
				AuditMessage: null,
				JavaSource: "CM_TUNE.runImpl -> !item.isIdentified() -> ItemActionService.identifyItem(player, item)");
		}

		if (tuningScrollObjectId == 0)
		{
			return new CmTuneRuntimePlan(
				CmTuneRuntimePlanStatus.AuditAlreadyIdentifiedWithoutScroll,
				ResolvedAction: null,
				GuardPlan: null,
				AuditMessage: "attempted to tune an already identified item without tuning scroll.",
				JavaSource: "CM_TUNE.runImpl -> identified target + tuningScrollObjectId == 0 -> AuditLogger.log(...)");
		}

		if (tuningScrollItem == null || tuningScrollTemplate == null)
		{
			return new CmTuneRuntimePlan(
				CmTuneRuntimePlanStatus.MissingTuningScroll,
				ResolvedAction: null,
				GuardPlan: null,
				AuditMessage: null,
				JavaSource: "CM_TUNE.runImpl -> tuningScroll lookup by object id returned null -> return");
		}

		if (tuningScrollTemplate.TuningAction == null)
		{
			return new CmTuneRuntimePlan(
				CmTuneRuntimePlanStatus.MissingTuningAction,
				ResolvedAction: null,
				GuardPlan: null,
				AuditMessage: null,
				JavaSource: "CM_TUNE.runImpl -> tuningScroll.getItemTemplate().getActions().getTuningAction() returned null -> return");
		}

		var tuningAction = tuningScrollTemplate.TuningAction;
		var targetType = ToTargetType(tuningAction.Target);
		var guardPlan = TuningActionGuardPlanService.CreatePlan(
			tuningScrollItem,
			tuningScrollTemplate,
			targetItem,
			targetTemplate,
			targetType,
			tuningAction.ShouldNotReduceTuneCount,
			tuningScrollName,
			targetItemName);
		if (!guardPlan.CanAct)
		{
			return new CmTuneRuntimePlan(
				CmTuneRuntimePlanStatus.GuardBlocked,
				ResolvedAction: null,
				GuardPlan: guardPlan,
				AuditMessage: null,
				JavaSource: "CM_TUNE.runImpl -> action.canAct(player, tuningScroll, item) returned false -> return");
		}

		return new CmTuneRuntimePlan(
			CmTuneRuntimePlanStatus.ExecuteTuning,
			new CmTuneResolvedTuningAction(
				tuningScrollItem,
				tuningScrollTemplate,
				targetItem,
				targetTemplate,
				targetType,
				tuningAction.ShouldNotReduceTuneCount,
				guardPlan),
			GuardPlan: guardPlan,
			AuditMessage: null,
			JavaSource: "CM_TUNE.runImpl -> action != null && action.canAct(...) -> action.act(player, tuningScroll, item)");
	}

	private static TuningActionTargetType ToTargetType(ItemActionUseTargetType targetType) =>
		targetType switch
		{
			ItemActionUseTargetType.Accessory => TuningActionTargetType.Accessory,
			ItemActionUseTargetType.Armor => TuningActionTargetType.Armor,
			ItemActionUseTargetType.Equipment => TuningActionTargetType.Equipment,
			ItemActionUseTargetType.Weapon => TuningActionTargetType.Weapon,
			ItemActionUseTargetType.Wing => TuningActionTargetType.Wing,
			ItemActionUseTargetType.Other => TuningActionTargetType.Other,
			_ => TuningActionTargetType.All,
		};
}
