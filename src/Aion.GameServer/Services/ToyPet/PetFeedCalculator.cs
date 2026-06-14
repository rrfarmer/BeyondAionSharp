namespace Aion.GameServer.Services.ToyPet;

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

			progress.HungryLevel = PetHungryLevel.FULL;
			progress.IncrementCount(lovedFood: true);
			return;
		}

		var oldPoints = progress.TotalPoints;
		var needSwitch = false;
		if ((currentHungryLevel == PetHungryLevel.HUNGRY && progress.RegularCount > maxFeedCount * 0.5f)
			|| (currentHungryLevel == PetHungryLevel.CONTENT && progress.RegularCount > maxFeedCount * 0.8f)
			|| (currentHungryLevel == PetHungryLevel.SEMIFULL && progress.RegularCount > maxFeedCount * 1.05))
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
			if ((nextLevel == PetHungryLevel.CONTENT && progress.RegularCount <= 0.487f * maxFeedCount)
				|| (nextLevel == PetHungryLevel.SEMIFULL && progress.RegularCount <= 0.78f * maxFeedCount))
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

	// Java parity: services/toypet/PetFeedCalculator static fullCounts/pointValues, built once from
	// DataManager.PET_FEED_DATA.getPetFlavours(). Lazily computed to avoid touching data at type-load time.
	private static readonly Lock FaithfulTablesSync = new();
	private static int[]? _faithfulFullCounts;
	private static int[][]? _faithfulPointValues;

	private static (int[] FullCounts, int[][] PointValues) GetFaithfulTables()
	{
		lock (FaithfulTablesSync)
		{
			if (_faithfulFullCounts != null && _faithfulPointValues != null)
				return (_faithfulFullCounts, _faithfulPointValues);

			var counts = new SortedSet<int>();
			foreach (var flavour in global::Aion.GameServer.Dataholders.DataManager.PET_FEED_DATA.GetPetFlavours())
			{
				if (flavour.GetFullCount() > 0)
					counts.Add(flavour.GetFullCount() & 0xFFFF);
			}

			_faithfulFullCounts = counts.ToArray();
			_faithfulPointValues = CreatePointValues(_faithfulFullCounts);
			return (_faithfulFullCounts, _faithfulPointValues);
		}
	}

	// Java parity: services/toypet/PetFeedCalculator.getReward(int, PetRewards, PetFeedProgress, int).
	public static global::Aion.GameServer.Model.Templates.Pet.PetFeedResult? GetReward(
		int fullCount,
		global::Aion.GameServer.Model.Templates.Pet.PetRewards rewardGroup,
		PetFeedProgress progress,
		int playerLevel)
	{
		ArgumentNullException.ThrowIfNull(rewardGroup);
		ArgumentNullException.ThrowIfNull(progress);

		var results = rewardGroup.GetResults();
		if (progress.HungryLevel != PetHungryLevel.FULL || results.Count == 0)
			return null;

		var (fullCounts, pointValues) = GetFaithfulTables();
		var pointsIndex = IndexOfFullCount(fullCounts, fullCount);
		if (pointsIndex < 0)
			return null;

		if (progress.IsLovedFeeded)
		{
			if (results.Count == 1)
				return results[0];

			var validRewards = new List<global::Aion.GameServer.Model.Templates.Pet.PetFeedResult>();
			var maxLevel = 0;
			foreach (var result in results)
			{
				var resultLevel = global::Aion.GameServer.Dataholders.DataManager.ITEM_DATA
					.GetItemTemplate(result.GetItem()).GetLevel();
				if (resultLevel > playerLevel)
					continue;
				if (resultLevel > maxLevel)
				{
					maxLevel = resultLevel;
					validRewards.Clear();
				}

				validRewards.Add(result);
			}

			return global::Aion.GameServer.Commons.Utils.Rnd.Get(validRewards);
		}

		var rewardIndex = 0;
		var totalRewards = results.Count;
		for (var row = 1; row < pointValues.Length; row++)
		{
			var points = pointValues[row];
			if (points[pointsIndex] <= progress.TotalPoints)
				rewardIndex = JavaRound((float)totalRewards / (pointValues.Length - 1) * row) - 1;
		}

		if (rewardIndex < 0)
			rewardIndex = 0;
		else if (rewardIndex > results.Count - 1)
			rewardIndex = results.Count - 1;

		return results[rewardIndex];
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
