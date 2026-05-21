using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class EquipmentServiceTests
{
	[Fact]
	public void ChangeEquipment_EquipsOneHandWeaponInMainHandWithoutDualWieldSkill()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = SwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(player, action: 0, slotRead: 2, itemObjectId: 1001, CreateItemTemplates(), skillTemplates: null);

		Assert.True(change.Changed);
		var sword = Assert.Single(change.InventoryItems);
		Assert.True(sword.IsEquipped);
		Assert.Equal(1, sword.Slot);
		Assert.Equal([1001], change.PersistedItems.Select(item => item.ObjectId));
		Assert.Equal([1], change.InventoryUpdateItems.Select(item => item.Slot));
	}

	[Fact]
	public void ChangeEquipment_AllowsOffHandOneHandWeaponWithWeaponDualSkill()
	{
		var player = CreatePlayer();
		player.Skills = [new PlayerSkill { SkillId = 55, SkillLevel = 1 }];
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = SwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(player, action: 0, slotRead: 2, itemObjectId: 1001, CreateItemTemplates(), CreateSkillTemplates());

		Assert.True(change.Changed);
		var sword = Assert.Single(change.InventoryItems);
		Assert.True(sword.IsEquipped);
		Assert.Equal(2, sword.Slot);
	}

	[Fact]
	public void ChangeEquipment_UnequipsCollidingSlotBeforeEquippingNewItem()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = RobeId, Location = 0, IsEquipped = true, Slot = 8 },
			new InventoryItem { ObjectId = 1002, ItemId = RobeId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(player, action: 0, slotRead: 8, itemObjectId: 1002, CreateItemTemplates(), skillTemplates: null);

		Assert.True(change.Changed);
		Assert.Equal([(1001, false, 0L), (1002, true, 8L)], change.PersistedItems.Select(item => (item.ObjectId, item.IsEquipped, item.Slot)).ToArray());
		Assert.Equal([(1001, false, 0L), (1002, true, 8L)], change.InventoryUpdateItems.Select(item => (item.ObjectId, item.IsEquipped, item.Slot)).ToArray());
	}

	[Fact]
	public void ChangeEquipment_RejectsTwoHandWeaponWhenFullInventoryWouldNeedTwoSlots()
	{
		var player = CreatePlayer();
		var items = new List<InventoryItem>
		{
			new() { ObjectId = 1001, ItemId = SwordId, Location = 0, IsEquipped = true, Slot = 1 },
			new() { ObjectId = 1002, ItemId = SwordId, Location = 0, IsEquipped = true, Slot = 2 },
			new() { ObjectId = 1003, ItemId = GreatswordId, Location = 0, Slot = 65535 },
		};
		for (var i = 0; i < 26; i++)
			items.Add(new InventoryItem { ObjectId = 2000 + i, ItemId = RobeId, Location = 0, Slot = 65535 });
		player.InventoryItems = items;

		var change = EquipmentService.ChangeEquipment(player, action: 0, slotRead: 1, itemObjectId: 1003, CreateItemTemplates(), skillTemplates: null);

		Assert.False(change.Changed);
		Assert.True(change.InventoryFull);
		Assert.Empty(change.PersistedItems);
	}

	[Fact]
	public void ChangeEquipment_UnequipsItemAndReportsInventoryFullWhenCubeIsFull()
	{
		var player = CreatePlayer();
		var items = new List<InventoryItem>
		{
			new() { ObjectId = 1001, ItemId = RobeId, Location = 0, IsEquipped = true, Slot = 8 },
		};
		for (var i = 0; i < 27; i++)
			items.Add(new InventoryItem { ObjectId = 2000 + i, ItemId = RobeId, Location = 0, Slot = 65535 });
		player.InventoryItems = items;

		var change = EquipmentService.ChangeEquipment(player, action: 1, slotRead: 0, itemObjectId: 1001, CreateItemTemplates(), skillTemplates: null);

		Assert.False(change.Changed);
		Assert.True(change.InventoryFull);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 7001,
			AccountId = 10,
			Name = "EquipTester",
			PlayerClass = "WARRIOR",
			Race = "ELYOS",
			Gender = "MALE",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(SwordId, "Practice Sword", 0, 1, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3),
			new ItemTemplateSummary(GreatswordId, "Practice Greatsword", 0, 1, 1, "GREATSWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3),
			new ItemTemplateSummary(RobeId, "Practice Robe", 0, 1, 1, "CL_TORSO", "NORMAL", "COMMON", "PC_ALL", 1, 0, 8),
		]);
	}

	private static SkillTemplateTable CreateSkillTemplates()
	{
		return new SkillTemplateTable(
		[
			new SkillTemplateSummary(
				55,
				"Advanced Dual-Wielding I",
				0,
				1,
				string.Empty,
				string.Empty,
				string.Empty,
				string.Empty,
				0,
				0,
				WeaponDualEffects: [new SkillWeaponDualEffectSummary(50, 0, 100, 0, 0)]),
		]);
	}

	private const int SwordId = 100000001;
	private const int GreatswordId = 100100001;
	private const int RobeId = 110100001;
}
