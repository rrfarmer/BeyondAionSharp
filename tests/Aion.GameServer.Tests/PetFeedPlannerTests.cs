using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetFeedPlannerTests
{
	[Fact]
	public void ProcessFeedResultReturnsNullAndDoesNotMutateWhenFoodGroupMissingLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 1);

		var reward = PetFeedPlanner.ProcessFeedResult(
			progress,
			PetFoodType.Armor,
			itemLevel: 10,
			playerLevel: 60,
			fullCount: 10,
			rewardGroups: [],
			fullCounts: [10],
			pointValues: PetFeedCalculator.CreatePointValues([10]),
			ThrowingLovedSelector);

		Assert.Null(reward);
		Assert.Equal(PetHungryLevel.Hungry, progress.HungryLevel);
		Assert.Equal(0, progress.RegularCount);
		Assert.Equal(0, progress.TotalPoints);
		Assert.False(progress.IsLovedFeeded);
	}

	[Fact]
	public void ProcessFeedResultNormalFoodUpdatesProgressAndReturnsNullUntilFullLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);
		var rewards = new[]
		{
			new PetFeedRewardGroup(PetFoodType.Armor, IsLoved: false, [new PetFeedReward(1001, 10)]),
		};

		var reward = PetFeedPlanner.ProcessFeedResult(
			progress,
			PetFoodType.Armor,
			itemLevel: 6,
			playerLevel: 60,
			fullCount: 10,
			rewards,
			fullCounts: [10],
			pointValues: PetFeedCalculator.CreatePointValues([10]),
			ThrowingLovedSelector);

		Assert.Null(reward);
		Assert.Equal(PetHungryLevel.Hungry, progress.HungryLevel);
		Assert.Equal(1, progress.RegularCount);
		Assert.Equal(8, progress.TotalPoints);
		Assert.False(progress.IsLovedFeeded);
	}

	[Fact]
	public void ProcessFeedResultNormalFoodReturnsRewardAfterCalculatorReachesFullLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0)
		{
			HungryLevel = PetHungryLevel.Semifull,
			TotalPoints = 880,
		};
		progress.SetRegularCount(11);
		var pointValues = PetFeedCalculator.CreatePointValues([10]);
		var rewardGroup = new PetFeedRewardGroup(
			PetFoodType.Bones,
			IsLoved: false,
			[new PetFeedReward(1001, 10), new PetFeedReward(1002, 20)]);

		var reward = PetFeedPlanner.ProcessFeedResult(
			progress,
			PetFoodType.Bones,
			itemLevel: 6,
			playerLevel: 60,
			fullCount: 10,
			[rewardGroup],
			fullCounts: [10],
			pointValues,
			ThrowingLovedSelector);

		Assert.Equal(PetHungryLevel.Full, progress.HungryLevel);
		Assert.Equal(12, progress.RegularCount);
		Assert.Equal(rewardGroup.Results[^1], reward);
	}

	[Fact]
	public void ProcessFeedResultLovedFoodMarksLovedFeedsAndReturnsRewardLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 1);
		var pointValues = PetFeedCalculator.CreatePointValues([1]);
		var rewardGroup = new PetFeedRewardGroup(
			PetFoodType.PoppySnack,
			IsLoved: true,
			[new PetFeedReward(2001, 10), new PetFeedReward(2002, 10)]);

		var reward = PetFeedPlanner.ProcessFeedResult(
			progress,
			PetFoodType.PoppySnack,
			itemLevel: 1,
			playerLevel: 60,
			fullCount: 10,
			[rewardGroup],
			fullCounts: [1],
			pointValues,
			validRewards =>
			{
				Assert.Equal(rewardGroup.Results, validRewards);
				return validRewards[0];
			});

		Assert.Equal(PetHungryLevel.Full, progress.HungryLevel);
		Assert.True(progress.IsLovedFeeded);
		Assert.Equal(0, progress.LovedFoodRemaining);
		Assert.Equal(0, progress.RegularCount);
		Assert.Equal(rewardGroup.Results[0], reward);
	}

	[Fact]
	public void ProcessFeedResultLovedFoodReturnsNullWhenLovedLimitAlreadyConsumedLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);
		var rewardGroup = new PetFeedRewardGroup(PetFoodType.PoppySnack, IsLoved: true, [new PetFeedReward(2001, 10)]);

		var reward = PetFeedPlanner.ProcessFeedResult(
			progress,
			PetFoodType.PoppySnack,
			itemLevel: 1,
			playerLevel: 60,
			fullCount: 10,
			[rewardGroup],
			fullCounts: [1],
			pointValues: PetFeedCalculator.CreatePointValues([1]),
			ThrowingLovedSelector);

		Assert.Null(reward);
		Assert.True(progress.IsLovedFeeded);
		Assert.Equal(PetHungryLevel.Hungry, progress.HungryLevel);
		Assert.Equal(0, progress.LovedFoodRemaining);
	}

	[Fact]
	public void IsLovedFoodMatchesFirstRewardGroupTypeLikeJava()
	{
		var rewards = new[]
		{
			new PetFeedRewardGroup(PetFoodType.Armor, IsLoved: false, []),
			new PetFeedRewardGroup(PetFoodType.PoppySnack, IsLoved: true, []),
		};

		Assert.False(PetFeedPlanner.IsLovedFood(rewards, PetFoodType.Armor));
		Assert.True(PetFeedPlanner.IsLovedFood(rewards, PetFoodType.PoppySnack));
		Assert.False(PetFeedPlanner.IsLovedFood(rewards, PetFoodType.Bones));
	}

	private static PetFeedReward? ThrowingLovedSelector(IReadOnlyList<PetFeedReward> rewards)
	{
		throw new InvalidOperationException("Loved reward selector should not be called.");
	}
}
