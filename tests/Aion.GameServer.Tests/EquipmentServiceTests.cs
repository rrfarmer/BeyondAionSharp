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
		player.Skills =
		[
			new PlayerSkill { SkillId = 37, SkillLevel = 1 },
			new PlayerSkill { SkillId = 55, SkillLevel = 1 },
		];
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

	[Fact]
	public void ChangeEquipment_RejectsItemForInvalidClass()
	{
		var player = CreatePlayer(playerClass: "WARRIOR");
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = RestrictedSwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: 1,
			itemObjectId: 1001,
			CreateItemTemplates(new ItemTemplateSummary(
				RestrictedSwordId,
				"Mage Sword",
				0,
				1,
				1,
				"SWORD",
				"NORMAL",
				"COMMON",
				"PC_ALL",
				1,
				0,
				3,
				RequiredLevels: RequiredLevels(("MAGE", 1)))),
			skillTemplates: null);

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.InvalidClass, change.Failure);
	}

	[Fact]
	public void ChangeEquipment_RejectsItemWhenPlayerLevelIsTooLow()
	{
		var player = CreatePlayer(exp: 0);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = RestrictedSwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: 1,
			itemObjectId: 1001,
			CreateItemTemplates(new ItemTemplateSummary(
				RestrictedSwordId,
				"Level Ten Sword",
				0,
				1,
				10,
				"SWORD",
				"NORMAL",
				"COMMON",
				"PC_ALL",
				1,
				0,
				3,
				RequiredLevels: RequiredLevels(10))),
			skillTemplates: null,
			experienceTable: new PlayerExperienceTable([0, 1000]));

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.TooLowLevel, change.Failure);
		Assert.Equal(10, change.RequiredLevel);
		Assert.Equal("Level Ten Sword", change.ItemName);
	}

	[Fact]
	public void ChangeEquipment_RejectsItemWhenPlayerLevelIsTooHigh()
	{
		var player = CreatePlayer(exp: 1000);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = RestrictedSwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: 1,
			itemObjectId: 1001,
			CreateItemTemplates(new ItemTemplateSummary(
				RestrictedSwordId,
				"Lowbie Sword",
				0,
				1,
				1,
				"SWORD",
				"NORMAL",
				"COMMON",
				"PC_ALL",
				1,
				0,
				3,
				RequiredLevels: RequiredLevels(1),
				MaxLevelRestrictions: RequiredLevels(2))),
			skillTemplates: null,
			experienceTable: new PlayerExperienceTable([0, 500, 1000, 2000]));

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.TooHighLevel, change.Failure);
		Assert.Equal(2, change.MaxLevel);
		Assert.Equal("Lowbie Sword", change.ItemName);
	}

	[Fact]
	public void ChangeEquipment_RejectsItemForInvalidRace()
	{
		var player = CreatePlayer(race: "ASMODIANS");
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = RestrictedSwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: 1,
			itemObjectId: 1001,
			CreateItemTemplates(new ItemTemplateSummary(
				RestrictedSwordId,
				"Elyos Sword",
				0,
				1,
				1,
				"SWORD",
				"NORMAL",
				"COMMON",
				"ELYOS",
				1,
				0,
				3,
				RequiredLevels: RequiredLevels(1))),
			skillTemplates: null);

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.InvalidRace, change.Failure);
	}

	[Fact]
	public void ChangeEquipment_RejectsItemForInvalidGender()
	{
		var player = CreatePlayer(gender: "MALE");
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = RestrictedSwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: 1,
			itemObjectId: 1001,
			CreateItemTemplates(new ItemTemplateSummary(
				RestrictedSwordId,
				"Dress",
				0,
				1,
				1,
				"CL_TORSO",
				"NORMAL",
				"COMMON",
				"PC_ALL",
				1,
				0,
				8,
				RequiredLevels: RequiredLevels(1),
				GenderPermitted: "FEMALE")),
			skillTemplates: null);

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.InvalidGender, change.Failure);
	}

	[Fact]
	public void ChangeEquipment_RejectsItemForInvalidAbyssRank()
	{
		var player = CreatePlayer(rank: 1);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = RestrictedSwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: 1,
			itemObjectId: 1001,
			CreateItemTemplates(new ItemTemplateSummary(
				RestrictedSwordId,
				"Abyss Sword",
				0,
				1,
				1,
				"SWORD",
				"NORMAL",
				"COMMON",
				"PC_ALL",
				1,
				0,
				3,
				RequiredLevels: RequiredLevels(1),
				MinRank: 5,
				MaxRank: 9)),
			skillTemplates: null);

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.InvalidRank, change.Failure);
		Assert.Equal(PlayerAbyssRank.GetRankL10n("ELYOS", 5), change.RankName);
	}

	[Fact]
	public void ChangeEquipment_RejectsItemWhenRequiredEquipSkillIsMissing()
	{
		var player = CreatePlayer();
		player.Skills = [];
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = SwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(player, action: 0, slotRead: 1, itemObjectId: 1001, CreateItemTemplates(), skillTemplates: null);

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.MissingRequiredSkill, change.Failure);
		Assert.Empty(change.InventoryUpdateItems);
	}

	[Fact]
	public void ChangeEquipment_RejectsUnidentifiedItemAfterJavaEquipGuards()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = SwordId, Location = 0, Slot = 65535, TuneCount = -1 },
		];

		var change = EquipmentService.ChangeEquipment(player, action: 0, slotRead: 1, itemObjectId: 1001, CreateItemTemplates(), skillTemplates: null);

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.UnidentifiedItem, change.Failure);
		Assert.Empty(change.PersistedItems);
		Assert.Empty(change.InventoryUpdateItems);
	}

	[Fact]
	public void ChangeEquipment_RequestsSoulBindBeforeEquippingUnboundSoulBoundItem()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = SoulBoundSwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(player, action: 0, slotRead: 1, itemObjectId: 1001, CreateItemTemplates(), skillTemplates: null);

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.SoulBindRequired, change.Failure);
		Assert.Equal(1001, change.SoulBindItemObjectId);
		Assert.Equal(1, change.SoulBindSlot);
		Assert.Equal("Practice Soulbound Sword", change.ItemName);
	}

	[Fact]
	public void ChangeEquipment_SoulBindsAndEquipsWhenConfirmed()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = SoulBoundSwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: 1,
			itemObjectId: 1001,
			CreateItemTemplates(),
			skillTemplates: null,
			soulBindConfirmed: true);

		Assert.True(change.Changed);
		var sword = Assert.Single(change.InventoryItems);
		Assert.True(sword.IsSoulBound);
		Assert.True(sword.IsEquipped);
		Assert.Equal([(1001, true, true, 1L)], change.PersistedItems.Select(item => (item.ObjectId, item.IsSoulBound, item.IsEquipped, item.Slot)).ToArray());
	}

	private static Player CreatePlayer(
		string playerClass = "WARRIOR",
		string race = "ELYOS",
		string gender = "MALE",
		long exp = 0,
		int rank = 1)
	{
		return new Player
		{
			ObjectId = 7001,
			AccountId = 10,
			Name = "EquipTester",
			PlayerClass = playerClass,
			Race = race,
			Gender = gender,
			Exp = exp,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			AbyssRank = PlayerAbyssRank.Default() with { Rank = rank, MaxRank = rank },
			Skills =
			[
				new PlayerSkill { SkillId = 37, SkillLevel = 1 },
				new PlayerSkill { SkillId = 40, SkillLevel = 1 },
				new PlayerSkill { SkillId = 51, SkillLevel = 1 },
			],
		};
	}

	private static ItemTemplateTable CreateItemTemplates(params ItemTemplateSummary[] extraTemplates)
	{
		var templates = new List<ItemTemplateSummary>
		{
			new(SwordId, "Practice Sword", 0, 1, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3, RequiredLevels: RequiredLevels(1)),
			new(SoulBoundSwordId, "Practice Soulbound Sword", 0, 1 << 7, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3, RequiredLevels: RequiredLevels(1)),
			new(GreatswordId, "Practice Greatsword", 0, 1, 1, "GREATSWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3, RequiredLevels: RequiredLevels(1)),
			new(RobeId, "Practice Robe", 0, 1, 1, "CL_TORSO", "NORMAL", "COMMON", "PC_ALL", 1, 0, 8, RequiredLevels: RequiredLevels(1)),
		};
		templates.AddRange(extraTemplates);
		return new ItemTemplateTable(templates);
	}

	private static IReadOnlyDictionary<string, int> RequiredLevels(int level)
	{
		return PlayerClasses.ToDictionary(playerClass => playerClass, _ => level, StringComparer.Ordinal);
	}

	private static IReadOnlyDictionary<string, int> RequiredLevels(params (string PlayerClass, int Level)[] entries)
	{
		return entries.ToDictionary(entry => entry.PlayerClass, entry => entry.Level, StringComparer.Ordinal);
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
	private const int SoulBoundSwordId = 100000003;
	private const int GreatswordId = 100100001;
	private const int RobeId = 110100001;
	private const int RestrictedSwordId = 100000002;
	private static readonly string[] PlayerClasses =
	[
		"WARRIOR",
		"GLADIATOR",
		"TEMPLAR",
		"SCOUT",
		"ASSASSIN",
		"RANGER",
		"MAGE",
		"SORCERER",
		"SPIRIT_MASTER",
		"PRIEST",
		"CLERIC",
		"CHANTER",
		"ENGINEER",
		"RIDER",
		"GUNNER",
		"ARTIST",
		"BARD",
	];
}
