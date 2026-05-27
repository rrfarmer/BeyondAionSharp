using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetFeedCalculatorTests
{
	[Theory]
	[InlineData(0, 10, 0)]
	[InlineData(8, 10, 80)]
	[InlineData(8, 25, 200)]
	[InlineData(88, 200, 17600)]
	public void GetPointsMatchesJavaPrecalculatedTableSamples(int feedPoints, int maxFeedCount, int expected)
	{
		Assert.Equal(expected, PetFeedCalculator.GetPoints(feedPoints, maxFeedCount));
	}

	[Theory]
	[InlineData(5, 0)]
	[InlineData(6, 8)]
	[InlineData(10, 8)]
	[InlineData(11, 16)]
	[InlineData(60, 88)]
	public void GetFeedPointsForItemLevelUsesJavaFiveLevelBuckets(int itemLevel, int expected)
	{
		Assert.Equal(expected, PetFeedCalculator.GetFeedPointsForItemLevel(itemLevel));
	}

	[Fact]
	public void UpdatePetFeedProgressAddsPointsAndIncrementsRegularCountLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);

		PetFeedCalculator.UpdatePetFeedProgress(progress, itemLevel: 6, maxFeedCount: 10);

		Assert.Equal(PetHungryLevel.Hungry, progress.HungryLevel);
		Assert.Equal(8, progress.TotalPoints);
		Assert.Equal(1, progress.RegularCount);
	}

	[Fact]
	public void UpdatePetFeedProgressSwitchesHungryLevelAfterJavaThreshold()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);
		progress.TotalPoints = 40;
		progress.SetRegularCount(6);

		PetFeedCalculator.UpdatePetFeedProgress(progress, itemLevel: 6, maxFeedCount: 10);

		Assert.Equal(PetHungryLevel.Content, progress.HungryLevel);
		Assert.Equal(40, progress.TotalPoints);
		Assert.Equal(7, progress.RegularCount);
	}

	[Fact]
	public void UpdatePetFeedProgressDoesNotSwitchAtExactHalfThresholdLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);
		progress.TotalPoints = 40;
		progress.SetRegularCount(5);

		PetFeedCalculator.UpdatePetFeedProgress(progress, itemLevel: 6, maxFeedCount: 10);

		Assert.Equal(PetHungryLevel.Hungry, progress.HungryLevel);
		Assert.Equal(48, progress.TotalPoints);
		Assert.Equal(6, progress.RegularCount);
	}

	[Fact]
	public void UpdatePetFeedProgressLovedFoodSetsFullAndConsumesLovedLimitLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 1);
		progress.SetIsLovedFeeded();

		PetFeedCalculator.UpdatePetFeedProgress(progress, itemLevel: 1, maxFeedCount: 1);

		Assert.Equal(PetHungryLevel.Full, progress.HungryLevel);
		Assert.Equal(0, progress.LovedFoodRemaining);
		Assert.Equal(0, progress.RegularCount);
	}

	[Fact]
	public void UpdatePetFeedProgressLovedFoodNoOpsWhenLovedLimitIsConsumedLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);
		progress.SetIsLovedFeeded();

		PetFeedCalculator.UpdatePetFeedProgress(progress, itemLevel: 1, maxFeedCount: 1);

		Assert.Equal(PetHungryLevel.Hungry, progress.HungryLevel);
		Assert.Equal(0, progress.LovedFoodRemaining);
		Assert.Equal(0, progress.RegularCount);
	}

	[Fact]
	public void CreatePointValuesMatchesJavaDocumentedTableSamples()
	{
		var pointValues = PetFeedCalculator.CreatePointValues([10, 25, 40, 50, 100, 200]);

		Assert.Equal([0, 0, 0, 0, 0, 0], pointValues[0]);
		Assert.Equal([80, 200, 320, 400, 800, 1600], pointValues[1]);
		Assert.Equal([880, 2200, 3520, 4400, 8800, 17600], pointValues[11]);
	}

	[Fact]
	public void GetRewardReturnsNullWhenNotFullNoRewardsOrUnknownFullCountLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);
		var pointValues = PetFeedCalculator.CreatePointValues([10]);
		var rewards = new[] { new PetFeedReward(1001, 10) };

		Assert.Null(PetFeedCalculator.GetReward(10, [10], pointValues, rewards, progress, playerLevel: 20, ThrowingLovedSelector));

		progress.HungryLevel = PetHungryLevel.Full;
		Assert.Null(PetFeedCalculator.GetReward(10, [10], pointValues, [], progress, playerLevel: 20, ThrowingLovedSelector));
		Assert.Null(PetFeedCalculator.GetReward(25, [10], pointValues, rewards, progress, playerLevel: 20, ThrowingLovedSelector));
	}

	[Fact]
	public void GetRewardSelectsNormalRewardIndexFromPointThresholdsLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0)
		{
			HungryLevel = PetHungryLevel.Full,
			TotalPoints = 8800,
		};
		var pointValues = PetFeedCalculator.CreatePointValues([10, 25, 40, 50, 100, 200]);
		var rewards = new[]
		{
			new PetFeedReward(1001, 10),
			new PetFeedReward(1002, 20),
			new PetFeedReward(1003, 30),
			new PetFeedReward(1004, 40),
			new PetFeedReward(1005, 50),
		};

		var reward = PetFeedCalculator.GetReward(100, [10, 25, 40, 50, 100, 200], pointValues, rewards, progress, playerLevel: 60, ThrowingLovedSelector);

		Assert.Equal(rewards[^1], reward);
	}

	[Fact]
	public void GetRewardClampsNormalRewardIndexToFirstLikeJavaRoundingFix()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0)
		{
			HungryLevel = PetHungryLevel.Full,
			TotalPoints = 0,
		};
		var pointValues = PetFeedCalculator.CreatePointValues([10]);
		var rewards = new[] { new PetFeedReward(1001, 10), new PetFeedReward(1002, 20) };

		var reward = PetFeedCalculator.GetReward(10, [10], pointValues, rewards, progress, playerLevel: 60, ThrowingLovedSelector);

		Assert.Equal(rewards[0], reward);
	}

	[Fact]
	public void GetRewardLovedFeedReturnsSingleRewardWithoutLevelFilteringLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 1)
		{
			HungryLevel = PetHungryLevel.Full,
		};
		progress.SetIsLovedFeeded();
		var pointValues = PetFeedCalculator.CreatePointValues([1]);
		var rewards = new[] { new PetFeedReward(1001, 99) };

		var reward = PetFeedCalculator.GetReward(1, [1], pointValues, rewards, progress, playerLevel: 1, ThrowingLovedSelector);

		Assert.Equal(rewards[0], reward);
	}

	[Fact]
	public void GetRewardLovedFeedFiltersToHighestAllowedItemLevelBeforeRandomChoiceLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 1)
		{
			HungryLevel = PetHungryLevel.Full,
		};
		progress.SetIsLovedFeeded();
		var pointValues = PetFeedCalculator.CreatePointValues([1]);
		var rewards = new[]
		{
			new PetFeedReward(1001, 10),
			new PetFeedReward(1002, 20),
			new PetFeedReward(1003, 30),
			new PetFeedReward(1004, 30),
			new PetFeedReward(1005, 40),
		};

		var reward = PetFeedCalculator.GetReward(
			1,
			[1],
			pointValues,
			rewards,
			progress,
			playerLevel: 30,
			validRewards =>
			{
				Assert.Equal([rewards[2], rewards[3]], validRewards);
				return validRewards[1];
			});

		Assert.Equal(rewards[3], reward);
	}

	private static PetFeedReward? ThrowingLovedSelector(IReadOnlyList<PetFeedReward> rewards)
	{
		throw new InvalidOperationException("Loved reward selector should not be called.");
	}
}
