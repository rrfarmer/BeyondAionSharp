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
}
