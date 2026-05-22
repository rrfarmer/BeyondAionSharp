using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class AssemblyItemService
{
	private const int CubeStorageId = 0;

	public const int UsageDelayMilliseconds = 1000;

	public static AssemblyItemValidation CanAct(
		Player player,
		ItemTemplateSummary sourceTemplate,
		AssemblyItemTable assemblyItems)
	{
		// Java parity: model/templates/item/actions/AssemblyItemAction.canAct.
		var assemblyItem = sourceTemplate.AssemblyItemId == 0
			? null
			: assemblyItems.GetAssemblyItem(sourceTemplate.AssemblyItemId);
		if (assemblyItem == null)
			return AssemblyItemValidation.Fail(AssemblyItemFailure.MissingAssemblyItem);

		foreach (var partItemId in assemblyItem.Parts)
		{
			if (GetFirstCubeItemByItemId(player.InventoryItems, partItemId) == null)
				return AssemblyItemValidation.Fail(AssemblyItemFailure.MissingPart);
		}

		return AssemblyItemValidation.Success(assemblyItem);
	}

	public static AssemblyItemMutationPlan CreateMutationPlan(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		AssemblyItemSummary assemblyItem,
		ItemTemplateSummary rewardTemplate,
		ItemTemplateTable itemTemplates,
		Func<int> nextObjectId)
	{
		// Java parity: AssemblyItemAction.act decreases each part by item id, then ItemService.addItem(result, 1).
		var workingItems = inventoryItems.ToList();
		var updatedPartsByObjectId = new Dictionary<int, InventoryItem>();
		var deletedPartObjectIds = new List<int>();
		foreach (var partItemId in assemblyItem.Parts)
		{
			var partItem = GetFirstCubeItemByItemId(workingItems, partItemId);
			if (partItem == null)
				return AssemblyItemMutationPlan.Fail(AssemblyItemFailure.MissingPart);

			if (partItem.Count > 1)
			{
				var updatedPart = CopyInventoryItem(partItem, partItem.Count - 1);
				updatedPartsByObjectId[updatedPart.ObjectId] = updatedPart;
				ReplaceInventoryItem(workingItems, updatedPart);
			}
			else
			{
				updatedPartsByObjectId.Remove(partItem.ObjectId);
				deletedPartObjectIds.Add(partItem.ObjectId);
				workingItems.RemoveAll(item => item.ObjectId == partItem.ObjectId);
			}
		}

		var addPlan = InventoryAddService.CreateAddItemPlan(
			player,
			workingItems,
			rewardTemplate,
			1,
			nextObjectId,
			allowInventoryOverflow: false,
			itemTemplates);

		return AssemblyItemMutationPlan.Success(
			updatedPartsByObjectId.Values.ToArray(),
			deletedPartObjectIds,
			addPlan.Succeeded ? addPlan.UpdatedItems : Array.Empty<InventoryItem>(),
			addPlan.Succeeded ? addPlan.AddedItems : Array.Empty<InventoryItem>(),
			addPlan.RemainingCount);
	}

	private static InventoryItem? GetFirstCubeItemByItemId(IReadOnlyList<InventoryItem> inventoryItems, int itemId)
	{
		return inventoryItems.FirstOrDefault(item => item.ItemId == itemId && item.Location == CubeStorageId && !item.IsEquipped);
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
}

public sealed record AssemblyItemValidation(
	bool Succeeded,
	AssemblyItemFailure Failure,
	AssemblyItemSummary? AssemblyItem)
{
	public static AssemblyItemValidation Success(AssemblyItemSummary assemblyItem)
	{
		return new AssemblyItemValidation(true, AssemblyItemFailure.None, assemblyItem);
	}

	public static AssemblyItemValidation Fail(AssemblyItemFailure failure)
	{
		return new AssemblyItemValidation(false, failure, null);
	}
}

public sealed record AssemblyItemMutationPlan(
	bool Succeeded,
	AssemblyItemFailure Failure,
	IReadOnlyList<InventoryItem> UpdatedPartItems,
	IReadOnlyList<int> DeletedPartObjectIds,
	IReadOnlyList<InventoryItem> UpdatedRewardItems,
	IReadOnlyList<InventoryItem> AddedRewardItems,
	long RewardRemainingCount)
{
	public bool RewardSucceeded => RewardRemainingCount == 0;

	public static AssemblyItemMutationPlan Success(
		IReadOnlyList<InventoryItem> updatedPartItems,
		IReadOnlyList<int> deletedPartObjectIds,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		long rewardRemainingCount)
	{
		return new AssemblyItemMutationPlan(
			true,
			AssemblyItemFailure.None,
			updatedPartItems,
			deletedPartObjectIds,
			updatedRewardItems,
			addedRewardItems,
			rewardRemainingCount);
	}

	public static AssemblyItemMutationPlan Fail(AssemblyItemFailure failure)
	{
		return new AssemblyItemMutationPlan(
			false,
			failure,
			Array.Empty<InventoryItem>(),
			Array.Empty<int>(),
			Array.Empty<InventoryItem>(),
			Array.Empty<InventoryItem>(),
			0);
	}
}

public enum AssemblyItemFailure
{
	None,
	MissingAssemblyItem,
	MissingPart,
}
