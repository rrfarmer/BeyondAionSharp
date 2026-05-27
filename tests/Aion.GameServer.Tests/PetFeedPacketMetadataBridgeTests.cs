using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetFeedPacketMetadataBridgeTests
{
	[Fact]
	public void Construct_NoOperationsReportsNoOperationsAndNeverLive()
	{
		var bridge = new PetFeedPacketMetadataBridge();

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			new PetFeedServiceOperationPlan(
				PetFeedServiceOperationPlanStatus.NoActionCancelled,
				Evaluation: null,
				Operations: [],
				RemainingRequestedCount: 1,
				RefeedTimeMilliseconds: null),
			FeedProgressData: 0x123450));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.NoOperations, result.Status);
		Assert.Empty(result.Results);
		Assert.False(result.ExecutesLivePackets);
		Assert.False(result.IsLive);
		Assert.False(result.IsJavaRuntimeParity);
	}

	[Fact]
	public void Construct_RejectedFoodBuildsSmPetEndAndReportsPacketGaps()
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.RejectedFood,
			Evaluation: new PetFeedEvaluationResult(null, IsLovedFood: false, Reward: null),
			Operations:
			[
				new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: 5001),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedEndPacket),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendEndFeedingEmotion),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendFoodNotLovedSystemMessage, ItemId: 9999),
			],
			RemainingRequestedCount: 1,
			RefeedTimeMilliseconds: null);

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(plan, FeedProgressData: 0x123450, RefeedDelaySeconds: 12));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.PartiallyConstructed, result.Status);
		Assert.Equal(
			[
				PetFeedPacketMetadataResultStatus.BlockedItemUnlockPacket,
				PetFeedPacketMetadataResultStatus.Constructed,
				PetFeedPacketMetadataResultStatus.BlockedEmotionContext,
				PetFeedPacketMetadataResultStatus.BlockedSystemMessageContext,
			],
			result.Results.Select(packet => packet.Status).ToArray());
		var smPet = Assert.IsType<SmPet>(result.Results[1].Packet);
		Assert.Equal(SmPet.PacketOpCode, smPet.OpCode);
		Assert.Contains("subtype 5", result.Results[1].Notes);
	}

	[Fact]
	public void Construct_RejectedFoodWithSuppliedContextBuildsEmotionAndSystemMessageMetadata()
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.RejectedFood,
			Evaluation: new PetFeedEvaluationResult(null, IsLovedFood: false, Reward: null),
			Operations:
			[
				new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: 5001),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedEndPacket),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendEndFeedingEmotion),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendFoodNotLovedSystemMessage, ItemId: 9999),
			],
			RemainingRequestedCount: 1,
			RefeedTimeMilliseconds: null);

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			plan,
			FeedProgressData: 0x123450,
			SupplementalContext: new PetFeedSupplementalPacketContext(
				PlayerObjectId: 7001,
				PetName: "Tog",
				ItemName: "Old Boot")));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.PartiallyConstructed, result.Status);
		Assert.Equal(PetFeedPacketMetadataResultStatus.BlockedItemUnlockPacket, result.Results[0].Status);
		Assert.IsType<SmPet>(result.Results[1].Packet);
		Assert.IsType<SmEmotion>(result.Results[2].Packet);
		var systemMessage = Assert.IsType<SmSystemMessage>(result.Results[3].Packet);
		Assert.Equal(1400618, systemMessage.MessageId);
		Assert.Contains("system-message", result.Results[3].Notes);
	}

	[Fact]
	public void Construct_NotFullContinueBuildsProgressPacketAndSkipsNonPacketOperations()
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.ConsumedContinue,
			Evaluation: new PetFeedEvaluationResult(PetFoodType.Armor, IsLovedFood: false, Reward: null),
			Operations:
			[
				new PetFeedServiceOperation(PetFeedServiceOperationKind.DecreaseFoodItemCount, ItemObjectId: 5001, Count: 1),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedProgressPacket, ItemObjectId: 5001, Count: 2),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.ScheduleNextFeedCheck, Count: 2),
			],
			RemainingRequestedCount: 2,
			RefeedTimeMilliseconds: null);

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(plan, FeedProgressData: 0x123450));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, result.Status);
		Assert.Equal(
			[
				PetFeedPacketMetadataResultStatus.SkippedNonPacketOperation,
				PetFeedPacketMetadataResultStatus.Constructed,
				PetFeedPacketMetadataResultStatus.SkippedNonPacketOperation,
			],
			result.Results.Select(packet => packet.Status).ToArray());
		Assert.IsType<SmPet>(result.Results[1].Packet);
		Assert.Contains("subtype 2", result.Results[1].Notes);
	}

	[Fact]
	public void Construct_RewardedFeedBuildsAllSmPetFeedPacketsAndMarksOtherBoundaries()
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.Rewarded,
			Evaluation: new PetFeedEvaluationResult(PetFoodType.Armor, IsLovedFood: false, Reward: new PetFeedReward(1003, 0)),
			Operations:
			[
				new PetFeedServiceOperation(PetFeedServiceOperationKind.DecreaseFoodItemCount, ItemObjectId: 5001, Count: 1),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedProgressPacket, ItemObjectId: 5001, Count: 0),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetRewardItemPacket, ItemId: 1003),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedEndPacket),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendEndFeedingEmotion),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetRefeedPacket),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.AddRewardItem, ItemId: 1003, Count: 1),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.ScheduleRefeed, TimeMilliseconds: 60000),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SetRefeedTime, TimeMilliseconds: 160000),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.PersistRefeedTime, TimeMilliseconds: 160000),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.ResetFeedProgress),
			],
			RemainingRequestedCount: 0,
			RefeedTimeMilliseconds: 160000);

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(plan, FeedProgressData: 0x123450, RefeedDelaySeconds: 60));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.PartiallyConstructed, result.Status);
		Assert.Equal([1, 2, 3, 5], result.Results
			.Select((metadata, index) => (metadata, index))
			.Where(entry => entry.metadata.Status == PetFeedPacketMetadataResultStatus.Constructed)
			.Select(entry => entry.index)
			.ToArray());
		Assert.All(
			result.Results.Where(metadata => metadata.Status == PetFeedPacketMetadataResultStatus.Constructed),
			metadata => Assert.Equal(SmPet.PacketOpCode, Assert.IsType<SmPet>(metadata.Packet).OpCode));
		Assert.Equal(PetFeedPacketMetadataResultStatus.BlockedEmotionContext, result.Results[4].Status);
		Assert.Equal(6, result.Results.Count(metadata => metadata.Status == PetFeedPacketMetadataResultStatus.SkippedNonPacketOperation));
	}

	[Fact]
	public void Construct_RewardedFeedWithSuppliedPlayerContextBuildsEndFeedingEmotionMetadata()
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.Rewarded,
			Evaluation: new PetFeedEvaluationResult(PetFoodType.Armor, IsLovedFood: false, Reward: new PetFeedReward(1003, 0)),
			Operations:
			[
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedProgressPacket, ItemObjectId: 5001, Count: 0),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendEndFeedingEmotion),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetRefeedPacket),
			],
			RemainingRequestedCount: 0,
			RefeedTimeMilliseconds: 160000);

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			plan,
			FeedProgressData: 0x123450,
			RefeedDelaySeconds: 60,
			SupplementalContext: new PetFeedSupplementalPacketContext(PlayerObjectId: 7001)));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, result.Status);
		Assert.IsType<SmPet>(result.Results[0].Packet);
		Assert.IsType<SmEmotion>(result.Results[1].Packet);
		Assert.IsType<SmPet>(result.Results[2].Packet);
		Assert.All(result.Results, metadata => Assert.Equal(PetFeedPacketMetadataResultStatus.Constructed, metadata.Status));
	}
}
