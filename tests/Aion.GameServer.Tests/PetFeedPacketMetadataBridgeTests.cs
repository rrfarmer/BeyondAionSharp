using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
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
	public void Construct_RejectedFoodWithNormalCubeUnlockContextBuildsUnlockPacketsBeforeSmPetLikeJava()
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

		var item = new InventoryItem
		{
			ObjectId = 5001,
			ItemId = 188000001,
			Count = 2,
			OwnerId = 7001,
			Location = 0,
			Slot = 9,
		};
		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			plan,
			FeedProgressData: 0x123450,
			SupplementalContext: new PetFeedSupplementalPacketContext(
				PlayerObjectId: 7001,
				PetName: "Tog",
				ItemName: "Spiced Snack",
				UnlockPacketContext: new PetFeedUnlockPacketContext(
					PetFeedUnlockPacketStorageKind.Cube,
					item,
					CreateTemplate(item.ItemId, "Spiced Snack"),
					CubeItemsCount: 7,
					NpcExpands: 2,
					QuestExpands: 1,
					ItemExpands: 3))));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, result.Status);
		Assert.All(result.Results, metadata => Assert.Equal(PetFeedPacketMetadataResultStatus.Constructed, metadata.Status));
		Assert.Equal([2, 1, 1, 1], result.Results.Select(metadata => metadata.Packets.Count).ToArray());
		var inventoryAdd = Assert.IsType<SmInventoryAddItem>(result.Results[0].Packets[0]);
		var cubeUpdate = Assert.IsType<SmCubeUpdate>(result.Results[0].Packets[1]);
		Assert.Same(inventoryAdd, result.Results[0].Packet);
		Assert.IsType<SmPet>(result.Results[1].Packet);

		using (var inventoryReader = new PacketBuffer(SerializeUnencryptedPayload(inventoryAdd)))
		{
			Assert.Equal(SmInventoryAddItem.AllSlot, inventoryReader.ReadH());
			Assert.Equal(1, inventoryReader.ReadH());
			Assert.Equal(5001, inventoryReader.ReadD());
			Assert.Equal(188000001, inventoryReader.ReadD());
			Assert.Equal(string.Empty, inventoryReader.ReadS());
			var blobSize = inventoryReader.ReadH();
			Assert.True(blobSize > 0);
			inventoryReader.ReadB(blobSize);
			Assert.Equal(9, inventoryReader.ReadH());
			Assert.Equal(0, (int)inventoryReader.ReadC());
			Assert.Equal(0, inventoryReader.Remaining);
		}

		using var cubeReader = new PacketBuffer(SerializeUnencryptedPayload(cubeUpdate));
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(7, cubeReader.ReadD());
		Assert.Equal(2, (int)cubeReader.ReadC());
		Assert.Equal(1, (int)cubeReader.ReadC());
		Assert.Equal(3, (int)cubeReader.ReadC());
		Assert.Equal(0, cubeReader.Remaining);
	}

	[Fact]
	public void Construct_RejectedFoodWithRegularWarehouseUnlockContextBuildsWarehousePacketsBeforeSmPetLikeJava()
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.RejectedFood,
			Evaluation: new PetFeedEvaluationResult(null, IsLovedFood: false, Reward: null),
			Operations:
			[
				new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: 5001),
				new PetFeedServiceOperation(PetFeedServiceOperationKind.SendPetFeedEndPacket),
			],
			RemainingRequestedCount: 1,
			RefeedTimeMilliseconds: null);
		var item = new InventoryItem
		{
			ObjectId = 5001,
			ItemId = 188000001,
			Count = 2,
			OwnerId = 7001,
			Location = SmWarehouseAddItem.RegularWarehouse,
			Slot = 12,
		};

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			plan,
			FeedProgressData: 0x123450,
			SupplementalContext: new PetFeedSupplementalPacketContext(
				UnlockPacketContext: new PetFeedUnlockPacketContext(
					PetFeedUnlockPacketStorageKind.Warehouse,
					item,
					CreateTemplate(item.ItemId, "Warehouse Snack"),
					NpcExpands: 4,
					QuestExpands: 2,
					StorageItemsCount: 11))));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, result.Status);
		Assert.Equal([2, 1], result.Results.Select(metadata => metadata.Packets.Count).ToArray());
		var warehouseAdd = Assert.IsType<SmWarehouseAddItem>(result.Results[0].Packets[0]);
		var cubeUpdate = Assert.IsType<SmCubeUpdate>(result.Results[0].Packets[1]);
		Assert.IsType<SmPet>(result.Results[1].Packet);

		using (var warehouseReader = new PacketBuffer(SerializeUnencryptedPayload(warehouseAdd)))
		{
			Assert.Equal(SmWarehouseAddItem.RegularWarehouse, (int)warehouseReader.ReadC());
			Assert.Equal(SmWarehouseAddItem.AllSlot, warehouseReader.ReadH());
			Assert.Equal(1, warehouseReader.ReadH());
			Assert.Equal(5001, warehouseReader.ReadD());
			Assert.Equal(188000001, warehouseReader.ReadD());
			Assert.Equal(0, (int)warehouseReader.ReadC());
			Assert.Equal(string.Empty, warehouseReader.ReadS());
			var blobSize = warehouseReader.ReadH();
			Assert.True(blobSize > 0);
			warehouseReader.ReadB(blobSize);
			Assert.Equal(12, warehouseReader.ReadH());
			Assert.Equal(0, warehouseReader.Remaining);
		}

		using var cubeReader = new PacketBuffer(SerializeUnencryptedPayload(cubeUpdate));
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(1, (int)cubeReader.ReadC());
		Assert.Equal(11, cubeReader.ReadD());
		Assert.Equal(4, (int)cubeReader.ReadC());
		Assert.Equal(2, (int)cubeReader.ReadC());
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(0, cubeReader.Remaining);
	}

	[Fact]
	public void Construct_RejectedFoodWithAccountWarehouseUnlockContextUsesJavaZeroCubeUpdate()
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.RejectedFood,
			Evaluation: new PetFeedEvaluationResult(null, IsLovedFood: false, Reward: null),
			Operations: [new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: 5001)],
			RemainingRequestedCount: 1,
			RefeedTimeMilliseconds: null);
		var item = new InventoryItem { ObjectId = 5001, ItemId = 188000001, Count = 2, OwnerId = 7001, Location = SmWarehouseAddItem.AccountWarehouse, Slot = 3 };

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			plan,
			FeedProgressData: 0x123450,
			SupplementalContext: new PetFeedSupplementalPacketContext(
				UnlockPacketContext: new PetFeedUnlockPacketContext(
					PetFeedUnlockPacketStorageKind.AccountWarehouse,
					item,
					CreateTemplate(item.ItemId, "Account Snack"),
					StorageItemsCount: 99))));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, result.Status);
		var warehouseAdd = Assert.IsType<SmWarehouseAddItem>(result.Results[0].Packets[0]);
		var cubeUpdate = Assert.IsType<SmCubeUpdate>(result.Results[0].Packets[1]);

		using (var warehouseReader = new PacketBuffer(SerializeUnencryptedPayload(warehouseAdd)))
		{
			Assert.Equal(SmWarehouseAddItem.AccountWarehouse, (int)warehouseReader.ReadC());
			Assert.Equal(SmWarehouseAddItem.AllSlot, warehouseReader.ReadH());
		}

		Assert.Equal(
			Convert.FromHexString("000200000000000000"),
			SerializeUnencryptedPayload(cubeUpdate));
	}

	[Fact]
	public void Construct_RejectedFoodWithLegionWarehouseItemUnlockContextBuildsWarehousePacketsLikeJava()
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.RejectedFood,
			Evaluation: new PetFeedEvaluationResult(null, IsLovedFood: false, Reward: null),
			Operations: [new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: 5001)],
			RemainingRequestedCount: 1,
			RefeedTimeMilliseconds: null);
		var item = new InventoryItem { ObjectId = 5001, ItemId = 188000001, Count = 2, OwnerId = 7001, Location = SmWarehouseAddItem.LegionWarehouse, Slot = 4 };

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			plan,
			FeedProgressData: 0x123450,
			SupplementalContext: new PetFeedSupplementalPacketContext(
				UnlockPacketContext: new PetFeedUnlockPacketContext(
					PetFeedUnlockPacketStorageKind.LegionWarehouse,
					item,
					CreateTemplate(item.ItemId, "Legion Snack"),
					NpcExpands: 6,
					StorageItemsCount: 21))));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, result.Status);
		var warehouseAdd = Assert.IsType<SmWarehouseAddItem>(result.Results[0].Packets[0]);
		var cubeUpdate = Assert.IsType<SmCubeUpdate>(result.Results[0].Packets[1]);

		using (var warehouseReader = new PacketBuffer(SerializeUnencryptedPayload(warehouseAdd)))
		{
			Assert.Equal(SmWarehouseAddItem.LegionWarehouse, (int)warehouseReader.ReadC());
			Assert.Equal(SmWarehouseAddItem.AllSlot, warehouseReader.ReadH());
			Assert.Equal(1, warehouseReader.ReadH());
		}

		using var cubeReader = new PacketBuffer(SerializeUnencryptedPayload(cubeUpdate));
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(3, (int)cubeReader.ReadC());
		Assert.Equal(21, cubeReader.ReadD());
		Assert.Equal(6, (int)cubeReader.ReadC());
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(0, cubeReader.Remaining);
	}

	[Fact]
	public void Construct_RejectedFoodWithLegionWarehouseKinahUnlockContextBuildsLegionEditLikeJava()
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.RejectedFood,
			Evaluation: new PetFeedEvaluationResult(null, IsLovedFood: false, Reward: null),
			Operations: [new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: 5001)],
			RemainingRequestedCount: 1,
			RefeedTimeMilliseconds: null);

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			plan,
			FeedProgressData: 0x123450,
			SupplementalContext: new PetFeedSupplementalPacketContext(
				UnlockPacketContext: new PetFeedUnlockPacketContext(
					PetFeedUnlockPacketStorageKind.LegionWarehouse,
					NpcExpands: 7,
					StorageItemsCount: 22,
					IsKinah: true,
					LegionWarehouseKinah: 123456))));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, result.Status);
		var legionEdit = Assert.IsType<SmLegionEdit>(result.Results[0].Packets[0]);
		var cubeUpdate = Assert.IsType<SmCubeUpdate>(result.Results[0].Packets[1]);

		using (var legionReader = new PacketBuffer(SerializeUnencryptedPayload(legionEdit)))
		{
			Assert.Equal(4, (int)legionReader.ReadC());
			Assert.Equal(123456, legionReader.ReadQ());
			Assert.Equal(0, legionReader.Remaining);
		}

		using var cubeReader = new PacketBuffer(SerializeUnencryptedPayload(cubeUpdate));
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(3, (int)cubeReader.ReadC());
		Assert.Equal(22, cubeReader.ReadD());
		Assert.Equal(7, (int)cubeReader.ReadC());
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(0, cubeReader.Remaining);
	}

	[Theory]
	[InlineData(32, 4)]
	[InlineData(33, 5)]
	[InlineData(34, 6)]
	[InlineData(35, 7)]
	[InlineData(36, 8)]
	[InlineData(37, 9)]
	[InlineData(38, 10)]
	[InlineData(39, 11)]
	[InlineData(40, 12)]
	[InlineData(41, 13)]
	[InlineData(42, 14)]
	[InlineData(43, 15)]
	[InlineData(60, 16)]
	[InlineData(61, 17)]
	[InlineData(62, 18)]
	[InlineData(63, 19)]
	[InlineData(64, 20)]
	[InlineData(65, 21)]
	[InlineData(66, 22)]
	[InlineData(67, 23)]
	[InlineData(68, 24)]
	[InlineData(69, 25)]
	[InlineData(70, 26)]
	[InlineData(71, 27)]
	[InlineData(72, 28)]
	[InlineData(73, 29)]
	[InlineData(74, 30)]
	[InlineData(75, 31)]
	[InlineData(76, 32)]
	[InlineData(77, 33)]
	[InlineData(78, 34)]
	[InlineData(79, 35)]
	[InlineData(126, 36)]
	[InlineData(127, 37)]
	public void Construct_RejectedFoodWithGuardedUnusualStorageContextBuildsWarehouseAddAndZeroCubeUpdate(
		int storageId,
		int expectedCubeActionValue)
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.RejectedFood,
			Evaluation: new PetFeedEvaluationResult(null, IsLovedFood: false, Reward: null),
			Operations: [new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: 5001)],
			RemainingRequestedCount: 1,
			RefeedTimeMilliseconds: null);
		var item = new InventoryItem
		{
			ObjectId = 5001,
			ItemId = 188000001,
			Count = 2,
			OwnerId = 7001,
			Location = storageId,
			Slot = 4,
		};

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			plan,
			FeedProgressData: 0x123450,
			SupplementalContext: new PetFeedSupplementalPacketContext(
				UnlockPacketContext: new PetFeedUnlockPacketContext(
					PetFeedUnlockPacketStorageKind.UnusualWarehouse,
					item,
					CreateTemplate(item.ItemId, "Odd Snack")))));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, result.Status);
		var warehouseAdd = Assert.IsType<SmWarehouseAddItem>(result.Results[0].Packets[0]);
		var cubeUpdate = Assert.IsType<SmCubeUpdate>(result.Results[0].Packets[1]);

		using (var warehouseReader = new PacketBuffer(SerializeUnencryptedPayload(warehouseAdd)))
		{
			Assert.Equal(storageId, (int)warehouseReader.ReadC());
			Assert.Equal(SmWarehouseAddItem.AllSlot, warehouseReader.ReadH());
			Assert.Equal(1, warehouseReader.ReadH());
			Assert.Equal(5001, warehouseReader.ReadD());
			Assert.Equal(188000001, warehouseReader.ReadD());
			Assert.Equal(0, (int)warehouseReader.ReadC());
			Assert.Equal(string.Empty, warehouseReader.ReadS());
			var blobSize = warehouseReader.ReadH();
			Assert.True(blobSize > 0);
			warehouseReader.ReadB(blobSize);
			Assert.Equal(4, warehouseReader.ReadH());
			Assert.Equal(0, warehouseReader.Remaining);
		}

		using var cubeReader = new PacketBuffer(SerializeUnencryptedPayload(cubeUpdate));
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(expectedCubeActionValue, (int)cubeReader.ReadC());
		Assert.Equal(0, cubeReader.ReadD());
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(0, (int)cubeReader.ReadC());
		Assert.Equal(0, cubeReader.Remaining);
	}

	[Fact]
	public void Construct_RejectedFoodWithGuardedUnusualStorageContextRejectsUnknownStorageId()
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.RejectedFood,
			Evaluation: new PetFeedEvaluationResult(null, IsLovedFood: false, Reward: null),
			Operations: [new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: 5001)],
			RemainingRequestedCount: 1,
			RefeedTimeMilliseconds: null);

		var result = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			plan,
			FeedProgressData: 0x123450,
			SupplementalContext: new PetFeedSupplementalPacketContext(
				UnlockPacketContext: new PetFeedUnlockPacketContext(
					PetFeedUnlockPacketStorageKind.UnusualWarehouse,
					new InventoryItem { ObjectId = 5001, ItemId = 188000001, Location = 999, Slot = 4 },
					CreateTemplate(188000001, "Odd Snack")))));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Blocked, result.Status);
		Assert.Equal(PetFeedPacketMetadataResultStatus.BlockedItemUnlockPacket, result.Results[0].Status);
		Assert.Contains("not a modeled Java StorageType", result.Results[0].Notes);
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

	private static ItemTemplateSummary CreateTemplate(int itemId, string name)
	{
		return new ItemTemplateSummary(
			itemId,
			name,
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

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
