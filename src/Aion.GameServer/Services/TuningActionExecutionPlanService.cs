using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum TuningActionCompletionPlanStatus
{
	Planned,
	ScrollConsumptionFailed,
}

public sealed record TuningActionStartPlan(
	SmItemUsageAnimation BroadcastPacket,
	int DelayMilliseconds,
	string JavaSource,
	bool IsLive = false);

public sealed record TuningActionAbortPlan(
	string CancelledTaskName,
	int RemovedCooldownDelayId,
	SmSystemMessage CancelMessage,
	SmItemUsageAnimation BroadcastPacket,
	bool RemoveObserver,
	string JavaSource,
	bool IsLive = false);

public sealed record TuningActionCompletionPlan(
	TuningActionCompletionPlanStatus Status,
	SmItemUsageAnimation BroadcastPacket,
	bool RemoveObserver,
	bool AttemptDecreaseScroll,
	InventoryItem? TargetItemUpdate,
	PendingTuneResult? PendingResult,
	SmTuneResult? ResultPacket,
	SmSystemMessage? SuccessMessage,
	bool InventoryPersistentUpdateRequired,
	string JavaSource,
	bool IsLive = false);

public static class TuningActionExecutionPlanService
{
	public const string ItemUseTaskName = "ITEM_USE";
	public const int UseDurationMilliseconds = 5000;

	public static TuningActionStartPlan CreateStartPlan(int playerObjectId, int tuningScrollObjectId, int tuningScrollItemId)
	{
		return new TuningActionStartPlan(
			new SmItemUsageAnimation(playerObjectId, tuningScrollObjectId, tuningScrollItemId, UseDurationMilliseconds, 12, 0),
			UseDurationMilliseconds,
			" TuningAction.act -> broadcast SM_ITEM_USAGE_ANIMATION(player.getObjectId(), parentItem.getObjectId(), tuningScrollItemId, 5000, 12, 0)".TrimStart());
	}

	public static TuningActionAbortPlan CreateAbortPlan(
		int playerObjectId,
		int tuningScrollObjectId,
		int tuningScrollItemId,
		int removedCooldownDelayId,
		string targetItemName)
	{
		return new TuningActionAbortPlan(
			ItemUseTaskName,
			removedCooldownDelayId,
			SmSystemMessage.ItemReidentifyCanceled(targetItemName),
			new SmItemUsageAnimation(playerObjectId, tuningScrollObjectId, tuningScrollItemId, 0, 14, 0),
			RemoveObserver: true,
			"TuningAction.act abort -> cancel TaskId.ITEM_USE, remove cooldown, send STR_MSG_ITEM_REIDENTIFY_CANCELED, broadcast SM_ITEM_USAGE_ANIMATION(..., 0, 14, 0), remove observer");
	}

	public static TuningActionCompletionPlan CreateCompletionPlan(
		InventoryItem targetItem,
		ItemTemplateSummary targetTemplate,
		int maxOptionalSockets,
		int maxEnchantBonus,
		int playerObjectId,
		int tuningScrollObjectId,
		int tuningScrollItemId,
		bool shouldNotReduceTuneCount,
		bool scrollConsumptionSucceeded,
		ItemRandomBonusTable itemRandomBonuses,
		string targetItemName,
		Func<int, int, int>? randomInclusive = null,
		Func<double>? randomBonusRoll = null)
	{
		var completionPacket = new SmItemUsageAnimation(playerObjectId, tuningScrollObjectId, tuningScrollItemId, 0, 13, 0);
		if (!scrollConsumptionSucceeded)
		{
			return new TuningActionCompletionPlan(
				TuningActionCompletionPlanStatus.ScrollConsumptionFailed,
				completionPacket,
				RemoveObserver: true,
				AttemptDecreaseScroll: true,
				TargetItemUpdate: null,
				PendingResult: null,
				ResultPacket: null,
				SuccessMessage: null,
				InventoryPersistentUpdateRequired: false,
				"TuningAction.act completion -> broadcast SM_ITEM_USAGE_ANIMATION(..., 0, 13, 0), decreaseByObjectId failed -> return");
		}

		var updatedTarget = targetItem;
		int optionalSockets;
		int enchantBonus;
		var inventoryPersistentUpdateRequired = false;
		if (shouldNotReduceTuneCount)
		{
			optionalSockets = targetItem.OptionalSocket;
			enchantBonus = targetItem.EnchantBonus;
		}
		else
		{
			updatedTarget = CopyInventoryItem(targetItem, tuneCount: targetItem.TuneCount + 1);
			inventoryPersistentUpdateRequired = true;
			optionalSockets = NextInclusive(0, maxOptionalSockets, randomInclusive);
			enchantBonus = NextInclusive(0, maxEnchantBonus, randomInclusive);
		}

		var statBonusId = itemRandomBonuses.SelectRandomBonusNumber("INVENTORY", targetTemplate.StatBonusSetId, randomBonusRoll);
		var pendingResult = new PendingTuneResult(optionalSockets, enchantBonus, statBonusId, shouldNotReduceTuneCount);

		return new TuningActionCompletionPlan(
			TuningActionCompletionPlanStatus.Planned,
			completionPacket,
			RemoveObserver: true,
			AttemptDecreaseScroll: true,
			TargetItemUpdate: CopyInventoryItem(updatedTarget, pendingResult: pendingResult),
			PendingResult: pendingResult,
			ResultPacket: new SmTuneResult(updatedTarget, targetTemplate, tuningScrollItemId, pendingResult),
			SuccessMessage: SmSystemMessage.ItemReidentifySucceed(targetItemName),
			InventoryPersistentUpdateRequired: inventoryPersistentUpdateRequired,
			shouldNotReduceTuneCount
				? "TuningAction.act completion -> attribute-only tuning reuses optional sockets and enchant bonus, then sends SM_TUNE_RESULT + STR_MSG_ITEM_REIDENTIFY_SUCCEED"
				: "TuningAction.act completion -> increment tuneCount, mark inventory UPDATE_REQUIRED, roll optional sockets/enchant bonus/stat bonus, then sends SM_TUNE_RESULT + STR_MSG_ITEM_REIDENTIFY_SUCCEED");
	}

	private static int NextInclusive(int min, int max, Func<int, int, int>? randomInclusive)
	{
		if (max <= min)
			return min;

		var value = randomInclusive?.Invoke(min, max) ?? Random.Shared.Next(min, max + 1);
		return Math.Clamp(value, min, max);
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, int tuneCount)
	{
		return new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = item.Count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = tuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			PendingTuneResult = item.PendingTuneResult,
			ManaStones = item.ManaStones,
			FusionStones = item.FusionStones,
			Godstone = item.Godstone,
			IdianStone = item.IdianStone,
		};
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, PendingTuneResult pendingResult)
	{
		return new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = item.Count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			PendingTuneResult = pendingResult,
			ManaStones = item.ManaStones,
			FusionStones = item.FusionStones,
			Godstone = item.Godstone,
			IdianStone = item.IdianStone,
		};
	}
}
