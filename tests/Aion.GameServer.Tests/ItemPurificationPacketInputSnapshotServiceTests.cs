using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationPacketInputSnapshotServiceTests
{
	[Fact]
	public void CreateInputs_ProjectsPostMutationSnapshotsIntoPacketPlanInputs()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		var postMutationItems = new[]
		{
			new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 },
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
			new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0, Slot = -1 },
		};
		var cubeSnapshots = new Dictionary<int, ItemPurificationCubeSnapshot>
		{
			[5] = new(ItemsCount: 2, NpcExpands: 1, QuestExpands: 0, ItemExpands: 0),
			[7] = new(ItemsCount: 3, NpcExpands: 1, QuestExpands: 0, ItemExpands: 1),
		};

		var result = ItemPurificationPacketInputSnapshotService.CreateInputs(
			application,
			postMutationItems,
			CreateItemTemplates(),
			cubeSnapshots);
		var packetPlan = ItemPurificationPacketPlanService.CreatePacketPlan(
			application,
			"base-name",
			"target-name",
			result.InventoryPacketInputs,
			result.CubePacketInputsByPacketOperationIndex);

		Assert.True(result.Succeeded);
		Assert.Equal(ItemPurificationPacketInputSnapshotStatus.Ready, result.Status);
		Assert.Equal([20, 9001], result.InventoryPacketInputs.Keys.Order().ToArray());
		Assert.Equal([5, 7], result.CubePacketInputsByPacketOperationIndex.Keys.Order().ToArray());
		Assert.Empty(result.MissingTemplateIds);
		Assert.Empty(result.MissingInventoryObjectIds);
		Assert.Empty(result.MismatchedInventoryObjectIds);
		Assert.Empty(result.MissingCubePacketOperationIndexes);
		Assert.Empty(result.InvalidCubePacketOperationIndexes);
		Assert.IsType<SmInventoryUpdateItem>(packetPlan.Operations[1].ConcretePacket);
		Assert.IsType<SmDeleteItem>(packetPlan.Operations[4].ConcretePacket);
		Assert.IsType<SmCubeUpdate>(packetPlan.Operations[5].ConcretePacket);
		Assert.IsType<SmInventoryAddItem>(packetPlan.Operations[6].ConcretePacket);
		Assert.IsType<SmCubeUpdate>(packetPlan.Operations[7].ConcretePacket);
	}

	[Fact]
	public void CreateInputs_ReportsMissingSnapshotsWithoutSynthesizingPackets()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);

		var result = ItemPurificationPacketInputSnapshotService.CreateInputs(
			application,
			postMutationInventoryItems:
			[
				new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 },
				new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
			],
			CreateItemTemplates(),
			new Dictionary<int, ItemPurificationCubeSnapshot>());

		Assert.False(result.Succeeded);
		Assert.Equal(ItemPurificationPacketInputSnapshotStatus.MissingInventorySnapshots, result.Status);
		Assert.Equal([9001], result.MissingInventoryObjectIds);
		Assert.Empty(result.MismatchedInventoryObjectIds);
		Assert.Equal([5, 7], result.MissingCubePacketOperationIndexes);
		Assert.Empty(result.InvalidCubePacketOperationIndexes);
		Assert.Equal([20], result.InventoryPacketInputs.Keys.ToArray());
		Assert.Empty(result.CubePacketInputsByPacketOperationIndex);
	}

	[Fact]
	public void CreateInputs_ReportsMismatchedInventorySnapshotCounts()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);

		var result = ItemPurificationPacketInputSnapshotService.CreateInputs(
			application,
			postMutationInventoryItems:
			[
				new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 },
				new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0, Slot = -1 },
			],
			CreateItemTemplates(),
			new Dictionary<int, ItemPurificationCubeSnapshot>
			{
				[5] = new(ItemsCount: 2, NpcExpands: 0, QuestExpands: 0, ItemExpands: 0),
				[7] = new(ItemsCount: 3, NpcExpands: 0, QuestExpands: 0, ItemExpands: 0),
			});

		Assert.False(result.Succeeded);
		Assert.Equal(ItemPurificationPacketInputSnapshotStatus.MismatchedInventorySnapshots, result.Status);
		Assert.Equal([20], result.MismatchedInventoryObjectIds);
		Assert.DoesNotContain(20, result.InventoryPacketInputs.Keys);
	}

	[Fact]
	public void CreateInputs_ReportsInvalidCubeSnapshots()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);

		var result = ItemPurificationPacketInputSnapshotService.CreateInputs(
			application,
			postMutationInventoryItems:
			[
				new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
				new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0, Slot = -1 },
			],
			CreateItemTemplates(),
			new Dictionary<int, ItemPurificationCubeSnapshot>
			{
				[5] = new(ItemsCount: 2, NpcExpands: 256, QuestExpands: 0, ItemExpands: 0),
				[7] = new(ItemsCount: 3, NpcExpands: 0, QuestExpands: 0, ItemExpands: 0),
			});

		Assert.False(result.Succeeded);
		Assert.Equal(ItemPurificationPacketInputSnapshotStatus.InvalidCubeSnapshots, result.Status);
		Assert.Equal([5], result.InvalidCubePacketOperationIndexes);
		Assert.DoesNotContain(5, result.CubePacketInputsByPacketOperationIndex.Keys);
		Assert.Contains(7, result.CubePacketInputsByPacketOperationIndex.Keys);
	}

	[Fact]
	public void CreateCubeSnapshot_UsesJavaStorageSizeSemanticsForKinah()
	{
		var snapshot = ItemPurificationPacketInputSnapshotService.CreateCubeSnapshot(
			[
				new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 },
				new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
				new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0 },
				new InventoryItem { ObjectId = 40, ItemId = 166000001, Count = 1, Location = 1 },
			],
			npcExpands: 2,
			questExpands: 1,
			itemExpands: 3);

		Assert.Equal(2, snapshot.ItemsCount);
		Assert.Equal(2, snapshot.NpcExpands);
		Assert.Equal(1, snapshot.QuestExpands);
		Assert.Equal(3, snapshot.ItemExpands);
	}

	[Fact]
	public void CreateInputs_ReportsMissingTemplatesBeforeMissingInventorySnapshots()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		var templatesWithoutMaterial = new ItemTemplateTable([CreateTemplate(100000001), CreateTemplate(100000002)]);

		var result = ItemPurificationPacketInputSnapshotService.CreateInputs(
			application,
			postMutationInventoryItems:
			[
				new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
				new InventoryItem { ObjectId = 9001, ItemId = 100000002, Count = 1, Location = 0, Slot = -1 },
			],
			templatesWithoutMaterial,
			new Dictionary<int, ItemPurificationCubeSnapshot>
			{
				[5] = new(ItemsCount: 2, NpcExpands: 0, QuestExpands: 0, ItemExpands: 0),
				[7] = new(ItemsCount: 3, NpcExpands: 0, QuestExpands: 0, ItemExpands: 0),
			});

		Assert.False(result.Succeeded);
		Assert.Equal(ItemPurificationPacketInputSnapshotStatus.MissingTemplates, result.Status);
		Assert.Equal([186000001], result.MissingTemplateIds);
	}

	[Fact]
	public void CreateInputs_RejectsApplicationPlanThatStillNeedsRuntimeInputs()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 0);

		var result = ItemPurificationPacketInputSnapshotService.CreateInputs(
			application,
			postMutationInventoryItems: [],
			CreateItemTemplates(),
			new Dictionary<int, ItemPurificationCubeSnapshot>());

		Assert.False(result.Succeeded);
		Assert.Equal(ItemPurificationPacketInputSnapshotStatus.ApplicationPlanNotReady, result.Status);
		Assert.Empty(result.InventoryPacketInputs);
		Assert.Empty(result.CubePacketInputsByPacketOperationIndex);
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
			CreateTemplate(100000001),
			CreateTemplate(100000002),
			CreateTemplate(186000001),
		]);
	}

	private static ItemTemplateSummary CreateTemplate(int templateId)
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
			StatBonusSetId: 1,
			MaxTuneCount: 5,
			MaxEnchantLevel: 20);
	}
}
