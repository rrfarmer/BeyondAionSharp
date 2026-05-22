using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class CompositionService
{
	private const int CubeStorageId = 0;

	public const int UsageDelayMilliseconds = 5000;

	public static CompositionValidation CanAct(
		ItemTemplateSummary toolTemplate,
		InventoryItem firstItem,
		ItemTemplateSummary firstTemplate,
		InventoryItem secondItem,
		ItemTemplateSummary secondTemplate)
	{
		// Java parity: model/templates/item/actions/CompositionAction.canAct.
		if (!toolTemplate.HasCompositionAction || !string.Equals(toolTemplate.ItemGroup, "COMBINATION", StringComparison.Ordinal))
			return CompositionValidation.Fail(CompositionFailure.InvalidTool);
		if (!string.Equals(firstTemplate.ItemGroup, "ENCHANTMENT", StringComparison.Ordinal))
			return CompositionValidation.Fail(CompositionFailure.InvalidFirstStone);
		if (!string.Equals(secondTemplate.ItemGroup, "ENCHANTMENT", StringComparison.Ordinal))
			return CompositionValidation.Fail(CompositionFailure.InvalidSecondStone);
		if (firstItem.Count < 1 || secondItem.Count < 1)
			return CompositionValidation.Fail(CompositionFailure.MissingStoneCount);
		if (firstTemplate.Level > 95 || secondTemplate.Level > 95)
			return CompositionValidation.Fail(CompositionFailure.StoneLevelTooHigh);

		return CompositionValidation.Success();
	}

	public static CompositionMutationPlan CreateMutationPlan(
		Player player,
		IReadOnlyList<InventoryItem> inventoryItems,
		int toolItemId,
		int firstItemId,
		int firstItemLevel,
		int secondItemId,
		int secondItemLevel,
		ItemTemplateTable itemTemplates,
		Func<int, int, int> randomInclusive,
		Func<int> nextObjectId)
	{
		// Java parity: CompositionAction.run decreases tool/first/second by item id, then ItemService.addItem(result, 1).
		var workingItems = inventoryItems.ToList();
		var updatedItemsByObjectId = new Dictionary<int, InventoryItem>();
		var deletedObjectIds = new List<int>();

		var toolConsumed = DecreaseByItemId(workingItems, toolItemId, updatedItemsByObjectId, deletedObjectIds);
		var firstConsumed = DecreaseByItemId(workingItems, firstItemId, updatedItemsByObjectId, deletedObjectIds);
		var secondConsumed = DecreaseByItemId(workingItems, secondItemId, updatedItemsByObjectId, deletedObjectIds);
		if (!toolConsumed || !firstConsumed || !secondConsumed)
		{
			return CompositionMutationPlan.Success(
				updatedItemsByObjectId.Values.ToArray(),
				deletedObjectIds,
				Array.Empty<InventoryItem>(),
				Array.Empty<InventoryItem>(),
				1,
				false,
				0);
		}

		var rewardItemId = GetRewardItemId(firstItemLevel, secondItemLevel, randomInclusive);
		var rewardTemplate = itemTemplates.GetItemTemplate(rewardItemId);
		if (rewardTemplate == null)
		{
			return CompositionMutationPlan.Success(
				updatedItemsByObjectId.Values.ToArray(),
				deletedObjectIds,
				Array.Empty<InventoryItem>(),
				Array.Empty<InventoryItem>(),
				1,
				false,
				rewardItemId);
		}

		var addPlan = InventoryAddService.CreateAddItemPlan(
			player,
			workingItems,
			rewardTemplate,
			1,
			nextObjectId,
			allowInventoryOverflow: false,
			itemTemplates);

		return CompositionMutationPlan.Success(
			updatedItemsByObjectId.Values.ToArray(),
			deletedObjectIds,
			addPlan.UpdatedItems,
			addPlan.AddedItems,
			addPlan.RemainingCount,
			addPlan.InventoryFull,
			rewardItemId);
	}

	public static int GetRewardItemId(int firstItemLevel, int secondItemLevel, Func<int, int, int> randomInclusive)
	{
		var value = (firstItemLevel + secondItemLevel) / 2;
		if (value < 11)
		{
			value = randomInclusive(1, 20);
		}
		else
		{
			var random = randomInclusive(1, 10);
			var bit = randomInclusive(0, 1);
			value = bit == 0 ? value - random : value + random;
		}

		return 166000000 + value;
	}

	private static bool DecreaseByItemId(
		List<InventoryItem> workingItems,
		int itemId,
		Dictionary<int, InventoryItem> updatedItemsByObjectId,
		List<int> deletedObjectIds)
	{
		var item = workingItems.FirstOrDefault(candidate => candidate.ItemId == itemId && candidate.Location == CubeStorageId && !candidate.IsEquipped);
		if (item == null)
			return false;

		if (item.Count > 1)
		{
			var updatedItem = CopyInventoryItem(item, item.Count - 1);
			updatedItemsByObjectId[updatedItem.ObjectId] = updatedItem;
			ReplaceInventoryItem(workingItems, updatedItem);
		}
		else
		{
			updatedItemsByObjectId.Remove(item.ObjectId);
			deletedObjectIds.Add(item.ObjectId);
			workingItems.RemoveAll(candidate => candidate.ObjectId == item.ObjectId);
		}

		return true;
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

public sealed record CompositionValidation(bool Succeeded, CompositionFailure Failure)
{
	public static CompositionValidation Success()
	{
		return new CompositionValidation(true, CompositionFailure.None);
	}

	public static CompositionValidation Fail(CompositionFailure failure)
	{
		return new CompositionValidation(false, failure);
	}
}

public sealed record CompositionMutationPlan(
	bool Succeeded,
	IReadOnlyList<InventoryItem> UpdatedConsumedItems,
	IReadOnlyList<int> DeletedConsumedObjectIds,
	IReadOnlyList<InventoryItem> UpdatedRewardItems,
	IReadOnlyList<InventoryItem> AddedRewardItems,
	long RewardRemainingCount,
	bool RewardInventoryFull,
	int RewardItemId)
{
	public bool RewardSucceeded => RewardItemId != 0 && RewardRemainingCount == 0;

	public static CompositionMutationPlan Success(
		IReadOnlyList<InventoryItem> updatedConsumedItems,
		IReadOnlyList<int> deletedConsumedObjectIds,
		IReadOnlyList<InventoryItem> updatedRewardItems,
		IReadOnlyList<InventoryItem> addedRewardItems,
		long rewardRemainingCount,
		bool rewardInventoryFull,
		int rewardItemId)
	{
		return new CompositionMutationPlan(
			true,
			updatedConsumedItems,
			deletedConsumedObjectIds,
			updatedRewardItems,
			addedRewardItems,
			rewardRemainingCount,
			rewardInventoryFull,
			rewardItemId);
	}
}

public enum CompositionFailure
{
	None,
	InvalidTool,
	InvalidFirstStone,
	InvalidSecondStone,
	MissingStoneCount,
	StoneLevelTooHigh,
}
