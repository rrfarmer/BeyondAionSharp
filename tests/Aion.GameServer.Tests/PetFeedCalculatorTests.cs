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
}
