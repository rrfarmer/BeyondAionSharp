using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationPacketPlanServiceTests
{
	[Fact]
	public void CreatePacketPlan_PutsUpgradeSuccessMessageBeforeInventoryFanout()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);

		var plan = ItemPurificationPacketPlanService.CreatePacketPlan(application, "base-name", "target-name");

		Assert.True(plan.Succeeded);
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.UpgradeSuccessSystemMessage,
				ItemPurificationPacketOperationType.DeleteItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
				ItemPurificationPacketOperationType.DeleteItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
				ItemPurificationPacketOperationType.InventoryAddItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
			],
			plan.Operations.Select(operation => operation.Type).ToArray());
		Assert.Equal(ItemPurificationPacketPlanService.UpgradeSuccessMessageId, plan.Operations[0].Mask);
		Assert.Equal(["base-name", "target-name"], plan.Operations[0].Parameters);
		Assert.IsType<SmSystemMessage>(plan.Operations[0].ConcretePacket);
		Assert.All(
			plan.Operations.Skip(1).Where(operation => operation.Type != ItemPurificationPacketOperationType.DeleteItem),
			operation => Assert.Null(operation.ConcretePacket));
	}

	[Fact]
	public void CreatePacketPlan_UsesJavaInventoryMasksForUpdateDeleteAndAdd()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);

		var plan = ItemPurificationPacketPlanService.CreatePacketPlan(application, "base-name", "target-name");

		Assert.True(plan.Succeeded);
		var materialUpdate = plan.Operations[1];
		Assert.Equal(ItemPurificationPacketOperationType.InventoryUpdateItem, materialUpdate.Type);
		Assert.Equal(ItemPurificationPacketPlanService.InventoryUpdatePacketOpcode, materialUpdate.PacketOpcode);
		Assert.Equal(ItemPurificationPacketPlanService.DecreaseItemUseUpdateType, materialUpdate.Mask);
		Assert.Equal(20, materialUpdate.ObjectId);
		Assert.Equal(ItemPurificationApplicationOperationType.UpdateMaterialItemCount, materialUpdate.SourceOperationType);

		var baseDelete = plan.Operations.Single(operation =>
			operation.Type == ItemPurificationPacketOperationType.DeleteItem
			&& operation.ObjectId == 10);
		Assert.Equal(ItemPurificationPacketPlanService.DeleteItemPacketOpcode, baseDelete.PacketOpcode);
		Assert.Equal(ItemPurificationPacketPlanService.UseDeleteType, baseDelete.Mask);

		var targetAdd = plan.Operations.Single(operation => operation.Type == ItemPurificationPacketOperationType.InventoryAddItem);
		Assert.Equal(ItemPurificationPacketPlanService.InventoryAddPacketOpcode, targetAdd.PacketOpcode);
		Assert.Equal(ItemPurificationPacketPlanService.ItemCollectAddType, targetAdd.Mask);
		Assert.Equal(9001, targetAdd.ObjectId);
		Assert.Equal(100000002, targetAdd.ItemId);
	}

	[Fact]
	public void CreatePacketPlan_AttachesConcreteInventoryUpdatePacketWhenRuntimeItemInputProvided()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		var materialTemplate = CreateTemplate(186000001, maxTuneCount: 0, maxEnchantLevel: 0);
		var inventoryInputs = new Dictionary<int, ItemPurificationInventoryPacketInput>
		{
			[20] = new(
				new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
				materialTemplate),
		};

		var plan = ItemPurificationPacketPlanService.CreatePacketPlan(
			application,
			"base-name",
			"target-name",
			inventoryInputs);

		Assert.True(plan.Succeeded);
		var materialUpdate = plan.Operations.Single(operation =>
			operation.Type == ItemPurificationPacketOperationType.InventoryUpdateItem);
		var concretePacket = Assert.IsType<SmInventoryUpdateItem>(materialUpdate.ConcretePacket);
		Assert.Equal(SmInventoryUpdateItem.PacketOpCode, concretePacket.OpCode);
		Assert.Equal(ItemPurificationPacketPlanService.DecreaseItemUseUpdateType, materialUpdate.Mask);
		Assert.All(
			plan.Operations.Where(operation =>
				operation.Type is not ItemPurificationPacketOperationType.UpgradeSuccessSystemMessage
					and not ItemPurificationPacketOperationType.InventoryUpdateItem
					and not ItemPurificationPacketOperationType.DeleteItem),
			operation => Assert.Null(operation.ConcretePacket));
	}

	[Fact]
	public void CreatePacketPlan_AttachesConcreteDeletePacketsBeforeCubeSizeMetadata()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);

		var plan = ItemPurificationPacketPlanService.CreatePacketPlan(application, "base-name", "target-name");

		var deleteOperations = plan.Operations
			.Where(operation => operation.Type == ItemPurificationPacketOperationType.DeleteItem)
			.ToArray();
		Assert.Equal([20, 10], deleteOperations.Select(operation => operation.ObjectId).ToArray());
		Assert.All(deleteOperations, operation =>
		{
			var concretePacket = Assert.IsType<SmDeleteItem>(operation.ConcretePacket);
			Assert.Equal(SmDeleteItem.PacketOpCode, concretePacket.OpCode);
			Assert.Equal(ItemPurificationPacketPlanService.UseDeleteType, operation.Mask);
			using var reader = new PacketBuffer(SerializeUnencryptedPayload(concretePacket));
			Assert.Equal(operation.ObjectId, reader.ReadD());
			Assert.Equal(ItemPurificationPacketPlanService.UseDeleteType, (int)reader.ReadC());
			Assert.Equal(0, reader.Remaining);
		});
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.DeleteItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
			],
			plan.Operations.Skip(1).Take(2).Select(operation => operation.Type).ToArray());
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.DeleteItem,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
			],
			plan.Operations.Skip(5).Take(2).Select(operation => operation.Type).ToArray());
		Assert.All(
			plan.Operations.Where(operation => operation.Type == ItemPurificationPacketOperationType.CubeSizeUpdate),
			operation => Assert.Null(operation.ConcretePacket));
	}

	[Fact]
	public void CreatePacketPlan_AttachesConcreteTargetAddPacketWhenRuntimeItemInputProvided()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		var targetTemplate = CreateTemplate(100000002, maxTuneCount: 1, maxEnchantLevel: 20);
		var inventoryInputs = new Dictionary<int, ItemPurificationInventoryPacketInput>
		{
			[9001] = new(
				new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0, Slot = -1 },
				targetTemplate,
				GeneralInfoWarehouseRestrictionFlag: 3),
		};

		var plan = ItemPurificationPacketPlanService.CreatePacketPlan(
			application,
			"base-name",
			"target-name",
			inventoryInputs);

		var addOperation = plan.Operations.Single(operation =>
			operation.Type == ItemPurificationPacketOperationType.InventoryAddItem);
		var concretePacket = Assert.IsType<SmInventoryAddItem>(addOperation.ConcretePacket);
		Assert.Equal(SmInventoryAddItem.PacketOpCode, concretePacket.OpCode);
		Assert.Equal(ItemPurificationPacketPlanService.ItemCollectAddType, addOperation.Mask);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(concretePacket));
		Assert.Equal(ItemPurificationPacketPlanService.ItemCollectAddType, reader.ReadH());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(9001, reader.ReadD());
		Assert.Equal(100000002, reader.ReadD());
		reader.ReadS();
		var blobSize = reader.ReadH();
		Assert.True(blobSize > 0);
		var blob = reader.ReadB(blobSize);
		Assert.Equal(0xffff, reader.ReadH());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
		AssertGeneralInfoCleanupSealFlag(blob, expectedFlag: 3);
		Assert.Null(plan.Operations[8].ConcretePacket);
	}

	[Fact]
	public void CreatePacketPlan_AttachesConcreteCubePacketsWhenRuntimeSnapshotsProvided()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		var cubeInputs = new Dictionary<int, ItemPurificationCubePacketInput>
		{
			[2] = CreateCubeInput(ItemPurificationApplicationOperationType.DeleteMaterialItem, 20, 186000001, 2, 1, 0, 0),
			[6] = CreateCubeInput(ItemPurificationApplicationOperationType.DeleteBaseItem, 10, 100000001, 1, 1, 0, 0),
			[8] = CreateCubeInput(ItemPurificationApplicationOperationType.AddTargetItem, 9001, 100000002, 2, 1, 0, 1),
		};

		var plan = ItemPurificationPacketPlanService.CreatePacketPlan(
			application,
			"base-name",
			"target-name",
			cubePacketInputsByPacketOperationIndex: cubeInputs);

		var cubeOperations = plan.Operations
			.Where(operation => operation.Type == ItemPurificationPacketOperationType.CubeSizeUpdate)
			.ToArray();
		Assert.Equal([20, 10, 9001], cubeOperations.Select(operation => operation.ObjectId).ToArray());
		Assert.Equal(
			[
				(ItemPurificationPacketOperationType.DeleteItem, 20),
				(ItemPurificationPacketOperationType.CubeSizeUpdate, 20),
			],
			plan.Operations.Skip(1).Take(2).Select(operation => (operation.Type, operation.ObjectId)).ToArray());
		Assert.Equal(
			[
				(ItemPurificationPacketOperationType.DeleteItem, 10),
				(ItemPurificationPacketOperationType.CubeSizeUpdate, 10),
			],
			plan.Operations.Skip(5).Take(2).Select(operation => (operation.Type, operation.ObjectId)).ToArray());
		Assert.Equal(
			[
				(ItemPurificationPacketOperationType.InventoryAddItem, 9001),
				(ItemPurificationPacketOperationType.CubeSizeUpdate, 9001),
			],
			plan.Operations.Skip(7).Take(2).Select(operation => (operation.Type, operation.ObjectId)).ToArray());
		AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(cubeOperations[0].ConcretePacket), 2, 1, 0, 0);
		AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(cubeOperations[1].ConcretePacket), 1, 1, 0, 0);
		AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(cubeOperations[2].ConcretePacket), 2, 1, 0, 1);
	}

	[Fact]
	public void CreatePacketPlan_LeavesCubeMetadataWhenRuntimeSnapshotDoesNotMatchOperation()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		var cubeInputs = new Dictionary<int, ItemPurificationCubePacketInput>
		{
			[2] = CreateCubeInput(ItemPurificationApplicationOperationType.DeleteBaseItem, 20, 186000001, 2, 1, 0, 0),
			[6] = CreateCubeInput(ItemPurificationApplicationOperationType.DeleteBaseItem, 10, 100000001, 1, 1, 0, 0) with { NpcExpands = 256 },
		};

		var plan = ItemPurificationPacketPlanService.CreatePacketPlan(
			application,
			"base-name",
			"target-name",
			cubePacketInputsByPacketOperationIndex: cubeInputs);

		var cubeOperations = plan.Operations
			.Where(operation => operation.Type == ItemPurificationPacketOperationType.CubeSizeUpdate)
			.ToArray();
		Assert.All(cubeOperations, operation => Assert.Null(operation.ConcretePacket));
	}

	[Fact]
	public void CreatePacketPlan_FlagsRuntimeInputBlockersButStillListsDryRunPackets()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 0);

		var plan = ItemPurificationPacketPlanService.CreatePacketPlan(application, "base-name", "target-name");

		Assert.Equal(ItemPurificationPacketPlanStatus.NeedsRuntimeInputs, plan.Status);
		Assert.False(plan.Succeeded);
		Assert.Contains(plan.Operations, operation => operation.Type == ItemPurificationPacketOperationType.InventoryAddItem);
		Assert.Contains(plan.Operations, operation => operation.Type == ItemPurificationPacketOperationType.CubeSizeUpdate);
	}

	[Fact]
	public async Task SendConcretePacketsAsync_SendsConcretePacketsInPlanOrderAndSkipsMetadata()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		var inventoryInputs = new Dictionary<int, ItemPurificationInventoryPacketInput>
		{
			[20] = new(
				new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
				CreateTemplate(186000001, maxTuneCount: 0, maxEnchantLevel: 0)),
			[9001] = new(
				new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0, Slot = -1 },
				CreateTemplate(100000002, maxTuneCount: 1, maxEnchantLevel: 20)),
		};
		var plan = ItemPurificationPacketPlanService.CreatePacketPlan(
			application,
			"base-name",
			"target-name",
			inventoryInputs);
		var registry = new RecordingConnectionRegistry();
		var adapter = new ItemPurificationPacketSendAdapter(registry);

		var result = await adapter.SendConcretePacketsAsync(player.ObjectId, plan);

		Assert.True(result.Succeeded);
		Assert.Equal(ItemPurificationPacketPlanStatus.Ready, result.PacketPlanStatus);
		Assert.Equal(4, result.SentCount);
		Assert.Equal([player.ObjectId, player.ObjectId, player.ObjectId, player.ObjectId], registry.SentPackets.Select(send => send.PlayerObjectId).ToArray());
		Assert.Equal(
			[
				typeof(SmSystemMessage),
				typeof(SmInventoryUpdateItem),
				typeof(SmDeleteItem),
				typeof(SmInventoryAddItem),
			],
			result.Packets.Select(packet => packet.GetType()).ToArray());
		Assert.Equal(result.Packets, registry.SentPackets.Select(send => send.Packet).ToArray());
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
				ItemPurificationPacketOperationType.CubeSizeUpdate,
			],
			result.SkippedMetadataOperations.Select(operation => operation.Type).ToArray());
	}

	[Fact]
	public async Task SendConcretePacketsAsync_IncludesConcreteCubePacketsInPlanOrderWhenSnapshotsProvided()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		var inventoryInputs = new Dictionary<int, ItemPurificationInventoryPacketInput>
		{
			[20] = new(
				new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
				CreateTemplate(186000001, maxTuneCount: 0, maxEnchantLevel: 0)),
			[9001] = new(
				new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0, Slot = -1 },
				CreateTemplate(100000002, maxTuneCount: 1, maxEnchantLevel: 20)),
		};
		var cubeInputs = new Dictionary<int, ItemPurificationCubePacketInput>
		{
			[5] = CreateCubeInput(ItemPurificationApplicationOperationType.DeleteBaseItem, 10, 100000001, 2, 1, 0, 0),
			[7] = CreateCubeInput(ItemPurificationApplicationOperationType.AddTargetItem, 9001, 100000002, 3, 1, 0, 1),
		};
		var plan = ItemPurificationPacketPlanService.CreatePacketPlan(
			application,
			"base-name",
			"target-name",
			inventoryInputs,
			cubeInputs);
		var registry = new RecordingConnectionRegistry();
		var adapter = new ItemPurificationPacketSendAdapter(registry);

		var result = await adapter.SendConcretePacketsAsync(player.ObjectId, plan);

		Assert.True(result.Succeeded);
		Assert.Equal(6, result.SentCount);
		Assert.Equal(
			[
				typeof(SmSystemMessage),
				typeof(SmInventoryUpdateItem),
				typeof(SmDeleteItem),
				typeof(SmCubeUpdate),
				typeof(SmInventoryAddItem),
				typeof(SmCubeUpdate),
			],
			result.Packets.Select(packet => packet.GetType()).ToArray());
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
			],
			result.SkippedMetadataOperations.Select(operation => operation.Type).ToArray());
		AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(result.Packets[3]), 2, 1, 0, 0);
		AssertCubeUpdatePayload(Assert.IsType<SmCubeUpdate>(result.Packets[5]), 3, 1, 0, 1);
	}

	[Fact]
	public async Task SendConcretePacketsAsync_RejectsPacketPlanThatStillNeedsRuntimeInputs()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 0);
		var packetPlan = ItemPurificationPacketPlanService.CreatePacketPlan(application, "base-name", "target-name");
		var registry = new RecordingConnectionRegistry();
		var adapter = new ItemPurificationPacketSendAdapter(registry);

		var result = await adapter.SendConcretePacketsAsync(player.ObjectId, packetPlan);

		Assert.Equal(ItemPurificationPacketSendStatus.PacketPlanNotReady, result.Status);
		Assert.False(result.Succeeded);
		Assert.Empty(result.Packets);
		Assert.Empty(result.ConcreteOperations);
		Assert.Empty(result.SkippedMetadataOperations);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public void CreatePacketPlan_RejectsMissingOrEmptyApplicationPlan()
	{
		var missing = ItemPurificationPacketPlanService.CreatePacketPlan(null, "base-name", "target-name");
		var empty = ItemPurificationPacketPlanService.CreatePacketPlan(
			ItemPurificationApplicationPlan.Failed(ItemPurificationApplicationPlanStatus.MissingWorkflow),
			"base-name",
			"target-name");

		Assert.Equal(ItemPurificationPacketPlanStatus.MissingApplicationPlan, missing.Status);
		Assert.Empty(missing.Operations);
		Assert.Equal(ItemPurificationPacketPlanStatus.ApplicationPlanUnavailable, empty.Status);
		Assert.Empty(empty.Operations);
	}

	private static ItemPurificationApplicationPlan CreateApplicationPlan(
		Player player,
		InventoryItem baseItem,
		int targetObjectId)
	{
		var workflow = ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			baseItem,
			CreatePurificationTable(),
			CreateItemTemplates(),
			resultItemId: 100000002,
			targetObjectId);
		return ItemPurificationApplicationPlanService.CreateApplicationPlan(workflow);
	}

	private static Player CreatePlayer(int abyssPoints, long kinah, params InventoryItem[] items)
	{
		return new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = abyssPoints },
			InventoryItems =
			[
				new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = kinah, Location = 0 },
				.. items,
			],
		};
	}

	private static InventoryItem CreateBaseItem(int enchant)
	{
		return new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
			Count = 1,
			Location = 0,
			Enchant = enchant,
			TuneCount = 2,
		};
	}

	private static ItemPurificationTable CreatePurificationTable()
	{
		return new ItemPurificationTable(
		[
			new ItemPurificationSummary(
				100000001,
				[
					new ItemPurificationResultSummary(
						ResultItemId: 100000002,
						MinEnchantCount: 10,
						NecessaryAbyssPoints: 1_200,
						NecessaryKinah: 1_000,
						RequiredMaterials: [new ItemPurificationMaterialSummary(186000001, 2)]),
				]),
		]);
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			CreateTemplate(100000001, maxTuneCount: 5, maxEnchantLevel: 15),
			CreateTemplate(100000002, maxTuneCount: 1, maxEnchantLevel: 20),
		]);
	}

	private static ItemTemplateSummary CreateTemplate(int templateId, int maxTuneCount, int maxEnchantLevel)
	{
		return new ItemTemplateSummary(
			TemplateId: templateId,
			Name: $"item-{templateId}",
			DescriptionId: 0,
			Mask: 0,
			Level: 65,
			ItemGroup: "SWORD",
			ItemType: "normal",
			Quality: "MYTHIC",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 0,
			StatBonusSetId: 0,
			MaxTuneCount: maxTuneCount,
			MaxEnchantLevel: maxEnchantLevel);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static ItemPurificationCubePacketInput CreateCubeInput(
		ItemPurificationApplicationOperationType sourceOperationType,
		int expectedObjectId,
		int expectedItemId,
		int itemsCount,
		int npcExpands,
		int questExpands,
		int itemExpands)
	{
		return new ItemPurificationCubePacketInput(
			ItemPurificationPacketPlanService.CubeStorageTypeId,
			ItemPurificationPacketPlanService.CubeStorageTypeOrdinal,
			sourceOperationType,
			expectedObjectId,
			expectedItemId,
			itemsCount,
			npcExpands,
			questExpands,
			itemExpands);
	}

	private static void AssertCubeUpdatePayload(
		SmCubeUpdate packet,
		int expectedItemsCount,
		int expectedNpcExpands,
		int expectedQuestExpands,
		int expectedItemExpands)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(expectedItemsCount, reader.ReadD());
		Assert.Equal(expectedNpcExpands, (int)reader.ReadC());
		Assert.Equal(expectedQuestExpands, (int)reader.ReadC());
		Assert.Equal(expectedItemExpands, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertGeneralInfoCleanupSealFlag(byte[] blob, int expectedFlag)
	{
		using var blobReader = new PacketBuffer(blob);
		Assert.Equal(0x00, (int)blobReader.ReadC());
		Assert.Equal(0, blobReader.ReadH());
		Assert.Equal(1, blobReader.ReadQ());
		Assert.Equal(string.Empty, blobReader.ReadS());
		Assert.Equal(0, (int)blobReader.ReadC());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(expectedFlag, blobReader.ReadH());
	}

	private sealed class RecordingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<SentPacket> SentPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			SentPackets.Add(new SentPacket(playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}

	private sealed record SentPacket(int PlayerObjectId, GameServerPacket Packet);
}
