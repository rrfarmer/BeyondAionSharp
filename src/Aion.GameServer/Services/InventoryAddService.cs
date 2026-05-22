using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class InventoryAddService
{
	private const int CubeStorageId = 0;
	private const long FirstAvailableSlot = 65535;

	public static InventoryAddPlan CreateAddItemPlan(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		ItemTemplateSummary itemTemplate,
		long count,
		Func<int> nextObjectId,
		bool allowInventoryOverflow = false)
	{
		// Java parity: services/item/ItemService.addItem plus model/items/storage/Storage.increaseItemCount/add.
		if (count <= 0)
			return InventoryAddPlan.Empty;

		var remaining = count;
		var updates = new List<InventoryItem>();
		if (itemTemplate.TemplateId == InventoryItemFactory.KinahItemId)
		{
			var kinahItem = inventoryItems.FirstOrDefault(item => item.ItemId == itemTemplate.TemplateId && item.Location == CubeStorageId);
			if (kinahItem != null)
				return InventoryAddPlan.Completed([CopyInventoryItem(kinahItem, kinahItem.Count + remaining)], Array.Empty<InventoryItem>());

			var kinahObjectId = nextObjectId();
			if (kinahObjectId == 0)
				return InventoryAddPlan.Failed(count);

			return InventoryAddPlan.Completed(
				Array.Empty<InventoryItem>(),
				[InventoryItemFactory.CreateNewItem(kinahObjectId, itemTemplate, remaining, player.ObjectId, CubeStorageId, FirstAvailableSlot)]);
		}

		if (itemTemplate.MaxStackCount > 1)
		{
			foreach (var item in inventoryItems.Where(item => CanMergeIntoStack(item, itemTemplate)))
			{
				if (remaining == 0)
					break;

				var available = itemTemplate.MaxStackCount - item.Count;
				if (available <= 0)
					continue;

				var merged = Math.Min(available, remaining);
				updates.Add(CopyInventoryItem(item, item.Count + merged));
				remaining -= merged;
			}
		}

		var addedItems = new List<InventoryItem>();
		var freeSlots = InventoryCapacity.GetFreeCubeSlots(player, inventoryItems);
		while (remaining > 0 && (allowInventoryOverflow || addedItems.Count < freeSlots))
		{
			var objectId = nextObjectId();
			if (objectId == 0)
				return InventoryAddPlan.Failed(remaining);

			var item = InventoryItemFactory.CreateNewItem(
				objectId,
				itemTemplate,
				remaining,
				player.ObjectId,
				CubeStorageId,
				FirstAvailableSlot);
			addedItems.Add(item);
			remaining -= item.Count;
		}

		return new InventoryAddPlan(
			Succeeded: remaining == 0,
			UpdatedItems: updates,
			AddedItems: addedItems,
			RemainingCount: remaining);
	}

	private static bool CanMergeIntoStack(InventoryItem item, ItemTemplateSummary itemTemplate)
	{
		return item.ItemId == itemTemplate.TemplateId
			&& item.Location == CubeStorageId
			&& !item.IsEquipped
			&& item.Count < itemTemplate.MaxStackCount;
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

public sealed record InventoryAddPlan(
	bool Succeeded,
	IReadOnlyList<InventoryItem> UpdatedItems,
	IReadOnlyList<InventoryItem> AddedItems,
	long RemainingCount)
{
	public static InventoryAddPlan Empty { get; } = new(true, Array.Empty<InventoryItem>(), Array.Empty<InventoryItem>(), 0);

	public static InventoryAddPlan Completed(IReadOnlyList<InventoryItem> updatedItems, IReadOnlyList<InventoryItem> addedItems)
	{
		return new InventoryAddPlan(true, updatedItems, addedItems, 0);
	}

	public static InventoryAddPlan Failed(long remainingCount)
	{
		return new InventoryAddPlan(false, Array.Empty<InventoryItem>(), Array.Empty<InventoryItem>(), remainingCount);
	}
}
