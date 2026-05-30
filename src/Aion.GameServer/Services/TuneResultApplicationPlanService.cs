using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Services;

public enum TuneResultApplicationPlanStatus
{
	Applied,
	MissingPendingResultAudited,
}

public sealed record TuneResultApplicationPlan(
	TuneResultApplicationPlanStatus Status,
	InventoryItem ResultingTargetItem,
	bool TargetItemMutated,
	bool TargetItemPersistentUpdateRequired,
	bool InventoryPersistentUpdateRequired,
	string? AuditMessage,
	string JavaSource,
	bool IsLive = false);

public static class TuneResultApplicationPlanService
{
	public static TuneResultApplicationPlan CreatePlan(InventoryItem targetItem, PendingTuneResult? pendingResult)
	{
		// Java parity: services/item/ItemActionService.applyTuneResult.
		if (pendingResult == null)
		{
			return new TuneResultApplicationPlan(
				TuneResultApplicationPlanStatus.MissingPendingResultAudited,
				targetItem,
				TargetItemMutated: false,
				TargetItemPersistentUpdateRequired: false,
				InventoryPersistentUpdateRequired: false,
				AuditMessage: "attempted to apply a tune result without tuning the item beforehand.",
				JavaSource: "ItemActionService.applyTuneResult -> pendingTuneResult == null -> AuditLogger.log(...) -> return");
		}

		return new TuneResultApplicationPlan(
			TuneResultApplicationPlanStatus.Applied,
			CopyInventoryItem(
				targetItem,
				optionalSocket: pendingResult.OptionalSockets,
				enchantBonus: pendingResult.EnchantBonus,
				randomBonus: pendingResult.StatBonusId),
			TargetItemMutated: true,
			TargetItemPersistentUpdateRequired: true,
			InventoryPersistentUpdateRequired: true,
			AuditMessage: null,
			JavaSource: "ItemActionService.applyTuneResult -> apply optionalSockets/enchantBonus/statBonus, clear pending result, mark item + inventory UPDATE_REQUIRED");
	}

	private static InventoryItem CopyInventoryItem(
		InventoryItem item,
		int optionalSocket,
		int enchantBonus,
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
			TuneCount = item.TuneCount,
			RandomBonus = randomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			ManaStones = item.ManaStones,
			FusionStones = item.FusionStones,
			Godstone = item.Godstone,
			IdianStone = item.IdianStone,
		};
	}
}
