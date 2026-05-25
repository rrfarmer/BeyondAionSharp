using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationMutationSnapshotServiceTests
{
	[Fact]
	public void CreatePreview_ProducesPostMutationSnapshotsWithoutMutatingInventory()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);

		var preview = ItemPurificationMutationSnapshotService.CreatePreview(
			player.InventoryItems,
			application,
			npcExpands: 1,
			questExpands: 0,
			itemExpands: 1);

		Assert.True(preview.Succeeded);
		Assert.Equal(ItemPurificationMutationSnapshotStatus.Ready, preview.Status);
		Assert.Empty(preview.MissingObjectIds);
		Assert.Empty(preview.MismatchedObjectIds);
		Assert.Equal([5, 7], preview.CubeSnapshotsByPacketOperationIndex.Keys.Order().ToArray());
		Assert.Equal(1, preview.CubeSnapshotsByPacketOperationIndex[5].ItemsCount);
		Assert.Equal(2, preview.CubeSnapshotsByPacketOperationIndex[7].ItemsCount);
		Assert.All(preview.CubeSnapshotsByPacketOperationIndex.Values, snapshot =>
		{
			Assert.Equal(1, snapshot.NpcExpands);
			Assert.Equal(0, snapshot.QuestExpands);
			Assert.Equal(1, snapshot.ItemExpands);
		});
		Assert.Equal([20, 30, 9001], preview.PostMutationInventoryItems.Select(item => item.ObjectId).Order().ToArray());
		Assert.DoesNotContain(preview.PostMutationInventoryItems, item => item.ObjectId == baseItem.ObjectId);
		Assert.Equal(1, preview.PostMutationInventoryItems.Single(item => item.ObjectId == material.ObjectId).Count);
		var target = preview.PostMutationInventoryItems.Single(item => item.ObjectId == 9001);
		Assert.Equal(100000002, target.ItemId);
		Assert.Equal(20, target.Enchant);
		Assert.Equal(7, target.RandomBonus);

		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(3, material.Count);
		Assert.Equal(10_000, kinah.Count);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public void CreatePreview_FeedsHandlerPacketBridgeConcretePlan()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var itemTemplates = CreateItemTemplates();
		var workflow = ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			baseItem,
			CreatePurificationTable(),
			itemTemplates,
			resultItemId: 100000002,
			targetObjectId: 9001);
		var application = ItemPurificationApplicationPlanService.CreateApplicationPlan(workflow);
		var handlerPlan = new ItemPurificationHandlerPlan(
			workflow,
			application,
			ItemPurificationPacketPlanService.CreatePacketPlan(application, "item-100000001", "item-100000002"));
		var preview = ItemPurificationMutationSnapshotService.CreatePreview(
			player.InventoryItems,
			application,
			npcExpands: 1,
			questExpands: 0,
			itemExpands: 1);

		var bridge = ItemPurificationHandlerPacketBridgeService.CreateConcretePacketPlan(
			handlerPlan,
			preview.PostMutationInventoryItems,
			itemTemplates,
			preview.CubeSnapshotsByPacketOperationIndex);

		Assert.True(preview.Succeeded);
		Assert.True(bridge.Succeeded);
		Assert.Equal(ItemPurificationPacketInputSnapshotStatus.Ready, bridge.PacketInputs?.Status);
		Assert.NotNull(bridge.ConcretePacketPlan);
		Assert.True(bridge.ConcretePacketPlan.Succeeded);
		Assert.Equal(
			[
				typeof(SmSystemMessage),
				typeof(SmInventoryUpdateItem),
				typeof(SmDeleteItem),
				typeof(SmCubeUpdate),
				typeof(SmInventoryAddItem),
				typeof(SmCubeUpdate),
			],
			bridge.ConcretePacketPlan.Operations
				.Where(operation => operation.ConcretePacket != null)
				.Select(operation => operation.ConcretePacket!.GetType())
				.ToArray());
		Assert.Equal(
			[
				ItemPurificationPacketOperationType.AbyssPointsUpdate,
				ItemPurificationPacketOperationType.KinahNoPacket,
			],
			bridge.ConcretePacketPlan.Operations
				.Where(operation => operation.ConcretePacket == null)
				.Select(operation => operation.Type)
				.ToArray());
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(3, material.Count);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public void CreatePreview_ReportsMissingCurrentInventoryItem()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);

		var preview = ItemPurificationMutationSnapshotService.CreatePreview(
			currentInventoryItems: [baseItem, kinah],
			application,
			npcExpands: 0,
			questExpands: 0,
			itemExpands: 0);

		Assert.False(preview.Succeeded);
		Assert.Equal(ItemPurificationMutationSnapshotStatus.MissingCurrentInventoryItems, preview.Status);
		Assert.Equal([20], preview.MissingObjectIds);
		Assert.Empty(preview.MismatchedObjectIds);
		Assert.Equal([30, 9001], preview.PostMutationInventoryItems.Select(item => item.ObjectId).Order().ToArray());
		Assert.Equal(3, material.Count);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public void CreatePreview_RejectsApplicationPlanThatStillNeedsRuntimeInputs()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var player = CreatePlayer(
			abyssPoints: 5_000,
			baseItem,
			material,
			new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 });
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 0);

		var preview = ItemPurificationMutationSnapshotService.CreatePreview(
			player.InventoryItems,
			application,
			npcExpands: 0,
			questExpands: 0,
			itemExpands: 0);

		Assert.False(preview.Succeeded);
		Assert.Equal(ItemPurificationMutationSnapshotStatus.ApplicationPlanNotReady, preview.Status);
		Assert.Empty(preview.PostMutationInventoryItems);
		Assert.Empty(preview.CubeSnapshotsByPacketOperationIndex);
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

	private static Player CreatePlayer(int abyssPoints, params InventoryItem[] items)
	{
		return new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = abyssPoints },
			InventoryItems = items,
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
			RandomBonus = 7,
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
			CreateTemplate(100000001, statBonusSetId: 1, maxTuneCount: 5, maxEnchantLevel: 15),
			CreateTemplate(100000002, statBonusSetId: 1, maxTuneCount: 1, maxEnchantLevel: 20),
			CreateTemplate(186000001, statBonusSetId: 0, maxTuneCount: 0, maxEnchantLevel: 0),
		]);
	}

	private static ItemTemplateSummary CreateTemplate(
		int templateId,
		int statBonusSetId,
		int maxTuneCount,
		int maxEnchantLevel)
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
			StatBonusSetId: statBonusSetId,
			MaxTuneCount: maxTuneCount,
			MaxEnchantLevel: maxEnchantLevel);
	}
}
