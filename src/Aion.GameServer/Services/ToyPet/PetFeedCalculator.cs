namespace Aion.GameServer.Services.ToyPet;

public sealed record PetFeedReward(int ItemId, int ItemLevel);

public static class PetFeedCalculator
{
	private const int ItemMaxLevel = 60;
	private static readonly byte[] ItemLevels = CreateItemLevels();

	public static int GetPoints(int feedPoints, int maxFeedCount)
	{
		// Java parity: services/toypet/PetFeedCalculator.getPoints.
		var points = 0;
		var state = 0;
		var consumed = 0;
		while (consumed < maxFeedCount)
		{
			var needSwitch = false;
			var oldPoints = points;
			if ((state == 0 && consumed > maxFeedCount * 0.5f)
				|| (state == 1 && consumed > maxFeedCount * 0.8f)
				|| (state == 2 && consumed > maxFeedCount * 1.05))
			{
				needSwitch = true;
			}

			points += feedPoints;
			if (needSwitch)
			{
				state++;
				if ((state == 1 && consumed <= 0.487f * maxFeedCount)
					|| (state == 2 && consumed <= 0.78f * maxFeedCount))
				{
					state--;
					points = oldPoints;
				}
			}

			consumed++;
		}

		return points;
	}

	public static int[][] CreatePointValues(IReadOnlyList<int> fullCounts)
	{
		ArgumentNullException.ThrowIfNull(fullCounts);

		var pointValues = new int[ItemLevels.Length][];
		for (var i = 0; i < pointValues.Length; i++)
		{
			pointValues[i] = new int[fullCounts.Count];
		}

		foreach (var levelByte in ItemLevels)
		{
			var level = levelByte & 0xFF;
			if (level < 10)
			{
				continue;
			}

			for (var countIndex = 0; countIndex < fullCounts.Count; countIndex++)
			{
				var count = fullCounts[countIndex] & 0xFF;
				var finalLevel = level;
				if (finalLevel % 5 == 0)
				{
					finalLevel--;
				}

				var pointLevel = ItemLevels[finalLevel / 5];
				var feedPoints = Math.Max(0, pointLevel - 5) / 5 * 8;
				pointValues[finalLevel / 5][countIndex] = GetPoints(feedPoints, count);
			}
		}

		return pointValues;
	}

	public static void UpdatePetFeedProgress(PetFeedProgress progress, int itemLevel, int maxFeedCount)
	{
		ArgumentNullException.ThrowIfNull(progress);

		var currentHungryLevel = progress.HungryLevel;
		if (progress.IsLovedFeeded)
		{
			if (progress.LovedFoodRemaining == 0)
			{
				return;
			}

			progress.HungryLevel = PetHungryLevel.Full;
			progress.IncrementCount(lovedFood: true);
			return;
		}

		var oldPoints = progress.TotalPoints;
		var needSwitch = false;
		if ((currentHungryLevel == PetHungryLevel.Hungry && progress.RegularCount > maxFeedCount * 0.5f)
			|| (currentHungryLevel == PetHungryLevel.Content && progress.RegularCount > maxFeedCount * 0.8f)
			|| (currentHungryLevel == PetHungryLevel.Semifull && progress.RegularCount > maxFeedCount * 1.05))
		{
			needSwitch = true;
		}
		else
		{
			progress.TotalPoints = progress.TotalPoints + GetFeedPointsForItemLevel(itemLevel);
		}

		if (needSwitch)
		{
			var nextLevel = progress.HungryLevel.GetNextValue();
			if ((nextLevel == PetHungryLevel.Content && progress.RegularCount <= 0.487f * maxFeedCount)
				|| (nextLevel == PetHungryLevel.Semifull && progress.RegularCount <= 0.78f * maxFeedCount))
			{
				progress.TotalPoints = oldPoints;
			}
			else
			{
				progress.HungryLevel = nextLevel;
			}
		}

		progress.IncrementCount(lovedFood: false);
	}

	public static int GetFeedPointsForItemLevel(int itemLevel)
	{
		// Java parity: finalLevel adjustment and integer division used by updatePetFeedProgress.
		var finalLevel = itemLevel;
		if (finalLevel % 5 == 0)
		{
			finalLevel--;
		}

		var pointLevel = ItemLevels[finalLevel / 5];
		return Math.Max(0, pointLevel - 5) / 5 * 8;
	}

	public static PetFeedReward? GetReward(
		int fullCount,
		IReadOnlyList<int> fullCounts,
		IReadOnlyList<IReadOnlyList<int>> pointValues,
		IReadOnlyList<PetFeedReward> rewards,
		PetFeedProgress progress,
		int playerLevel,
		Func<IReadOnlyList<PetFeedReward>, PetFeedReward?> lovedRewardSelector)
	{
		ArgumentNullException.ThrowIfNull(fullCounts);
		ArgumentNullException.ThrowIfNull(pointValues);
		ArgumentNullException.ThrowIfNull(rewards);
		ArgumentNullException.ThrowIfNull(progress);
		ArgumentNullException.ThrowIfNull(lovedRewardSelector);

		// Java parity: services/toypet/PetFeedCalculator.getReward, with DataManager item levels and Rnd.get supplied by caller.
		if (progress.HungryLevel != PetHungryLevel.Full || rewards.Count == 0)
		{
			return null;
		}

		var pointsIndex = IndexOfFullCount(fullCounts, fullCount);
		if (pointsIndex < 0)
		{
			return null;
		}

		if (progress.IsLovedFeeded)
		{
			if (rewards.Count == 1)
			{
				return rewards[0];
			}

			var validRewards = new List<PetFeedReward>();
			var maxLevel = 0;
			foreach (var reward in rewards)
			{
				if (reward.ItemLevel > playerLevel)
				{
					continue;
				}

				if (reward.ItemLevel > maxLevel)
				{
					maxLevel = reward.ItemLevel;
					validRewards.Clear();
				}

				validRewards.Add(reward);
			}

			return validRewards.Count == 0 ? null : lovedRewardSelector(validRewards);
		}

		var rewardIndex = 0;
		var totalRewards = rewards.Count;
		for (var row = 1; row < pointValues.Count; row++)
		{
			var points = pointValues[row];
			if (points[pointsIndex] <= progress.TotalPoints)
			{
				rewardIndex = JavaRound((float)totalRewards / (pointValues.Count - 1) * row) - 1;
			}
		}

		if (rewardIndex < 0)
		{
			rewardIndex = 0;
		}
		else if (rewardIndex > rewards.Count - 1)
		{
			rewardIndex = rewards.Count - 1;
		}

		return rewards[rewardIndex];
	}

	private static byte[] CreateItemLevels()
	{
		var itemLevels = new byte[ItemMaxLevel / 5];
		itemLevels[0] = 5;
		for (var i = 1; i < itemLevels.Length; i++)
		{
			itemLevels[i] = (byte)(itemLevels[i - 1] + 5);
		}

		return itemLevels;
	}

	private static int IndexOfFullCount(IReadOnlyList<int> fullCounts, int fullCount)
	{
		for (var i = 0; i < fullCounts.Count; i++)
		{
			if (fullCounts[i] == fullCount)
			{
				return i;
			}
		}

		return -1;
	}

	private static int JavaRound(float value)
	{
		return (int)MathF.Floor(value + 0.5f);
	}
}
