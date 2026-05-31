using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record IdentifyItemStartPlan(
	SmItemUsageAnimation BroadcastPacket,
	int DelayMilliseconds,
	string JavaSource,
	bool IsLive = false);

public sealed record IdentifyItemAbortPlan(
	string CancelledTaskName,
	SmSystemMessage CancelMessage,
	SmItemUsageAnimation BroadcastPacket,
	bool RemoveObserver,
	string JavaSource,
	bool IsLive = false);

public sealed record IdentifyItemCompletionPlan(
	InventoryItem TargetItemUpdate,
	SmItemUsageAnimation BroadcastPacket,
	SmInventoryUpdateItem InventoryUpdatePacket,
	SmSystemMessage SuccessMessage,
	bool RemoveObserver,
	bool InventoryPersistentUpdateRequired,
	string JavaSource,
	bool IsLive = false);

public static class IdentifyItemExecutionPlanService
{
	public const string ItemUseTaskName = "ITEM_USE";
	public const int UseDurationMilliseconds = 5000;

	public static IdentifyItemStartPlan CreateStartPlan(int playerObjectId, int targetItemObjectId, int targetItemId)
	{
		return new IdentifyItemStartPlan(
			new SmItemUsageAnimation(playerObjectId, targetItemObjectId, targetItemId, UseDurationMilliseconds, 9, 0),
			UseDurationMilliseconds,
			"ItemActionService.identifyItem -> broadcast SM_ITEM_USAGE_ANIMATION(player.getObjectId(), item.getObjectId(), itemId, 5000, 9, 0)");
	}

	public static IdentifyItemAbortPlan CreateAbortPlan(int playerObjectId, int targetItemObjectId, int targetItemId, string targetItemName)
	{
		return new IdentifyItemAbortPlan(
			ItemUseTaskName,
			SmSystemMessage.ItemIdentifyCanceled(targetItemName),
			new SmItemUsageAnimation(playerObjectId, targetItemObjectId, targetItemId, 0, 11, 0),
			RemoveObserver: true,
			"ItemActionService.identifyItem abort -> cancel TaskId.ITEM_USE, send STR_MSG_ITEM_IDENTIFY_CANCELED, broadcast SM_ITEM_USAGE_ANIMATION(..., 0, 11, 0), remove observer");
	}

	public static IdentifyItemCompletionPlan CreateCompletionPlan(
		InventoryItem targetItem,
		ItemTemplateSummary targetTemplate,
		int playerObjectId,
		ItemRandomBonusTable itemRandomBonuses,
		string targetItemName,
		Func<int, int, int>? randomInclusive = null,
		Func<double>? randomBonusRoll = null)
	{
		var optionalSockets = NextInclusive(0, targetTemplate.OptionSlotBonus, randomInclusive);
		var enchantBonus = NextInclusive(0, targetTemplate.MaxEnchantBonus, randomInclusive);
		var statBonusId = itemRandomBonuses.SelectRandomBonusNumber("INVENTORY", targetTemplate.StatBonusSetId, randomBonusRoll);
		var updatedItem = CopyInventoryItem(
			targetItem,
			optionalSocket: optionalSockets,
			enchantBonus: enchantBonus,
			tuneCount: targetItem.TuneCount + 1,
			randomBonus: statBonusId);

		return new IdentifyItemCompletionPlan(
			updatedItem,
			new SmItemUsageAnimation(playerObjectId, targetItem.ObjectId, targetItem.ItemId, 0, 10, 0),
			new SmInventoryUpdateItem(updatedItem, targetTemplate, SmInventoryUpdateItem.DecreaseItemUse),
			SmSystemMessage.ItemIdentifySucceed(targetItemName),
			RemoveObserver: true,
			InventoryPersistentUpdateRequired: true,
			"ItemActionService.identifyItem completion -> broadcast SM_ITEM_USAGE_ANIMATION(..., 0, 10, 0), roll optionalSockets/statBonus/enchantBonus, increment tuneCount, send SM_INVENTORY_UPDATE_ITEM + STR_MSG_ITEM_IDENTIFY_SUCCEED, mark inventory UPDATE_REQUIRED");
	}

	private static int NextInclusive(int min, int max, Func<int, int, int>? randomInclusive)
	{
		if (max <= min)
			return min;

		var value = randomInclusive?.Invoke(min, max) ?? Random.Shared.Next(min, max + 1);
		return Math.Clamp(value, min, max);
	}

	private static InventoryItem CopyInventoryItem(
		InventoryItem item,
		int optionalSocket,
		int enchantBonus,
		int tuneCount,
		int randomBonus)
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
			EnchantBonus = enchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = optionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = tuneCount,
			RandomBonus = randomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			PendingTuneResult = item.PendingTuneResult,
			PersistentState = InventoryItem.TransitionPersistentState(item.PersistentState, InventoryItemPersistentState.UpdateRequired),
			ManaStones = item.ManaStones,
			FusionStones = item.FusionStones,
			Godstone = item.Godstone,
			IdianStone = item.IdianStone,
		};
	}
}
