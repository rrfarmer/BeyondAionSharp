namespace Aion.GameServer.Services.ToyPet;

public sealed class PetFoodItemGroups
{
	private readonly IReadOnlyDictionary<PetFoodType, IReadOnlySet<int>> _itemIdsByType;

	public PetFoodItemGroups(IReadOnlyDictionary<PetFoodType, IReadOnlySet<int>> itemIdsByType)
	{
		_itemIdsByType = itemIdsByType ?? throw new ArgumentNullException(nameof(itemIdsByType));
	}

	public static PetFoodItemGroups From(params (PetFoodType Type, IReadOnlySet<int> ItemIds)[] groups)
	{
		return new PetFoodItemGroups(groups.ToDictionary(group => group.Type, group => group.ItemIds));
	}

	public bool IsFood(int itemId, PetFoodType foodType)
	{
		// Java parity: dataholders/ItemGroupsData.isFood rejects EXCLUDES and STINKY before checking the requested food type.
		if (Contains(PetFoodType.Excludes, itemId) || Contains(PetFoodType.Stinky, itemId))
		{
			return false;
		}

		if (foodType != PetFoodType.Miscellaneous)
		{
			return Contains(foodType, itemId);
		}

		return Contains(PetFoodType.Armor, itemId)
			|| Contains(PetFoodType.BalaurScales, itemId)
			|| Contains(PetFoodType.Bones, itemId)
			|| Contains(PetFoodType.Fluids, itemId)
			|| Contains(PetFoodType.Souls, itemId)
			|| Contains(PetFoodType.Thorns, itemId);
	}

	private bool Contains(PetFoodType foodType, int itemId)
	{
		return _itemIdsByType.TryGetValue(foodType, out var itemIds) && itemIds.Contains(itemId);
	}
}

public static class PetFoodTypeLookup
{
	public static PetFoodType? GetFoodType(int itemId, IReadOnlyList<PetFeedRewardGroup> rewardGroups, PetFoodItemGroups itemGroups)
	{
		ArgumentNullException.ThrowIfNull(rewardGroups);
		ArgumentNullException.ThrowIfNull(itemGroups);

		// Java parity: model/templates/pet/PetFlavour.getFoodType scans configured reward groups in order.
		foreach (var rewardGroup in rewardGroups)
		{
			if (itemGroups.IsFood(itemId, rewardGroup.Type))
			{
				return rewardGroup.Type;
			}
		}

		return null;
	}
}
