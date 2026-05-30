using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum TuningActionTargetType
{
	Accessory,
	Armor,
	Equipment,
	Weapon,
	Wing,
	Other,
	All,
}

public enum TuningActionGuardPlanStatus
{
	Allowed,
	BlockedEquippedTarget,
	BlockedUnidentifiedTarget,
	BlockedUntunableTarget,
	BlockedWrongTargetType,
	BlockedHigherTargetLevel,
	BlockedMaxTuneCount,
}

public sealed record TuningActionGuardPlan(
	TuningActionGuardPlanStatus Status,
	bool CanAct,
	SmSystemMessage? DenialMessage,
	string JavaSource,
	bool IsLive = false);

public static class TuningActionGuardPlanService
{
	public static TuningActionGuardPlan CreatePlan(
		InventoryItem parentItem,
		ItemTemplateSummary parentTemplate,
		InventoryItem targetItem,
		ItemTemplateSummary targetTemplate,
		TuningActionTargetType targetType,
		bool shouldNotReduceTuneCount,
		string tuningScrollName,
		string targetItemName)
	{
		// Java parity: model/templates/item/actions/TuningAction.canAct.
		if (targetItem.IsEquipped)
		{
			return Blocked(
				TuningActionGuardPlanStatus.BlockedEquippedTarget,
				denialMessage: null,
				"TuningAction.canAct -> targetItem.isEquipped() -> return false");
		}

		if (!targetItem.IsIdentified)
		{
			return Blocked(
				TuningActionGuardPlanStatus.BlockedUnidentifiedTarget,
				SmSystemMessage.ItemReidentifyDidntIdentify(targetItemName),
				"TuningAction.canAct -> !targetItem.isIdentified() -> STR_MSG_ITEM_REIDENTIFY_DIDNT_IDENTIFY(targetItem.getL10n())");
		}

		if (!targetTemplate.CanTune)
		{
			return Blocked(
				TuningActionGuardPlanStatus.BlockedUntunableTarget,
				SmSystemMessage.ItemReidentifyCannotReidentify(targetItemName),
				"TuningAction.canAct -> !targetItem.getItemTemplate().canTune() -> STR_MSG_ITEM_REIDENTIFY_CANNOT_REIDENTIFY(targetItem.getL10n())");
		}

		if ((targetType == TuningActionTargetType.Weapon && !targetTemplate.IsWeapon)
			|| (targetType == TuningActionTargetType.Armor && !targetTemplate.IsArmor))
		{
			return Blocked(
				TuningActionGuardPlanStatus.BlockedWrongTargetType,
				SmSystemMessage.ItemReidentifyWrongSelect(tuningScrollName, targetItemName),
				"TuningAction.canAct -> target mismatch -> STR_MSG_ITEM_REIDENTIFY_WRONG_SELECT(parentItem.getL10n(), targetItem.getL10n())");
		}

		if (targetTemplate.Level > parentTemplate.Level)
		{
			return Blocked(
				TuningActionGuardPlanStatus.BlockedHigherTargetLevel,
				SmSystemMessage.ItemReidentifyWrongLevel(tuningScrollName, targetItemName),
				"TuningAction.canAct -> targetItem.getItemTemplate().getLevel() > parentItem.getItemTemplate().getLevel() -> STR_MSG_ITEM_REIDENTIFY_WRONG_LEVEL(parentItem.getL10n(), targetItem.getL10n())");
		}

		if (!shouldNotReduceTuneCount && targetItem.TuneCount >= targetTemplate.MaxTuneCount)
		{
			return Blocked(
				TuningActionGuardPlanStatus.BlockedMaxTuneCount,
				denialMessage: null,
				"TuningAction.canAct -> !shouldNotReduceTuneCount && targetItem.getTuneCount() >= targetItem.getItemTemplate().getMaxTuneCount() -> return false");
		}

		return new TuningActionGuardPlan(
			TuningActionGuardPlanStatus.Allowed,
			CanAct: true,
			DenialMessage: null,
			JavaSource: shouldNotReduceTuneCount
				? "TuningAction.canAct -> shouldNotReduceTuneCount bypasses final tune-count guard"
				: "TuningAction.canAct -> all guards passed");
	}

	private static TuningActionGuardPlan Blocked(
		TuningActionGuardPlanStatus status,
		SmSystemMessage? denialMessage,
		string javaSource)
	{
		return new TuningActionGuardPlan(
			status,
			CanAct: false,
			DenialMessage: denialMessage,
			JavaSource: javaSource);
	}
}
