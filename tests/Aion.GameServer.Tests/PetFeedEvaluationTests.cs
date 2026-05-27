using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetFeedEvaluationTests
{
	[Fact]
	public void EvaluateReturnsNoMatchAndDoesNotMutateWhenItemIsNotFoodLikeJavaServiceGate()
	{
		var context = CreateContext();
		var progress = new PetFeedProgress(lovedFoodLimit: 1);

		var result = PetFeedEvaluation.Evaluate(context, flavourId: 7, progress, itemId: 9999, playerLevel: 60, ThrowingLovedSelector);

		Assert.Null(result.FoodType);
		Assert.False(result.IsLovedFood);
		Assert.Null(result.Reward);
		Assert.Equal(PetHungryLevel.Hungry, progress.HungryLevel);
		Assert.Equal(0, progress.RegularCount);
		Assert.Equal(0, progress.TotalPoints);
		Assert.False(progress.IsLovedFeeded);
	}

	[Fact]
	public void EvaluateLovedFoodWithNoRemainingLimitReturnsNoMatchAndDoesNotMutateLikeJavaServiceGate()
	{
		var context = CreateContext();
		var progress = new PetFeedProgress(lovedFoodLimit: 0);

		var result = PetFeedEvaluation.Evaluate(context, flavourId: 7, progress, itemId: 3001, playerLevel: 60, ThrowingLovedSelector);

		Assert.Null(result.FoodType);
		Assert.True(result.IsLovedFood);
		Assert.Null(result.Reward);
		Assert.Equal(PetHungryLevel.Hungry, progress.HungryLevel);
		Assert.Equal(0, progress.LovedFoodRemaining);
		Assert.False(progress.IsLovedFeeded);
	}

	[Fact]
	public void EvaluateNormalFoodUsesSuppliedItemLevelAndReturnsNullUntilFullLikeJava()
	{
		var context = CreateContext();
		var progress = new PetFeedProgress(lovedFoodLimit: 1);

		var result = PetFeedEvaluation.Evaluate(context, flavourId: 7, progress, itemId: 1001, playerLevel: 60, ThrowingLovedSelector);

		Assert.Equal(PetFoodType.Armor, result.FoodType);
		Assert.False(result.IsLovedFood);
		Assert.Null(result.Reward);
		Assert.Equal(PetHungryLevel.Hungry, progress.HungryLevel);
		Assert.Equal(1, progress.RegularCount);
		Assert.Equal(8, progress.TotalPoints);
	}

	[Fact]
	public void EvaluateNormalFoodReturnsRewardAfterFullTransitionLikeJava()
	{
		var context = CreateContext();
		var progress = new PetFeedProgress(lovedFoodLimit: 1)
		{
			HungryLevel = PetHungryLevel.Semifull,
			TotalPoints = 880,
		};
		progress.SetRegularCount(11);

		var result = PetFeedEvaluation.Evaluate(context, flavourId: 7, progress, itemId: 1001, playerLevel: 60, ThrowingLovedSelector);

		Assert.Equal(PetFoodType.Armor, result.FoodType);
		Assert.False(result.IsLovedFood);
		Assert.Equal(PetHungryLevel.Full, progress.HungryLevel);
		Assert.Equal(12, progress.RegularCount);
		Assert.Equal(1003, result.Reward?.ItemId);
	}

	[Fact]
	public void EvaluateLovedFoodResolvesRewardItemLevelsBeforeSelectorLikeJava()
	{
		var context = CreateContext();
		var progress = new PetFeedProgress(lovedFoodLimit: 1);

		var result = PetFeedEvaluation.Evaluate(
			context,
			flavourId: 7,
			progress,
			itemId: 3001,
			playerLevel: 30,
			validRewards =>
			{
				var reward = Assert.Single(validRewards);
				Assert.Equal(2002, reward.ItemId);
				Assert.Equal(30, reward.ItemLevel);
				return reward;
			});

		Assert.Equal(PetFoodType.PoppySnack, result.FoodType);
		Assert.True(result.IsLovedFood);
		Assert.Equal(PetHungryLevel.Full, progress.HungryLevel);
		Assert.Equal(0, progress.LovedFoodRemaining);
		Assert.Equal(2002, result.Reward?.ItemId);
	}

	[Fact]
	public void EvaluateThrowsWhenFedItemLevelIsMissingInsteadOfAssumingParity()
	{
		var context = CreateContext(itemLevels: new Dictionary<int, int>());
		var progress = new PetFeedProgress(lovedFoodLimit: 1);

		var exception = Assert.Throws<KeyNotFoundException>(() =>
			PetFeedEvaluation.Evaluate(context, flavourId: 7, progress, itemId: 1001, playerLevel: 60, ThrowingLovedSelector));

		Assert.Contains("fed item id 1001", exception.Message);
	}

	[Fact]
	public void EvaluateThrowsWhenLovedRewardItemLevelIsMissingInsteadOfAssumingParity()
	{
		var context = CreateContext(itemLevels: new Dictionary<int, int>
		{
			[1001] = 6,
			[3001] = 1,
			[2001] = 10,
		});
		var progress = new PetFeedProgress(lovedFoodLimit: 1);

		var exception = Assert.Throws<KeyNotFoundException>(() =>
			PetFeedEvaluation.Evaluate(context, flavourId: 7, progress, itemId: 3001, playerLevel: 60, ThrowingLovedSelector));

		Assert.Contains("loved reward item id 2002", exception.Message);
	}

	[Fact]
	public void EvaluationContextBuildsFullCountTablesFromProjectedFlavours()
	{
		var context = CreateContext();

		Assert.Equal([1, 10, 25], context.FullCounts);
		Assert.Equal(12, context.PointValues.Count);
		Assert.Equal(3, context.PointValues[1].Count);
	}

	private static PetFeedEvaluationContext CreateContext(IReadOnlyDictionary<int, int>? itemLevels = null)
	{
		var flavours = new Dictionary<int, PetFeedFlavourProjection>
		{
			[7] = new PetFeedFlavourProjection(
				Id: 7,
				FullCount: 10,
				LovedFoodLimit: 1,
				CooldownSeconds: 60,
				[
					new PetFeedRewardGroup(
						PetFoodType.Armor,
						IsLoved: false,
						[
							new PetFeedReward(1002, 0),
							new PetFeedReward(1003, 0),
						]),
					new PetFeedRewardGroup(
						PetFoodType.PoppySnack,
						IsLoved: true,
						[
							new PetFeedReward(2001, 0),
							new PetFeedReward(2002, 0),
							new PetFeedReward(2003, 0),
						]),
				]),
			[8] = new PetFeedFlavourProjection(
				Id: 8,
				FullCount: 25,
				LovedFoodLimit: 0,
				CooldownSeconds: 120,
				[new PetFeedRewardGroup(PetFoodType.Bones, IsLoved: false, [new PetFeedReward(4001, 0)])]),
			[9] = new PetFeedFlavourProjection(
				Id: 9,
				FullCount: 1,
				LovedFoodLimit: 0,
				CooldownSeconds: 30,
				[new PetFeedRewardGroup(PetFoodType.Fluids, IsLoved: false, [new PetFeedReward(5001, 0)])]),
		};
		var itemGroups = PetFoodItemGroups.From(
			(PetFoodType.Armor, Set(1001)),
			(PetFoodType.PoppySnack, Set(3001)));

		itemLevels ??= new Dictionary<int, int>
		{
			[1001] = 6,
			[3001] = 1,
			[2001] = 10,
			[2002] = 30,
			[2003] = 40,
		};

		return new PetFeedEvaluationContext(flavours, itemGroups, itemLevels);
	}

	private static IReadOnlySet<int> Set(params int[] itemIds)
	{
		return new HashSet<int>(itemIds);
	}

	private static PetFeedReward? ThrowingLovedSelector(IReadOnlyList<PetFeedReward> rewards)
	{
		throw new InvalidOperationException("Loved reward selector should not be called.");
	}
}
