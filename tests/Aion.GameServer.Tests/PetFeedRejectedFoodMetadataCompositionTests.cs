using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetFeedRejectedFoodMetadataCompositionTests
{
	[Theory]
	[InlineData(0, typeof(SmInventoryAddItem), typeof(SmCubeUpdate))]
	[InlineData(1, typeof(SmWarehouseAddItem), typeof(SmCubeUpdate))]
	[InlineData(2, typeof(SmWarehouseAddItem), typeof(SmCubeUpdate))]
	[InlineData(3, typeof(SmWarehouseAddItem), typeof(SmCubeUpdate))]
	public void Compose_RejectedFoodStorageUnlockContextFlowsIntoBridgeBeforePetPacketsLikeJava(
		int location,
		Type expectedUnlockPacket,
		Type expectedUpdatePacket)
	{
		var plan = CreateRejectedFoodPlan();
		var item = CreateItem(location, itemId: 188000001);
		var contextResult = new PetFeedUnlockPacketContextAssembler().Assemble(
			new PetFeedUnlockPacketContextAssemblerInput(
				item,
				CreateTemplate(item.ItemId),
				CubeItemsCount: 7,
				NpcExpands: 2,
				QuestExpands: 1,
				ItemExpands: 3,
				StorageItemsCount: 11,
				LegionWarehouseKinah: 123456));
		Assert.Equal(PetFeedUnlockPacketContextAssemblerStatus.Created, contextResult.Status);

		var bridgeResult = new PetFeedPacketMetadataBridge().Construct(
			new PetFeedPacketMetadataBridgeRequest(
				plan,
				FeedProgressData: 0x123450,
				SupplementalContext: new PetFeedSupplementalPacketContext(
					PlayerObjectId: 7001,
					PetName: "Tog",
					ItemName: "Stale Biscuit",
					UnlockPacketContext: contextResult.Context)));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, bridgeResult.Status);
		Assert.Equal(
			[
				PetFeedPacketMetadataResultStatus.Constructed,
				PetFeedPacketMetadataResultStatus.Constructed,
				PetFeedPacketMetadataResultStatus.Constructed,
				PetFeedPacketMetadataResultStatus.Constructed,
			],
			bridgeResult.Results.Select(result => result.Status).ToArray());
		Assert.IsType(expectedUnlockPacket, bridgeResult.Results[0].Packets[0]);
		Assert.IsType(expectedUpdatePacket, bridgeResult.Results[0].Packets[1]);
		Assert.IsType<SmPet>(bridgeResult.Results[1].Packet);
		Assert.IsType<SmEmotion>(bridgeResult.Results[2].Packet);
		var systemMessage = Assert.IsType<SmSystemMessage>(bridgeResult.Results[3].Packet);
		Assert.Equal(1400618, systemMessage.MessageId);
		Assert.False(bridgeResult.ExecutesLivePackets);
		Assert.False(bridgeResult.IsLive);
		Assert.False(bridgeResult.IsJavaRuntimeParity);
	}

	[Fact]
	public void Compose_RejectedFoodLegionKinahContextUsesLegionEditBeforePetPacketsLikeJava()
	{
		var plan = CreateRejectedFoodPlan();
		var item = CreateItem(location: 3, itemId: 182400001);
		var contextResult = new PetFeedUnlockPacketContextAssembler().Assemble(
			new PetFeedUnlockPacketContextAssemblerInput(
				item,
				Template: null,
				NpcExpands: 6,
				StorageItemsCount: 21,
				LegionWarehouseKinah: 456789));
		Assert.Equal(PetFeedUnlockPacketContextAssemblerStatus.Created, contextResult.Status);

		var bridgeResult = new PetFeedPacketMetadataBridge().Construct(
			new PetFeedPacketMetadataBridgeRequest(
				plan,
				FeedProgressData: 0x123450,
				SupplementalContext: new PetFeedSupplementalPacketContext(
					PlayerObjectId: 7001,
					PetName: "Tog",
					ItemName: "Kinah",
					UnlockPacketContext: contextResult.Context)));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, bridgeResult.Status);
		Assert.IsType<SmLegionEdit>(bridgeResult.Results[0].Packets[0]);
		Assert.IsType<SmCubeUpdate>(bridgeResult.Results[0].Packets[1]);
		Assert.IsType<SmPet>(bridgeResult.Results[1].Packet);
		Assert.IsType<SmEmotion>(bridgeResult.Results[2].Packet);
		Assert.IsType<SmSystemMessage>(bridgeResult.Results[3].Packet);
	}

	[Fact]
	public void Compose_RejectedFoodUnsupportedStorageKeepsUnlockBlockedWithoutGuessing()
	{
		var plan = CreateRejectedFoodPlan();
		var item = CreateItem(location: 32, itemId: 188000001);
		var contextResult = new PetFeedUnlockPacketContextAssembler().Assemble(
			new PetFeedUnlockPacketContextAssemblerInput(item, CreateTemplate(item.ItemId)));
		Assert.Equal(PetFeedUnlockPacketContextAssemblerStatus.UnsupportedStorageLocation, contextResult.Status);

		var bridgeResult = new PetFeedPacketMetadataBridge().Construct(
			new PetFeedPacketMetadataBridgeRequest(
				plan,
				FeedProgressData: 0x123450,
				SupplementalContext: new PetFeedSupplementalPacketContext(
					PlayerObjectId: 7001,
					PetName: "Tog",
					ItemName: "Stale Biscuit",
					UnlockPacketContext: contextResult.Context)));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.PartiallyConstructed, bridgeResult.Status);
		Assert.Equal(PetFeedPacketMetadataResultStatus.BlockedItemUnlockPacket, bridgeResult.Results[0].Status);
		Assert.Empty(bridgeResult.Results[0].Packets);
		Assert.IsType<SmPet>(bridgeResult.Results[1].Packet);
		Assert.IsType<SmEmotion>(bridgeResult.Results[2].Packet);
		Assert.IsType<SmSystemMessage>(bridgeResult.Results[3].Packet);
	}

	private static PetFeedServiceOperationPlan CreateRejectedFoodPlan()
	{
		return new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.RejectedFood,
			Evaluation: new PetFeedEvaluationResult(null, IsLovedFood: false, Reward: null),
			Operations:
			[
				new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: 5001),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedEndPacket),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendEndFeedingEmotion),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendFoodNotLovedSystemMessage, ItemId: 188000001),
			],
			RemainingRequestedCount: 1,
			RefeedTimeMilliseconds: null);
	}

	private static InventoryItem CreateItem(int location, int itemId)
	{
		return new InventoryItem
		{
			ObjectId = 5001,
			ItemId = itemId,
			Count = 2,
			OwnerId = 7001,
			Location = location,
			Slot = 9,
		};
	}

	private static ItemTemplateSummary CreateTemplate(int itemId)
	{
		return new ItemTemplateSummary(
			itemId,
			$"Item {itemId}",
			0,
			0,
			1,
			string.Empty,
			string.Empty,
			string.Empty,
			string.Empty,
			100,
			0,
			0);
	}
}
