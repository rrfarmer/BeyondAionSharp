using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

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
		Assert.All(plan.Operations.Skip(1), operation => Assert.Null(operation.ConcretePacket));
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
					and not ItemPurificationPacketOperationType.InventoryUpdateItem),
			operation => Assert.Null(operation.ConcretePacket));
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
}
