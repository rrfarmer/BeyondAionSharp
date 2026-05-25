using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class ItemPurificationMaterialMutationService
{
	private const int KinahItemId = 182400001;

	public static ItemPurificationMaterialMutationPlan CreateDecreaseMaterialsPlan(
		Player? player,
		InventoryItem? baseItem,
		IReadOnlyList<ItemPurificationMaterialRequirement> requiredMaterials,
		int necessaryAbyssPoints,
		long necessaryKinah)
	{
		// Java parity: services/item/ItemPurificationService.decreaseMaterials consumes each required
		// material by item id before AP spend, kinah attempt, and base-item removal.
		if (player == null)
			return ItemPurificationMaterialMutationPlan.Failed(ItemPurificationMaterialMutationStatus.MissingPlayer);
		if (baseItem == null)
			return ItemPurificationMaterialMutationPlan.Failed(ItemPurificationMaterialMutationStatus.MissingBaseItem);

		var workingItems = player.InventoryItems.ToList();
		var updatedItemsByObjectId = new Dictionary<int, InventoryItem>();
		var deletedObjectIds = new List<int>();
		var steps = new List<ItemPurificationMutationStep>();

		foreach (var requiredMaterial in requiredMaterials)
		{
			var materialResult = PlanDecreaseByItemId(
				workingItems,
				requiredMaterial.ItemId,
				requiredMaterial.ItemCount,
				deleteWhenZero: true,
				updatedItemsByObjectId,
				deletedObjectIds,
				steps);
			if (!materialResult.Succeeded)
			{
				// Java returns false here without rolling back earlier material decrements.
				return new ItemPurificationMaterialMutationPlan(
					ItemPurificationMaterialMutationStatus.MissingRequiredMaterial,
					updatedItemsByObjectId.Values.ToArray(),
					deletedObjectIds.ToArray(),
					steps.ToArray(),
					AbyssPointsToSpend: 0,
					necessaryKinah,
					KinahSpendApplied: false,
					BaseItemDeleteAttempted: false,
					BaseItemDeleted: false,
					materialResult.MissingItemId,
					materialResult.MissingCount);
			}
		}

		var baseDeleteResult = PlanDecreaseByObjectId(
			workingItems,
			baseItem.ObjectId,
			count: 1,
			updatedItemsByObjectId,
			deletedObjectIds,
			steps);

		return new ItemPurificationMaterialMutationPlan(
			ItemPurificationMaterialMutationStatus.Succeeded,
			updatedItemsByObjectId.Values.ToArray(),
			deletedObjectIds.ToArray(),
			steps.ToArray(),
			necessaryAbyssPoints > 0 ? necessaryAbyssPoints : 0,
			necessaryKinah,
			KinahSpendApplied: false,
			BaseItemDeleteAttempted: true,
			BaseItemDeleted: baseDeleteResult.Succeeded,
			MissingItemId: 0,
			MissingCount: 0);
	}

	private static DecreaseResult PlanDecreaseByItemId(
		List<InventoryItem> workingItems,
		int itemId,
		long count,
		bool deleteWhenZero,
		Dictionary<int, InventoryItem> updatedItemsByObjectId,
		List<int> deletedObjectIds,
		List<ItemPurificationMutationStep> steps)
	{
		var remaining = count;
		var matchingItems = workingItems
			.Where(item => item.ItemId == itemId && item.Count > 0)
			.ToArray();
		if (matchingItems.Length == 0)
			return new DecreaseResult(false, itemId, count);

		foreach (var item in matchingItems)
		{
			if (remaining == 0)
				break;

			remaining = PlanDecreaseItemCount(
				workingItems,
				item,
				remaining,
				deleteWhenZero,
				updatedItemsByObjectId,
				deletedObjectIds,
				steps);
		}

		return remaining == 0
			? DecreaseResult.Success()
			: new DecreaseResult(false, itemId, remaining);
	}

	private static DecreaseResult PlanDecreaseByObjectId(
		List<InventoryItem> workingItems,
		int objectId,
		long count,
		Dictionary<int, InventoryItem> updatedItemsByObjectId,
		List<int> deletedObjectIds,
		List<ItemPurificationMutationStep> steps)
	{
		var item = workingItems.FirstOrDefault(candidate => candidate.ObjectId == objectId);
		if (item == null || item.Count < count)
			return new DecreaseResult(false, item?.ItemId ?? 0, count);

		var remaining = PlanDecreaseItemCount(
			workingItems,
			item,
			count,
			deleteWhenZero: true,
			updatedItemsByObjectId,
			deletedObjectIds,
			steps);
		return remaining == 0
			? DecreaseResult.Success()
			: new DecreaseResult(false, item.ItemId, remaining);
	}

	private static long PlanDecreaseItemCount(
		List<InventoryItem> workingItems,
		InventoryItem item,
		long count,
		bool deleteWhenZero,
		Dictionary<int, InventoryItem> updatedItemsByObjectId,
		List<int> deletedObjectIds,
		List<ItemPurificationMutationStep> steps)
	{
		if (count <= 0)
			return 0;

		var consumed = Math.Min(item.Count, count);
		var newCount = item.Count - consumed;
		if (newCount == 0 && deleteWhenZero && item.ItemId != KinahItemId)
		{
			updatedItemsByObjectId.Remove(item.ObjectId);
			deletedObjectIds.Add(item.ObjectId);
			workingItems.RemoveAll(candidate => candidate.ObjectId == item.ObjectId);
		}
		else
		{
			var updatedItem = CopyInventoryItem(item, newCount);
			updatedItemsByObjectId[updatedItem.ObjectId] = updatedItem;
			ReplaceInventoryItem(workingItems, updatedItem);
		}

		steps.Add(new ItemPurificationMutationStep(
			item.ItemId,
			item.ObjectId,
			consumed,
			newCount,
			IsKinah: item.ItemId == KinahItemId));
		return count - consumed;
	}

	private static void ReplaceInventoryItem(List<InventoryItem> items, InventoryItem replacement)
	{
		var index = items.FindIndex(item => item.ObjectId == replacement.ObjectId);
		if (index >= 0)
			items[index] = replacement;
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

	private sealed record DecreaseResult(bool Succeeded, int MissingItemId, long MissingCount)
	{
		public static DecreaseResult Success()
		{
			return new DecreaseResult(true, MissingItemId: 0, MissingCount: 0);
		}
	}
}

public sealed record ItemPurificationMaterialMutationPlan(
	ItemPurificationMaterialMutationStatus Status,
	IReadOnlyList<InventoryItem> UpdatedItems,
	IReadOnlyList<int> DeletedObjectIds,
	IReadOnlyList<ItemPurificationMutationStep> MutationSteps,
	int AbyssPointsToSpend,
	long NecessaryKinah,
	bool KinahSpendApplied,
	bool BaseItemDeleteAttempted,
	bool BaseItemDeleted,
	int MissingItemId,
	long MissingCount)
{
	public bool Succeeded => Status == ItemPurificationMaterialMutationStatus.Succeeded;

	public static ItemPurificationMaterialMutationPlan Failed(ItemPurificationMaterialMutationStatus status)
	{
		return new ItemPurificationMaterialMutationPlan(
			status,
			Array.Empty<InventoryItem>(),
			Array.Empty<int>(),
			Array.Empty<ItemPurificationMutationStep>(),
			AbyssPointsToSpend: 0,
			NecessaryKinah: 0,
			KinahSpendApplied: false,
			BaseItemDeleteAttempted: false,
			BaseItemDeleted: false,
			MissingItemId: 0,
			MissingCount: 0);
	}
}

public sealed record ItemPurificationMutationStep(
	int ItemId,
	int ObjectId,
	long ConsumedCount,
	long NewCount,
	bool IsKinah);

public enum ItemPurificationMaterialMutationStatus
{
	Succeeded,
	MissingPlayer,
	MissingBaseItem,
	MissingRequiredMaterial,
}
