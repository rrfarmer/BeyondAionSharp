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
			new InventoryItem { ObjectId = 1001, ItemId = SwordId, Location = 0, Slot = 65535, PersistentState = InventoryItemPersistentState.New },
		];

		var change = EquipmentService.ChangeEquipment(player, action: 0, slotRead: 2, itemObjectId: 1001, CreateItemTemplates(), skillTemplates: null);

		Assert.True(change.Changed);
		var sword = Assert.Single(change.InventoryItems);
		Assert.True(sword.IsEquipped);
		Assert.Equal(1, sword.Slot);
		Assert.Equal(InventoryItemPersistentState.New, sword.PersistentState);
		Assert.True(change.MarksEquipmentPersistentState);
		Assert.Equal([1001], change.PersistedItems.Select(item => item.ObjectId));
		Assert.Equal([1], change.InventoryUpdateItems.Select(item => item.Slot));
	}

	[Fact]
	public void ChangeEquipment_UnequipsUpdatedItemAndMarksItUpdateRequired()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem
			{
				ObjectId = 1001,
				ItemId = RobeId,
				Location = 0,
				IsEquipped = true,
				Slot = 8,
				PersistentState = InventoryItemPersistentState.Updated,
			},
		];

		var change = EquipmentService.ChangeEquipment(player, action: 1, slotRead: 0, itemObjectId: 1001, CreateItemTemplates(), skillTemplates: null);

		Assert.True(change.Changed);
		var updatedItem = Assert.Single(change.PersistedItems);
		Assert.False(updatedItem.IsEquipped);
		Assert.Equal(0, updatedItem.Slot);
		Assert.Equal(InventoryItemPersistentState.UpdateRequired, updatedItem.PersistentState);
		Assert.True(change.MarksEquipmentPersistentState);
	}

	[Fact]
	public void NormalizeImmediatelySavedItems_ClearsDirtyStateForPersistedEquipmentAndKinahRows()
	{
		var persistedEquipment = new InventoryItem
		{
			ObjectId = 1001,
			ItemId = SwordId,
			Location = 0,
			IsEquipped = true,
			Slot = 1,
			PersistentState = InventoryItemPersistentState.UpdateRequired,
		};
		var persistedKinah = new InventoryItem
		{
			ObjectId = 77,
			ItemId = KinahItemId,
			Location = 0,
			Count = 49_000,
			PersistentState = InventoryItemPersistentState.UpdateRequired,
		};
		var untouched = new InventoryItem
		{
			ObjectId = 2001,
			ItemId = RobeId,
			Location = 0,
			IsEquipped = false,
			Slot = 65535,
			PersistentState = InventoryItemPersistentState.New,
		};

		var normalized = EquipmentService.NormalizeImmediatelySavedItems(
			[persistedEquipment, persistedKinah, untouched],
			[persistedEquipment],
			persistedKinah);

		Assert.Equal(
			[
				(1001, InventoryItemPersistentState.Updated),
				(77, InventoryItemPersistentState.Updated),
				(2001, InventoryItemPersistentState.New),
			],
			normalized.Select(item => (item.ObjectId, item.PersistentState)).ToArray());
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
	public void ChangeEquipment_UnequippingPowerShardRequestsPowerShardOff()
	{
		var player = CreatePlayer();
		player.CreatureState = PlayerCreatureState.Powershard;
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = PowerShardId, Location = 0, IsEquipped = true, Slot = PowerShardRight },
		];

		var change = EquipmentService.ChangeEquipment(player, action: 1, slotRead: 0, itemObjectId: 1001, CreateItemTemplates(), skillTemplates: null);

		Assert.True(change.Changed);
		Assert.True(change.PowerShardDeactivated);
		Assert.True(player.IsInState(PlayerCreatureState.Powershard));
		Assert.Equal([(1001, false, 0L)], change.InventoryUpdateItems.Select(item => (item.ObjectId, item.IsEquipped, item.Slot)).ToArray());
	}

	[Fact]
	public void UsePowerShard_DecreasesEquippedShardStack()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = PowerShardId, Count = 3, Location = 0, IsEquipped = true, Slot = PowerShardRight },
		];

		var result = EquipmentService.UsePowerShard(player, powerShardObjectId: 1001, count: 1, itemTemplates: CreateItemTemplates());

		Assert.True(result.Changed);
		Assert.False(result.PowerShardDeactivated);
		Assert.Empty(result.DeletedItemObjectIds);
		Assert.Empty(result.EquipUpdateItems);
		var update = Assert.Single(result.CountUpdateItems);
		Assert.Equal((1001, 2L, true, PowerShardRight), (update.ObjectId, update.Count, update.IsEquipped, update.Slot));
		Assert.Equal(2, Assert.Single(result.InventoryItems).Count);
	}

	[Fact]
	public void UsePowerShard_EquipsNextSameStackAfterBurn()
	{
		var player = CreatePlayer();
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = PowerShardId, Count = 1, Location = 0, IsEquipped = true, Slot = PowerShardRight },
			new InventoryItem { ObjectId = 1002, ItemId = PowerShardId, Count = 50, Location = 0, Slot = 65535 },
			new InventoryItem { ObjectId = 1003, ItemId = RobeId, Count = 1, Location = 0, Slot = 65535 },
		];

		var result = EquipmentService.UsePowerShard(player, powerShardObjectId: 1001, count: 1, itemTemplates: CreateItemTemplates());

		Assert.True(result.Changed);
		Assert.False(result.PowerShardDeactivated);
		Assert.Equal([1001], result.DeletedItemObjectIds);
		Assert.Empty(result.CountUpdateItems);
		var replacement = Assert.Single(result.EquipUpdateItems);
		Assert.Equal((1002, 50L, true, PowerShardRight), (replacement.ObjectId, replacement.Count, replacement.IsEquipped, replacement.Slot));
		Assert.DoesNotContain(result.InventoryItems, item => item.ObjectId == 1001);
		Assert.Contains(result.InventoryItems, item => item.ObjectId == 1002 && item.IsEquipped && item.Slot == PowerShardRight);
	}

	[Fact]
	public void UsePowerShard_BurnsOutWhenNoReplacementStackExists()
	{
		var player = CreatePlayer();
		player.CreatureState = PlayerCreatureState.Powershard;
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = PowerShardId, Count = 1, Location = 0, IsEquipped = true, Slot = PowerShardRight },
		];

		var result = EquipmentService.UsePowerShard(player, powerShardObjectId: 1001, count: 5, itemTemplates: CreateItemTemplates());

		Assert.True(result.Changed);
		Assert.True(result.PowerShardDeactivated);
		Assert.Equal([1001], result.DeletedItemObjectIds);
		Assert.Empty(result.CountUpdateItems);
		Assert.Empty(result.EquipUpdateItems);
		Assert.Empty(result.InventoryItems);
		Assert.True(player.IsInState(PlayerCreatureState.Powershard));
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
	public void CheckRankLimitItems_UnequipsEquippedItemWhenRankFallsOutOfRange()
	{
		var player = CreatePlayer(rank: 1);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = RestrictedSwordId, Location = 0, IsEquipped = true, Slot = 1 },
		];

		var change = EquipmentService.CheckRankLimitItems(
			player,
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
				MaxRank: 9)));

		Assert.True(change.Changed);
		Assert.Equal([(1001, false, 0L)], change.PersistedItems.Select(item => (item.ObjectId, item.IsEquipped, item.Slot)).ToArray());
		Assert.Equal(["Abyss Sword"], change.RankLimitedUnequipMessages);
	}

	[Fact]
	public void CheckRankLimitItems_UsesFusionedItemRankLimits()
	{
		var player = CreatePlayer(rank: 1);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = SwordId, FusionedItem = RestrictedSwordId, Location = 0, IsEquipped = true, Slot = 1 },
		];

		var change = EquipmentService.CheckRankLimitItems(
			player,
			CreateItemTemplates(new ItemTemplateSummary(
				RestrictedSwordId,
				"Fused Abyss Sword",
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
				MaxRank: 9)));

		Assert.True(change.Changed);
		Assert.Equal([(1001, false, 0L)], change.InventoryUpdateItems.Select(item => (item.ObjectId, item.IsEquipped, item.Slot)).ToArray());
		Assert.Equal(["Practice Sword"], change.RankLimitedUnequipMessages);
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

	[Theory]
	[InlineData("dead", 1400059)]
	[InlineData("riding", 1400056)]
	[InlineData("chair", 1400058)]
	[InlineData("resting", 1400057)]
	[InlineData("gliding", 1400082)]
	[InlineData("flying", 1400055)]
	[InlineData("weapon", 1400079)]
	public void ChangeEquipment_DeniesSoulBindRequestInInvalidJavaStance(string stance, int expectedL10nId)
	{
		var player = CreatePlayer();
		ApplySoulBindStance(player, stance);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 1001, ItemId = SoulBoundSwordId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(player, action: 0, slotRead: 1, itemObjectId: 1001, CreateItemTemplates(), skillTemplates: null);

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.SoulBindInvalidStance, change.Failure);
		Assert.Equal(expectedL10nId, change.SoulBindInvalidStanceL10nId);
		Assert.Equal(0, change.SoulBindItemObjectId);
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

	[Fact]
	public void ChangeEquipment_EquipsStigmaWithKinahCostAndTemporarySkill()
	{
		var player = CreatePlayer(playerClass: "GLADIATOR", exp: 46);
		player.Quests = [new PlayerQuestState(1929, "COMPLETE", 0, 0, 1)];
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 77, ItemId = KinahItemId, Count = 50_000, Location = 0 },
			new InventoryItem { ObjectId = 1001, ItemId = StigmaId, Location = 0, Slot = 65535, Enchant = 2 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: StigmaSlot1,
			itemObjectId: 1001,
			CreateItemTemplates(),
			CreateStigmaSkillTemplates(),
			CreateExperienceTable(),
			skillTree: CreateStigmaSkillTree());

		Assert.True(change.Changed);
		Assert.Equal(25_000, change.KinahItemUpdate?.Count);
		Assert.Equal([(1001, true, StigmaSlot1)], change.PersistedItems.Select(item => (item.ObjectId, item.IsEquipped, item.Slot)).ToArray());
		var learnedSkill = Assert.Single(change.SkillListUpdates);
		Assert.Equal(500, learnedSkill.SkillId);
		Assert.Equal(3, learnedSkill.SkillLevel);
		Assert.Equal(1, learnedSkill.SkillType);
		Assert.Contains(change.FinalSkills, skill => skill.SkillId == 500 && skill.SkillLevel == 3 && skill.SkillType == 1);
	}

	[Fact]
	public void ChangeEquipment_MembershipPermissionUnlocksStigmaSlotsWithoutQuest()
	{
		var player = CreatePlayer(playerClass: "GLADIATOR", exp: 46, membership: 10);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 77, ItemId = KinahItemId, Count = 50_000, Location = 0 },
			new InventoryItem { ObjectId = 1001, ItemId = StigmaId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: StigmaSlot1,
			itemObjectId: 1001,
			CreateItemTemplates(),
			CreateStigmaSkillTemplates(),
			CreateExperienceTable(),
			skillTree: CreateStigmaSkillTree(),
			stigmaSlotQuestMembership: 10);

		Assert.True(change.Changed);
		Assert.Contains(change.InventoryItems, item => item.ObjectId == 1001 && item.IsEquipped);
	}

	[Fact]
	public void ChangeEquipment_EquipsSixthChargeableStigmaAndAddsLinkedSkill()
	{
		var player = CreatePlayer(playerClass: "GLADIATOR", exp: 60);
		player.Quests = [new PlayerQuestState(1929, "COMPLETE", 0, 0, 1)];
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 77, ItemId = KinahItemId, Count = 50_000, Location = 0 },
			new InventoryItem { ObjectId = 2001, ItemId = LinkedStigmaItemIds[0], Location = 0, IsEquipped = true, Slot = StigmaSlot1, Enchant = 4 },
			new InventoryItem { ObjectId = 2002, ItemId = LinkedStigmaItemIds[1], Location = 0, IsEquipped = true, Slot = StigmaSlot2, Enchant = 4 },
			new InventoryItem { ObjectId = 2003, ItemId = LinkedStigmaItemIds[2], Location = 0, IsEquipped = true, Slot = StigmaSlot3, Enchant = 4 },
			new InventoryItem { ObjectId = 2004, ItemId = LinkedStigmaItemIds[3], Location = 0, IsEquipped = true, Slot = AdvancedStigmaSlot1, Enchant = 4 },
			new InventoryItem { ObjectId = 2005, ItemId = LinkedStigmaItemIds[4], Location = 0, IsEquipped = true, Slot = AdvancedStigmaSlot2, Enchant = 4 },
			new InventoryItem { ObjectId = 1001, ItemId = StigmaId, Location = 0, Slot = 65535, Enchant = 2 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: AdvancedStigmaSlot3,
			itemObjectId: 1001,
			CreateItemTemplates(CreateLinkedStigmaTemplates()),
			CreateStigmaSkillTemplates(),
			CreateExperienceTable(),
			skillTree: CreateStigmaSkillTree());

		Assert.True(change.Changed);
		Assert.Contains(change.SkillListUpdates, skill => skill.SkillId == 500 && skill.SkillLevel == 3 && skill.SkillType == 1);
		Assert.Contains(change.SkillListUpdates, skill => skill.SkillId == 662 && skill.SkillLevel == 3 && skill.SkillType == 3);
		Assert.Contains(change.FinalSkills, skill => skill.SkillId == 662 && skill.SkillType == 3);
	}

	[Fact]
	public void ChangeEquipment_RejectsStigmaWhenKinahIsMissing()
	{
		var player = CreatePlayer(playerClass: "GLADIATOR", exp: 46);
		player.Quests = [new PlayerQuestState(1929, "COMPLETE", 0, 0, 1)];
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 77, ItemId = KinahItemId, Count = 10, Location = 0 },
			new InventoryItem { ObjectId = 1001, ItemId = StigmaId, Location = 0, Slot = 65535 },
		];

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 0,
			slotRead: StigmaSlot1,
			itemObjectId: 1001,
			CreateItemTemplates(),
			CreateStigmaSkillTemplates(),
			CreateExperienceTable(),
			skillTree: CreateStigmaSkillTree());

		Assert.False(change.Changed);
		Assert.Equal(EquipmentChangeFailure.StigmaNotEnoughKinah, change.Failure);
		Assert.Empty(change.PersistedItems);
		Assert.Empty(change.SkillListUpdates);
	}

	[Fact]
	public void ChangeEquipment_UnequipsStigmaAndRemovesTemporarySkill()
	{
		var player = CreatePlayer(playerClass: "GLADIATOR", exp: 46);
		player.InventoryItems =
		[
			new InventoryItem { ObjectId = 77, ItemId = KinahItemId, Count = 50_000, Location = 0 },
			new InventoryItem { ObjectId = 1001, ItemId = StigmaId, Location = 0, IsEquipped = true, Slot = StigmaSlot1 },
		];
		player.Skills =
		[
			new PlayerSkill { SkillId = 37, SkillLevel = 1 },
			new PlayerSkill { SkillId = 500, SkillLevel = 3, SkillType = 1 },
			new PlayerSkill { SkillId = 662, SkillLevel = 3, SkillType = 3 },
		];
		var skillTemplates = CreateStigmaSkillTemplates();

		var change = EquipmentService.ChangeEquipment(
			player,
			action: 1,
			slotRead: 0,
			itemObjectId: 1001,
			CreateItemTemplates(),
			skillTemplates,
			CreateExperienceTable(),
			skillTree: CreateStigmaSkillTree());

		Assert.True(change.Changed);
		Assert.DoesNotContain(change.FinalSkills, skill => skill.SkillId == 500);
		Assert.DoesNotContain(change.FinalSkills, skill => skill.SkillId == 662);
		Assert.Equal([500, 662], change.SkillRemoveUpdates.Select(skill => skill.SkillId).ToArray());
		Assert.Single(change.StigmaSkillRemoveMessages);
		var hiddenMessage = Assert.Single(change.HiddenStigmaSkillRemoveMessages);
		Assert.Equal(skillTemplates.GetSkillTemplate(662)?.GetClientName(), hiddenMessage.FirstSkillName);
		Assert.Equal(3, hiddenMessage.SkillLevel);
	}

	private static Player CreatePlayer(
		string playerClass = "WARRIOR",
		string race = "ELYOS",
		string gender = "MALE",
		long exp = 0,
		int rank = 1,
		byte membership = 0)
	{
		return new Player
		{
			ObjectId = 7001,
			AccountId = 10,
			Name = "EquipTester",
			PlayerClass = playerClass,
			Race = race,
			Gender = gender,
			AccountMembership = membership,
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

	private static void ApplySoulBindStance(Player player, string stance)
	{
		switch (stance)
		{
			case "dead":
				player.LifeStats = new PlayerLifeStats(0, 0, 0);
				break;
			case "riding":
				player.IsInRideMode = true;
				break;
			case "chair":
				player.CreatureState = PlayerCreatureState.Chair;
				break;
			case "resting":
				player.CreatureState = PlayerCreatureState.Resting;
				break;
			case "gliding":
				player.CreatureState = PlayerCreatureState.Gliding;
				break;
			case "flying":
				player.CreatureState = PlayerCreatureState.Flying;
				break;
			case "weapon":
				player.CreatureState = PlayerCreatureState.WeaponEquipped;
				break;
		}
	}

	private static ItemTemplateTable CreateItemTemplates(params ItemTemplateSummary[] extraTemplates)
	{
		var templates = new List<ItemTemplateSummary>
		{
			new(SwordId, "Practice Sword", 0, 1, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3, RequiredLevels: RequiredLevels(1)),
			new(SoulBoundSwordId, "Practice Soulbound Sword", 0, 1 << 7, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3, RequiredLevels: RequiredLevels(1)),
			new(GreatswordId, "Practice Greatsword", 0, 1, 1, "GREATSWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3, RequiredLevels: RequiredLevels(1)),
			new(RobeId, "Practice Robe", 0, 1, 1, "CL_TORSO", "NORMAL", "COMMON", "PC_ALL", 1, 0, 8, RequiredLevels: RequiredLevels(1)),
			new(PowerShardId, "Practice Power Shard", 0, 1, 1, "POWER_SHARDS", "NORMAL", "COMMON", "PC_ALL", 1000, 0, PowerShardRight | PowerShardLeft, RequiredLevels: RequiredLevels(1)),
			new(StigmaId, "Practice Stigma", 0, 1, 20, "STIGMA", "NORMAL", "COMMON", "PC_ALL", 1, 0, StigmaSlot1 | StigmaSlot2 | StigmaSlot3 | AdvancedStigmaSlot1 | AdvancedStigmaSlot2 | AdvancedStigmaSlot3, StigmaInfo: new ItemStigmaInfo(["STIGMA_TEST"], Chargeable: true), RequiredLevels: RequiredLevels(("GLADIATOR", 20))),
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

	private static SkillTemplateTable CreateStigmaSkillTemplates()
	{
		return new SkillTemplateTable(
		[
			new SkillTemplateSummary(
				500,
				"Practice Stigma Skill",
				200,
				1,
				"STIGMA_TEST",
				"STIGMA_TEST",
				"PHYSICAL",
				"ATTACK",
				0,
				0,
				StigmaType: "NORMAL"),
			new SkillTemplateSummary(
				662,
				"Practice Linked Stigma Skill",
				201,
				1,
				"LINKED_STIGMA_TEST",
				"LINKED_STIGMA_TEST",
				"PHYSICAL",
				"ATTACK",
				0,
				0,
				StigmaType: "LINKED"),
		]);
	}

	private static SkillTreeTable CreateStigmaSkillTree()
	{
		return new SkillTreeTable(
		[
			new SkillLearnSummary("GLADIATOR", 500, null, "PC_ALL", 20, AutoLearn: false, Stigma: 1, SkillLevel: 0),
			new SkillLearnSummary("GLADIATOR", 662, null, "ELYOS", 55, AutoLearn: false, Stigma: 4, SkillLevel: 0),
		], CreateStigmaSkillTemplates());
	}

	private static ItemTemplateSummary[] CreateLinkedStigmaTemplates()
	{
		return LinkedStigmaItemIds
			.Select(itemId => new ItemTemplateSummary(
				itemId,
				$"Linked Stigma {itemId}",
				0,
				1,
				20,
				"STIGMA",
				"NORMAL",
				"COMMON",
				"PC_ALL",
				1,
				0,
				StigmaSlot1 | StigmaSlot2 | StigmaSlot3 | AdvancedStigmaSlot1 | AdvancedStigmaSlot2 | AdvancedStigmaSlot3,
				StigmaInfo: new ItemStigmaInfo(["STIGMA_OTHER"], Chargeable: true),
				RequiredLevels: RequiredLevels(("GLADIATOR", 20))))
			.ToArray();
	}

	private static PlayerExperienceTable CreateExperienceTable()
	{
		return new PlayerExperienceTable(Enumerable.Range(0, 70).Select(level => (long)level).ToArray());
	}

	private const int KinahItemId = 182400001;
	private const int SwordId = 100000001;
	private const int SoulBoundSwordId = 100000003;
	private const int GreatswordId = 100100001;
	private const int RobeId = 110100001;
	private const int PowerShardId = 166000001;
	private const long PowerShardRight = 1L << 13;
	private const long PowerShardLeft = 1L << 14;
	private const int RestrictedSwordId = 100000002;
	private const int StigmaId = 140001001;
	private const long StigmaSlot1 = 1L << 30;
	private const long StigmaSlot2 = 1L << 31;
	private const long StigmaSlot3 = 1L << 32;
	private const long AdvancedStigmaSlot1 = 1L << 33;
	private const long AdvancedStigmaSlot2 = 1L << 34;
	private const long AdvancedStigmaSlot3 = 1L << 35;
	private static readonly int[] LinkedStigmaItemIds = [140001010, 140001011, 140001012, 140001013, 140001014];
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
