namespace Aion.GameServer.Services.ToyPet;

public enum PetFoodType
{
	// Java parity: model/templates/pet/FoodType enum values.
	AetherCherry,
	AetherCrystalBiscuit,
	AetherGemBiscuit,
	AetherPowderBiscuit,
	Armor,
	BalaurScales,
	Bones,
	Excludes,
	Fluids,
	HealthyFoodAll,
	HealthyFoodSpicy,
	Miscellaneous,
	PoppySnack,
	PoppySnackTasty,
	PoppySnackNutritious,
	Souls,
	ShugoEventCoin,
	Stinky,
	Thorns,
}

public sealed record PetFeedRewardGroup(PetFoodType Type, bool IsLoved, IReadOnlyList<PetFeedReward> Results);

public static class PetFeedPlanner
{
	public static PetFeedReward? ProcessFeedResult(
		PetFeedProgress progress,
		PetFoodType foodType,
		int itemLevel,
		int playerLevel,
		int fullCount,
		IReadOnlyList<PetFeedRewardGroup> rewardGroups,
		IReadOnlyList<int> fullCounts,
		IReadOnlyList<IReadOnlyList<int>> pointValues,
		Func<IReadOnlyList<PetFeedReward>, PetFeedReward?> lovedRewardSelector)
	{
		ArgumentNullException.ThrowIfNull(progress);
		ArgumentNullException.ThrowIfNull(rewardGroups);
		ArgumentNullException.ThrowIfNull(fullCounts);
		ArgumentNullException.ThrowIfNull(pointValues);
		ArgumentNullException.ThrowIfNull(lovedRewardSelector);

		// Java parity: model/templates/pet/PetFlavour.processFeedResult resolves the first reward group matching the food type.
		var rewardGroup = FindRewardGroup(rewardGroups, foodType);
		if (rewardGroup is null)
		{
			return null;
		}

		var maxFeedCount = 1;
		if (rewardGroup.IsLoved)
		{
			progress.SetIsLovedFeeded();
		}
		else
		{
			maxFeedCount = fullCount;
		}

		PetFeedCalculator.UpdatePetFeedProgress(progress, itemLevel, maxFeedCount);
		if (progress.HungryLevel != PetHungryLevel.Full)
		{
			return null;
		}

		return PetFeedCalculator.GetReward(
			maxFeedCount,
			fullCounts,
			pointValues,
			rewardGroup.Results,
			progress,
			playerLevel,
			lovedRewardSelector);
	}

	public static bool IsLovedFood(IReadOnlyList<PetFeedRewardGroup> rewardGroups, PetFoodType foodType)
	{
		ArgumentNullException.ThrowIfNull(rewardGroups);

		// Java parity: model/templates/pet/PetFlavour.isLovedFood ignores itemId and checks the matching food group only.
		return FindRewardGroup(rewardGroups, foodType)?.IsLoved ?? false;
	}

	private static PetFeedRewardGroup? FindRewardGroup(IReadOnlyList<PetFeedRewardGroup> rewardGroups, PetFoodType foodType)
	{
		foreach (var rewardGroup in rewardGroups)
		{
			if (rewardGroup.Type == foodType)
			{
				return rewardGroup;
			}
		}

		return null;
	}
}
