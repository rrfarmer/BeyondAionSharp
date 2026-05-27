namespace Aion.GameServer.Services.ToyPet;

public sealed class PetFeedEvaluationContext
{
	public PetFeedEvaluationContext(
		IReadOnlyDictionary<int, PetFeedFlavourProjection> flavours,
		PetFoodItemGroups itemGroups,
		IReadOnlyDictionary<int, int> itemLevels)
	{
		Flavours = flavours ?? throw new ArgumentNullException(nameof(flavours));
		ItemGroups = itemGroups ?? throw new ArgumentNullException(nameof(itemGroups));
		ItemLevels = itemLevels ?? throw new ArgumentNullException(nameof(itemLevels));
		FullCounts = PetFeedXmlProjection.GetSortedFullCounts(flavours);
		PointValues = PetFeedCalculator.CreatePointValues(FullCounts);
	}

	public IReadOnlyDictionary<int, PetFeedFlavourProjection> Flavours { get; }

	public PetFoodItemGroups ItemGroups { get; }

	public IReadOnlyDictionary<int, int> ItemLevels { get; }

	public IReadOnlyList<int> FullCounts { get; }

	public IReadOnlyList<IReadOnlyList<int>> PointValues { get; }
}

public sealed record PetFeedEvaluationResult(
	PetFoodType? FoodType,
	bool IsLovedFood,
	PetFeedReward? Reward);

public static class PetFeedEvaluation
{
	public static PetFeedEvaluationResult Evaluate(
		PetFeedEvaluationContext context,
		int flavourId,
		PetFeedProgress progress,
		int itemId,
		int playerLevel,
		Func<IReadOnlyList<PetFeedReward>, PetFeedReward?> lovedRewardSelector)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (!context.Flavours.TryGetValue(flavourId, out var flavour))
		{
			throw new KeyNotFoundException($"Missing Java pet feed flavour id {flavourId}.");
		}

		return Evaluate(context, flavour, progress, itemId, playerLevel, lovedRewardSelector);
	}

	public static PetFeedEvaluationResult Evaluate(
		PetFeedEvaluationContext context,
		PetFeedFlavourProjection flavour,
		PetFeedProgress progress,
		int itemId,
		int playerLevel,
		Func<IReadOnlyList<PetFeedReward>, PetFeedReward?> lovedRewardSelector)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(flavour);
		ArgumentNullException.ThrowIfNull(progress);
		ArgumentNullException.ThrowIfNull(lovedRewardSelector);

		// Java parity: PetService.checkFeeding resolves the item food type before mutating inventory or progress.
		var foodType = PetFoodTypeLookup.GetFoodType(itemId, flavour.RewardGroups, context.ItemGroups);
		if (foodType is null)
		{
			return new PetFeedEvaluationResult(null, IsLovedFood: false, Reward: null);
		}

		var isLovedFood = PetFeedPlanner.IsLovedFood(flavour.RewardGroups, foodType.Value);
		if (isLovedFood && progress.LovedFoodRemaining == 0)
		{
			// Java parity: PetService clears foodType when the loved-food limit is exhausted, then follows the non-eatable branch.
			return new PetFeedEvaluationResult(null, IsLovedFood: true, Reward: null);
		}

		var itemLevel = GetItemLevel(context, itemId);
		var rewardGroups = ResolveLovedRewardLevels(flavour.RewardGroups, foodType.Value, context.ItemLevels);
		var reward = PetFeedPlanner.ProcessFeedResult(
			progress,
			foodType.Value,
			itemLevel,
			playerLevel,
			flavour.FullCount,
			rewardGroups,
			context.FullCounts,
			context.PointValues,
			lovedRewardSelector);

		return new PetFeedEvaluationResult(foodType, isLovedFood, reward);
	}

	private static int GetItemLevel(PetFeedEvaluationContext context, int itemId)
	{
		if (context.ItemLevels.TryGetValue(itemId, out var level))
		{
			return level;
		}

		throw new KeyNotFoundException($"Missing Java item-template level for fed item id {itemId}.");
	}

	private static IReadOnlyList<PetFeedRewardGroup> ResolveLovedRewardLevels(
		IReadOnlyList<PetFeedRewardGroup> rewardGroups,
		PetFoodType foodType,
		IReadOnlyDictionary<int, int> itemLevels)
	{
		for (var i = 0; i < rewardGroups.Count; i++)
		{
			var rewardGroup = rewardGroups[i];
			if (rewardGroup.Type != foodType || !rewardGroup.IsLoved || rewardGroup.Results.Count <= 1)
			{
				continue;
			}

			var resolvedGroups = rewardGroups.ToArray();
			resolvedGroups[i] = rewardGroup with
			{
				Results = rewardGroup.Results.Select(reward => ResolveRewardLevel(reward, itemLevels)).ToArray(),
			};
			return resolvedGroups;
		}

		return rewardGroups;
	}

	private static PetFeedReward ResolveRewardLevel(PetFeedReward reward, IReadOnlyDictionary<int, int> itemLevels)
	{
		if (itemLevels.TryGetValue(reward.ItemId, out var level))
		{
			return reward with { ItemLevel = level };
		}

		throw new KeyNotFoundException($"Missing Java item-template level for loved reward item id {reward.ItemId}.");
	}
}
