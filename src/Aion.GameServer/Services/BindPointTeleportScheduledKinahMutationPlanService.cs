using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum BindPointTeleportScheduledKinahMutationPlanStatus
{
	DecrementReady,
	NotEnoughKinah,
}

public enum BindPointTeleportScheduledKinahMutationPlanStep
{
	FindCubeKinahItem,
	CheckEnoughKinah,
	ContinueWithoutMutation,
	PrepareKinahItemUpdate,
	PrepareInventoryUpdatePacket,
	SendNotEnoughFee,
}

public sealed record BindPointTeleportScheduledKinahMutationPlan(
	BindPointTeleportScheduledKinahMutationPlanStatus Status,
	long RequiredPrice,
	long CurrentKinah,
	long? RemainingKinah,
	InventoryItem? KinahItemUpdate,
	IReadOnlyList<InventoryItem> InventoryItems,
	bool ShouldSendNotEnoughFee,
	bool ShouldEmitInventoryUpdatePacket,
	int? InventoryUpdateType,
	IReadOnlyList<BindPointTeleportScheduledKinahMutationPlanStep> Steps,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportScheduledKinahMutationPlanService
{
	public const int KinahItemId = 182400001;
	public const int CubeStorageId = 0;

	public static BindPointTeleportScheduledKinahMutationPlan CreatePlan(Player player, long requiredPrice)
	{
		// Java parity: BindPointTeleportService scheduled callback calls
		// player.getInventory().tryDecreaseKinah(price, ItemUpdateType.DEC_KINAH_FLY).
		var inventoryItems = player.InventoryItems.ToList();
		var kinahItem = inventoryItems.FirstOrDefault(item => item.ItemId == KinahItemId && item.Location == CubeStorageId);
		var currentKinah = kinahItem?.Count ?? 0;
		if (requiredPrice <= 0)
		{
			return new BindPointTeleportScheduledKinahMutationPlan(
				BindPointTeleportScheduledKinahMutationPlanStatus.DecrementReady,
				requiredPrice,
				currentKinah,
				RemainingKinah: currentKinah,
				KinahItemUpdate: null,
				inventoryItems,
				ShouldSendNotEnoughFee: false,
				ShouldEmitInventoryUpdatePacket: false,
				InventoryUpdateType: null,
				[
					BindPointTeleportScheduledKinahMutationPlanStep.FindCubeKinahItem,
					BindPointTeleportScheduledKinahMutationPlanStep.CheckEnoughKinah,
					BindPointTeleportScheduledKinahMutationPlanStep.ContinueWithoutMutation,
				],
				"Storage.tryDecreaseKinah succeeds for non-positive amount and Storage.decreaseKinah amount > 0 guard prevents item mutation",
				IsLive: false);
		}

		if (kinahItem == null || currentKinah < requiredPrice)
		{
			return new BindPointTeleportScheduledKinahMutationPlan(
				BindPointTeleportScheduledKinahMutationPlanStatus.NotEnoughKinah,
				requiredPrice,
				currentKinah,
				RemainingKinah: null,
				KinahItemUpdate: null,
				inventoryItems,
				ShouldSendNotEnoughFee: true,
				ShouldEmitInventoryUpdatePacket: false,
				InventoryUpdateType: null,
				[
					BindPointTeleportScheduledKinahMutationPlanStep.FindCubeKinahItem,
					BindPointTeleportScheduledKinahMutationPlanStep.CheckEnoughKinah,
					BindPointTeleportScheduledKinahMutationPlanStep.SendNotEnoughFee,
				],
				"BindPointTeleportService.teleport scheduled task -> failed tryDecreaseKinah(price, DEC_KINAH_FLY) sends STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE and returns",
				IsLive: false);
		}

		var updatedKinah = CopyInventoryItem(kinahItem, currentKinah - requiredPrice);
		ReplaceInventoryItem(inventoryItems, updatedKinah);
		return new BindPointTeleportScheduledKinahMutationPlan(
			BindPointTeleportScheduledKinahMutationPlanStatus.DecrementReady,
			requiredPrice,
			currentKinah,
			RemainingKinah: updatedKinah.Count,
			updatedKinah,
			inventoryItems,
			ShouldSendNotEnoughFee: false,
			ShouldEmitInventoryUpdatePacket: true,
			SmInventoryUpdateItem.DecreaseKinahFly,
			[
				BindPointTeleportScheduledKinahMutationPlanStep.FindCubeKinahItem,
				BindPointTeleportScheduledKinahMutationPlanStep.CheckEnoughKinah,
				BindPointTeleportScheduledKinahMutationPlanStep.PrepareKinahItemUpdate,
				BindPointTeleportScheduledKinahMutationPlanStep.PrepareInventoryUpdatePacket,
			],
			"Storage.tryDecreaseKinah -> decreaseKinah(price, DEC_KINAH_FLY) -> decreaseItemCount keeps Kinah item at zero and sends SM_INVENTORY_UPDATE_ITEM",
			IsLive: false);
	}

	private static void ReplaceInventoryItem(List<InventoryItem> inventoryItems, InventoryItem replacement)
	{
		var index = inventoryItems.FindIndex(item => item.ObjectId == replacement.ObjectId);
		if (index >= 0)
			inventoryItems[index] = replacement;
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long count)
	{
		var copy = new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count,
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
		};
		copy.ManaStones = item.ManaStones;
		copy.FusionStones = item.FusionStones;
		copy.Godstone = item.Godstone;
		copy.IdianStone = item.IdianStone;
		return copy;
	}
}
