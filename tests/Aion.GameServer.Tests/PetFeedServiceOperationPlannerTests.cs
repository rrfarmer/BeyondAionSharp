using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetFeedServiceOperationPlannerTests
{
	[Fact]
	public void CreatePlan_CancelledFeedReturnsNoOperationsLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 1);

		var plan = PetFeedServiceOperationPlanner.CreatePlan(
			CreateContext(),
			flavourId: 7,
			progress,
			itemObjectId: 5001,
			itemId: 1001,
			requestedCount: 3,
			playerLevel: 60,
			currentTimeMilliseconds: 100000,
			cancelFeed: true,
			ThrowingLovedSelector);

		Assert.Equal(PetFeedServiceOperationPlanStatus.NoActionCancelled, plan.Status);
		Assert.Null(plan.Evaluation);
		Assert.Empty(plan.Operations);
		Assert.Equal(3, plan.RemainingRequestedCount);
		Assert.Equal(PetHungryLevel.Hungry, progress.HungryLevel);
	}

	[Fact]
	public void CreatePlan_RejectedFoodPlansUnlockEndAndSystemMessageInJavaOrder()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 1);

		var plan = PetFeedServiceOperationPlanner.CreatePlan(
			CreateContext(),
			flavourId: 7,
			progress,
			itemObjectId: 5001,
			itemId: 9999,
			requestedCount: 2,
			playerLevel: 60,
			currentTimeMilliseconds: 100000,
			cancelFeed: false,
			ThrowingLovedSelector);

		Assert.Equal(PetFeedServiceOperationPlanStatus.RejectedFood, plan.Status);
		Assert.Null(plan.Evaluation?.FoodType);
		Assert.Equal(2, plan.RemainingRequestedCount);
		Assert.Equal(
			[
				PetFeedServiceOperationKind.UnlockFoodItem,
				PetFeedServiceOperationKind.SendPetFeedEndPacket,
				PetFeedServiceOperationKind.SendEndFeedingEmotion,
				PetFeedServiceOperationKind.SendFoodNotLovedSystemMessage,
			],
			plan.Operations.Select(operation => operation.Kind).ToArray());
		Assert.Equal(5001, plan.Operations[0].ItemObjectId);
		Assert.Equal(9999, plan.Operations[3].ItemId);
		Assert.Equal(0, progress.RegularCount);
	}

	[Fact]
	public void CreatePlan_LovedLimitRejectedFoodDoesNotDecreaseItemLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);

		var plan = PetFeedServiceOperationPlanner.CreatePlan(
			CreateContext(),
			flavourId: 7,
			progress,
			itemObjectId: 5002,
			itemId: 3001,
			requestedCount: 1,
			playerLevel: 60,
			currentTimeMilliseconds: 100000,
			cancelFeed: false,
			ThrowingLovedSelector);

		Assert.Equal(PetFeedServiceOperationPlanStatus.RejectedFood, plan.Status);
		Assert.True(plan.Evaluation?.IsLovedFood);
		Assert.DoesNotContain(plan.Operations, operation => operation.Kind == PetFeedServiceOperationKind.DecreaseFoodItemCount);
		Assert.False(progress.IsLovedFeeded);
	}

	[Fact]
	public void CreatePlan_ConsumedNotFullWithRemainingCountPlansDecreaseProgressAndRescheduleLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 1);

		var plan = PetFeedServiceOperationPlanner.CreatePlan(
			CreateContext(),
			flavourId: 7,
			progress,
			itemObjectId: 5001,
			itemId: 1001,
			requestedCount: 3,
			playerLevel: 60,
			currentTimeMilliseconds: 100000,
			cancelFeed: false,
			ThrowingLovedSelector);

		Assert.Equal(PetFeedServiceOperationPlanStatus.ConsumedContinue, plan.Status);
		Assert.Equal(PetFoodType.Armor, plan.Evaluation?.FoodType);
		Assert.Equal(2, plan.RemainingRequestedCount);
		Assert.Equal(
			[
				PetFeedServiceOperationKind.DecreaseFoodItemCount,
				PetFeedServiceOperationKind.SendPetFeedProgressPacket,
				PetFeedServiceOperationKind.ScheduleNextFeedCheck,
			],
			plan.Operations.Select(operation => operation.Kind).ToArray());
		Assert.Equal(1, plan.Operations[0].Count);
		Assert.Equal(2, plan.Operations[1].Count);
		Assert.Equal(2, plan.Operations[2].Count);
		Assert.Equal(1, progress.RegularCount);
		Assert.Equal(8, progress.TotalPoints);
	}

	[Fact]
	public void CreatePlan_ConsumedLastRequestedFoodPlansEndFeedingLikeJava()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 1);

		var plan = PetFeedServiceOperationPlanner.CreatePlan(
			CreateContext(),
			flavourId: 7,
			progress,
			itemObjectId: 5001,
			itemId: 1001,
			requestedCount: 1,
			playerLevel: 60,
			currentTimeMilliseconds: 100000,
			cancelFeed: false,
			ThrowingLovedSelector);

		Assert.Equal(PetFeedServiceOperationPlanStatus.ConsumedStop, plan.Status);
		Assert.Equal(0, plan.RemainingRequestedCount);
		Assert.Equal(
			[
				PetFeedServiceOperationKind.DecreaseFoodItemCount,
				PetFeedServiceOperationKind.SendPetFeedProgressPacket,
				PetFeedServiceOperationKind.SendPetFeedEndPacket,
				PetFeedServiceOperationKind.SendEndFeedingEmotion,
			],
			plan.Operations.Select(operation => operation.Kind).ToArray());
		Assert.Equal(0, plan.Operations[1].Count);
	}

	[Fact]
	public void CreatePlan_RewardedFeedPlansRewardCooldownDaoAndResetInJavaOrder()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 1)
		{
			HungryLevel = PetHungryLevel.Semifull,
			TotalPoints = 880,
		};
		progress.SetRegularCount(11);

		var plan = PetFeedServiceOperationPlanner.CreatePlan(
			CreateContext(),
			flavourId: 7,
			progress,
			itemObjectId: 5001,
			itemId: 1001,
			requestedCount: 5,
			playerLevel: 60,
			currentTimeMilliseconds: 100000,
			cancelFeed: false,
			ThrowingLovedSelector);

		Assert.Equal(PetFeedServiceOperationPlanStatus.Rewarded, plan.Status);
		Assert.Equal(0, plan.RemainingRequestedCount);
		Assert.Equal(1003, plan.Evaluation?.Reward?.ItemId);
		Assert.Equal(100000 + 60 * 60000L, plan.RefeedTimeMilliseconds);
		Assert.Equal(
			[
				PetFeedServiceOperationKind.DecreaseFoodItemCount,
				PetFeedServiceOperationKind.SendPetFeedProgressPacket,
				PetFeedServiceOperationKind.SendPetRewardItemPacket,
				PetFeedServiceOperationKind.SendPetFeedEndPacket,
				PetFeedServiceOperationKind.SendEndFeedingEmotion,
				PetFeedServiceOperationKind.SendPetRefeedPacket,
				PetFeedServiceOperationKind.AddRewardItem,
				PetFeedServiceOperationKind.ScheduleRefeed,
				PetFeedServiceOperationKind.SetRefeedTime,
				PetFeedServiceOperationKind.PersistRefeedTime,
				PetFeedServiceOperationKind.ResetFeedProgress,
			],
			plan.Operations.Select(operation => operation.Kind).ToArray());
		Assert.Equal(0, plan.Operations[1].Count);
		Assert.Equal(1003, plan.Operations[2].ItemId);
		Assert.Equal(1003, plan.Operations[6].ItemId);
		Assert.Equal(60 * 60000L, plan.Operations[7].TimeMilliseconds);
		Assert.Equal(plan.RefeedTimeMilliseconds, plan.Operations[8].TimeMilliseconds);
		Assert.Equal(plan.RefeedTimeMilliseconds, plan.Operations[9].TimeMilliseconds);
	}

	private static PetFeedEvaluationContext CreateContext()
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
						[new PetFeedReward(2001, 0)]),
				]),
			[8] = new PetFeedFlavourProjection(
				Id: 8,
				FullCount: 1,
				LovedFoodLimit: 0,
				CooldownSeconds: 30,
				[new PetFeedRewardGroup(PetFoodType.Fluids, IsLoved: false, [new PetFeedReward(4001, 0)])]),
		};
		var itemGroups = PetFoodItemGroups.From(
			(PetFoodType.Armor, Set(1001)),
			(PetFoodType.PoppySnack, Set(3001)));
		var itemLevels = new Dictionary<int, int>
		{
			[1001] = 6,
			[3001] = 1,
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
