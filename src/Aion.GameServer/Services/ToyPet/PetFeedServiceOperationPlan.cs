namespace Aion.GameServer.Services.ToyPet;

public enum PetFeedServiceOperationPlanStatus
{
	NoActionCancelled,
	RejectedFood,
	ConsumedContinue,
	ConsumedStop,
	Rewarded,
}

public enum PetFeedServiceOperationKind
{
	UnlockFoodItem,
	SendPetFeedEndPacket,
	SendEndFeedingEmotion,
	SendFoodNotLovedSystemMessage,
	DecreaseFoodItemCount,
	SendPetFeedProgressPacket,
	ScheduleNextFeedCheck,
	SendPetRewardItemPacket,
	SendPetRefeedPacket,
	AddRewardItem,
	ScheduleRefeed,
	SetRefeedTime,
	PersistRefeedTime,
	ResetFeedProgress,
}

public sealed record PetFeedServiceOperation(
	PetFeedServiceOperationKind Kind,
	int? ItemObjectId = null,
	int? ItemId = null,
	int? Count = null,
	long? TimeMilliseconds = null);

public sealed record PetFeedServiceOperationPlan(
	PetFeedServiceOperationPlanStatus Status,
	PetFeedEvaluationResult? Evaluation,
	IReadOnlyList<PetFeedServiceOperation> Operations,
	int RemainingRequestedCount,
	long? RefeedTimeMilliseconds);

public static class PetFeedServiceOperationPlanner
{
	public static PetFeedServiceOperationPlan CreatePlan(
		PetFeedEvaluationContext context,
		int flavourId,
		PetFeedProgress progress,
		int itemObjectId,
		int itemId,
		int requestedCount,
		int playerLevel,
		long currentTimeMilliseconds,
		bool cancelFeed,
		Func<IReadOnlyList<PetFeedReward>, PetFeedReward?> lovedRewardSelector)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(progress);
		ArgumentNullException.ThrowIfNull(lovedRewardSelector);

		// Java parity: PetService.checkFeeding exits without side effects when cancelFeed is already set.
		if (cancelFeed)
		{
			return new PetFeedServiceOperationPlan(
				PetFeedServiceOperationPlanStatus.NoActionCancelled,
				Evaluation: null,
				Operations: [],
				RemainingRequestedCount: requestedCount,
				RefeedTimeMilliseconds: null);
		}

		if (requestedCount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(requestedCount), requestedCount, "Java removeObject validates a positive feed count before scheduling.");
		}

		if (!context.Flavours.TryGetValue(flavourId, out var flavour))
		{
			throw new KeyNotFoundException($"Missing Java pet feed flavour id {flavourId}.");
		}

		var evaluation = PetFeedEvaluation.Evaluate(
			context,
			flavour,
			progress,
			itemId,
			playerLevel,
			lovedRewardSelector);

		if (evaluation.FoodType is null)
		{
			return new PetFeedServiceOperationPlan(
				PetFeedServiceOperationPlanStatus.RejectedFood,
				evaluation,
				[
					new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: itemObjectId),
					new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedEndPacket),
					new PetFeedServiceOperation(PetFeedServiceOperationKind.SendEndFeedingEmotion),
					new PetFeedServiceOperation(PetFeedServiceOperationKind.SendFoodNotLovedSystemMessage, ItemId: itemId),
				],
				RemainingRequestedCount: requestedCount,
				RefeedTimeMilliseconds: null);
		}

		var operations = new List<PetFeedServiceOperation>
		{
			// Java parity: inventory decrement happens after food acceptance and before process-feed result handling.
			new(PetFeedServiceOperationKind.DecreaseFoodItemCount, ItemObjectId: itemObjectId, Count: 1),
		};

		if (progress.HungryLevel == PetHungryLevel.Full && evaluation.Reward is { } reward)
		{
			var delayMilliseconds = flavour.CooldownSeconds * 60000L;
			var refeedTime = currentTimeMilliseconds + delayMilliseconds;
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedProgressPacket, ItemObjectId: itemObjectId, Count: 0));
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetRewardItemPacket, ItemId: reward.ItemId));
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedEndPacket));
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.SendEndFeedingEmotion));
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetRefeedPacket));
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.AddRewardItem, ItemId: reward.ItemId, Count: 1));
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.ScheduleRefeed, TimeMilliseconds: delayMilliseconds));
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.SetRefeedTime, TimeMilliseconds: refeedTime));
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.PersistRefeedTime, TimeMilliseconds: refeedTime));
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.ResetFeedProgress));
			return new PetFeedServiceOperationPlan(
				PetFeedServiceOperationPlanStatus.Rewarded,
				evaluation,
				operations,
				RemainingRequestedCount: 0,
				RefeedTimeMilliseconds: refeedTime);
		}

		var remainingCount = requestedCount - 1;
		operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedProgressPacket, ItemObjectId: itemObjectId, Count: remainingCount));
		if (remainingCount > 0)
		{
			operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.ScheduleNextFeedCheck, Count: remainingCount));
			return new PetFeedServiceOperationPlan(
				PetFeedServiceOperationPlanStatus.ConsumedContinue,
				evaluation,
				operations,
				remainingCount,
				RefeedTimeMilliseconds: null);
		}

		operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedEndPacket));
		operations.Add(new PetFeedServiceOperation(PetFeedServiceOperationKind.SendEndFeedingEmotion));
		return new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.ConsumedStop,
			evaluation,
			operations,
			remainingCount,
			RefeedTimeMilliseconds: null);
	}
}
