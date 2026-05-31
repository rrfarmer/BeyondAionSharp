using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class InventoryAddServiceTests
{
	[Fact]
	public void CreateAddItemPlan_MergesExistingStackBeforeCreatingRows()
	{
		var template = CreateTemplate(200, maxStackCount: 10);
		var player = new Player { ObjectId = 1000 };
		var inventoryItems = new[]
		{
			new InventoryItem { ObjectId = 1, ItemId = 200, Count = 7, OwnerId = player.ObjectId, Location = 0 },
		};
		var objectIdsUsed = 0;

		var plan = InventoryAddService.CreateAddItemPlan(player, inventoryItems, template, 3, () => ++objectIdsUsed);

		Assert.True(plan.Succeeded);
		Assert.Equal(0, objectIdsUsed);
		Assert.Empty(plan.AddedItems);
		var updatedItem = Assert.Single(plan.UpdatedItems);
		Assert.Equal(1, updatedItem.ObjectId);
		Assert.Equal(10, updatedItem.Count);
	}

	[Fact]
	public void CreateAddItemPlan_SplitsOverflowIntoNewStackRows()
	{
		var template = CreateTemplate(200, maxStackCount: 10);
		var player = new Player { ObjectId = 1000 };
		var inventoryItems = new[]
		{
			new InventoryItem { ObjectId = 1, ItemId = 200, Count = 8, OwnerId = player.ObjectId, Location = 0 },
		};
		var nextObjectId = 99;

		var plan = InventoryAddService.CreateAddItemPlan(player, inventoryItems, template, 17, () => ++nextObjectId);

		Assert.True(plan.Succeeded);
		Assert.Equal(0, plan.RemainingCount);
		Assert.Equal(10, Assert.Single(plan.UpdatedItems).Count);
		Assert.Collection(
			plan.AddedItems,
			item =>
			{
				Assert.Equal(100, item.ObjectId);
				Assert.Equal(10, item.Count);
				Assert.Equal(65535, item.Slot);
				Assert.Equal(player.ObjectId, item.OwnerId);
			},
			item =>
			{
				Assert.Equal(101, item.ObjectId);
				Assert.Equal(5, item.Count);
			});
	}

	[Fact]
	public void CreateAddItemPlan_MergesEquippedPowerShardBeforeCubeStack()
	{
		var template = CreateTemplate(166000001, maxStackCount: 1000, itemGroup: "POWER_SHARDS");
		var player = new Player { ObjectId = 1000 };
		var inventoryItems = new[]
		{
			new InventoryItem { ObjectId = 1, ItemId = 166000001, Count = 998, OwnerId = player.ObjectId, Location = 0 },
			new InventoryItem { ObjectId = 2, ItemId = 166000001, Count = 998, OwnerId = player.ObjectId, Location = 0, IsEquipped = true },
		};

		var plan = InventoryAddService.CreateAddItemPlan(player, inventoryItems, template, 3, () => 99);

		Assert.True(plan.Succeeded);
		Assert.Empty(plan.AddedItems);
		Assert.Collection(
			plan.UpdatedItems,
			item =>
			{
				Assert.Equal(2, item.ObjectId);
				Assert.Equal(1000, item.Count);
			},
			item =>
			{
				Assert.Equal(1, item.ObjectId);
				Assert.Equal(999, item.Count);
			});
	}

	[Fact]
	public void CreateAddItemPlan_IgnoresEquippedStacksForNormalStackables()
	{
		var template = CreateTemplate(200, maxStackCount: 10);
		var player = new Player { ObjectId = 1000 };
		var inventoryItems = new[]
		{
			new InventoryItem { ObjectId = 1, ItemId = 200, Count = 7, OwnerId = player.ObjectId, Location = 0, IsEquipped = true },
		};

		var plan = InventoryAddService.CreateAddItemPlan(player, inventoryItems, template, 1, () => 99);

		Assert.True(plan.Succeeded);
		Assert.Empty(plan.UpdatedItems);
		var addedItem = Assert.Single(plan.AddedItems);
		Assert.Equal(99, addedItem.ObjectId);
		Assert.Equal(1, addedItem.Count);
	}

	[Fact]
	public void CreateAddItemPlan_ReturnsRemainingWhenInventoryIsFull()
	{
		var template = CreateTemplate(200, maxStackCount: 1);
		var player = new Player { ObjectId = 1000 };
		var inventoryItems = Enumerable.Range(0, 27)
			.Select(index => new InventoryItem { ObjectId = index + 1, ItemId = 300 + index, Count = 1, OwnerId = player.ObjectId, Location = 0 })
			.ToArray();

		var plan = InventoryAddService.CreateAddItemPlan(player, inventoryItems, template, 1, () => 100);

		Assert.False(plan.Succeeded);
		Assert.Equal(1, plan.RemainingCount);
		Assert.True(plan.InventoryFull);
		Assert.Empty(plan.UpdatedItems);
		Assert.Empty(plan.AddedItems);
	}

	[Fact]
	public void CreateAddItemPlan_PreservesPartialStackMergeWhenInventoryFills()
	{
		var template = CreateTemplate(200, maxStackCount: 10);
		var fillerTemplate = CreateTemplate(300, maxStackCount: 1);
		var itemTemplates = new ItemTemplateTable([template, fillerTemplate]);
		var player = new Player { ObjectId = 1000 };
		var inventoryItems = Enumerable.Range(0, 26)
			.Select(index => new InventoryItem { ObjectId = index + 2, ItemId = 300, Count = 1, OwnerId = player.ObjectId, Location = 0 })
			.Prepend(new InventoryItem { ObjectId = 1, ItemId = 200, Count = 8, OwnerId = player.ObjectId, Location = 0 })
			.ToArray();

		var plan = InventoryAddService.CreateAddItemPlan(player, inventoryItems, template, 5, () => 100, itemTemplates: itemTemplates);

		Assert.False(plan.Succeeded);
		Assert.True(plan.InventoryFull);
		Assert.Equal(3, plan.RemainingCount);
		Assert.Empty(plan.AddedItems);
		var updatedItem = Assert.Single(plan.UpdatedItems);
		Assert.Equal(1, updatedItem.ObjectId);
		Assert.Equal(10, updatedItem.Count);
	}

	[Fact]
	public void CreateAddItemPlan_UsesSpecialCubeCapacityForSpecialItems()
	{
		var template = CreateTemplate(200, maxStackCount: 1, extraInventoryId: 2);
		var itemTemplates = new ItemTemplateTable([template]);
		var player = new Player { ObjectId = 1000 };
		var inventoryItems = Enumerable.Range(0, 27)
			.Select(index => new InventoryItem { ObjectId = index + 1, ItemId = 300 + index, Count = 1, OwnerId = player.ObjectId, Location = 0 })
			.ToArray();

		var plan = InventoryAddService.CreateAddItemPlan(player, inventoryItems, template, 1, () => 100, itemTemplates: itemTemplates);

		Assert.True(plan.Succeeded);
		var addedItem = Assert.Single(plan.AddedItems);
		Assert.Equal(200, addedItem.ItemId);
		Assert.Equal(100, addedItem.ObjectId);
	}

	[Fact]
	public void CreateAddItemPlan_ReturnsRemainingWhenSpecialCubeIsFull()
	{
		var template = CreateTemplate(200, maxStackCount: 1, extraInventoryId: 2);
		var fillerTemplate = CreateTemplate(300, maxStackCount: 1, extraInventoryId: 2);
		var itemTemplates = new ItemTemplateTable([template, fillerTemplate]);
		var player = new Player { ObjectId = 1000 };
		var inventoryItems = Enumerable.Range(0, 102)
			.Select(index => new InventoryItem { ObjectId = index + 1, ItemId = 300, Count = 1, OwnerId = player.ObjectId, Location = 0 })
			.ToArray();

		var plan = InventoryAddService.CreateAddItemPlan(player, inventoryItems, template, 1, () => 100, itemTemplates: itemTemplates);

		Assert.False(plan.Succeeded);
		Assert.Equal(1, plan.RemainingCount);
		Assert.True(plan.InventoryFull);
		Assert.Empty(plan.AddedItems);
	}

	[Fact]
	public void CreateAddItemPlan_CopiesNonStackableSourceItemInfo()
	{
		var template = CreateTemplate(200, maxStackCount: 1);
		var player = new Player { ObjectId = 1000 };
		var sourceItem = new InventoryItem
		{
			ObjectId = 1,
			ItemId = 200,
			Count = 1,
			Color = 123,
			ColorExpires = 456,
			Creator = "maker",
			OwnerId = player.ObjectId,
			IsSoulBound = true,
			Location = 0,
			Slot = 65535,
			Enchant = 12,
			EnchantBonus = 4,
			ItemSkin = 300,
			FusionedItem = 900,
			OptionalSocket = 2,
			OptionalFusionSocket = 3,
			Charge = 5,
			TuneCount = 7,
			RandomBonus = 8,
			FusionRandomBonus = 9,
			Tempering = 10,
			IsAmplified = true,
			BuffSkill = 11,
		};
		sourceItem.ManaStones = [new ItemStoneSocket(167000001, 0)];
		sourceItem.FusionStones = [new ItemStoneSocket(167000002, 1)];
		sourceItem.Godstone = new PlayerGodstone(168000001, ProcCount: 6);
		sourceItem.IdianStone = new PlayerIdianStone(169000001, PolishNumber: 2, PolishCharge: 1000);

		var plan = InventoryAddService.CreateAddItemPlan(
			player,
			Array.Empty<InventoryItem>(),
			template,
			1,
			() => 99,
			sourceItem: sourceItem);

		Assert.True(plan.Succeeded);
		var addedItem = Assert.Single(plan.AddedItems);
		Assert.Equal((99, 200, 1L, player.ObjectId, 0, 65535L), (addedItem.ObjectId, addedItem.ItemId, addedItem.Count, addedItem.OwnerId, addedItem.Location, addedItem.Slot));
		Assert.Equal(sourceItem.Color, addedItem.Color);
		Assert.Equal(sourceItem.ColorExpires, addedItem.ColorExpires);
		Assert.Equal(sourceItem.Creator, addedItem.Creator);
		Assert.Equal(sourceItem.IsSoulBound, addedItem.IsSoulBound);
		Assert.Equal(sourceItem.Enchant, addedItem.Enchant);
		Assert.Equal(sourceItem.EnchantBonus, addedItem.EnchantBonus);
		Assert.Equal(sourceItem.ItemSkin, addedItem.ItemSkin);
		Assert.Equal(sourceItem.OptionalSocket, addedItem.OptionalSocket);
		Assert.Equal(sourceItem.TuneCount, addedItem.TuneCount);
		Assert.Equal(sourceItem.RandomBonus, addedItem.RandomBonus);
		Assert.Equal(sourceItem.Tempering, addedItem.Tempering);
		Assert.Equal(sourceItem.IsAmplified, addedItem.IsAmplified);
		Assert.Equal(sourceItem.BuffSkill, addedItem.BuffSkill);
		Assert.Equal(sourceItem.ManaStones, addedItem.ManaStones);
		Assert.Equal(sourceItem.Godstone, addedItem.Godstone);
		Assert.Equal(sourceItem.IdianStone, addedItem.IdianStone);
		Assert.Equal(0, addedItem.FusionedItem);
		Assert.Equal(0, addedItem.OptionalFusionSocket);
		Assert.Equal(0, addedItem.FusionRandomBonus);
		Assert.Equal(0, addedItem.Charge);
		Assert.Empty(addedItem.FusionStones);
	}

	[Fact]
	public void CreateAddItemPlan_DoesNotCopySourceItemInfoForStackableRows()
	{
		var template = CreateTemplate(200, maxStackCount: 10);
		var player = new Player { ObjectId = 1000 };
		var sourceItem = new InventoryItem
		{
			ObjectId = 1,
			ItemId = 200,
			Count = 5,
			OwnerId = player.ObjectId,
			Enchant = 12,
			OptionalSocket = 2,
			RandomBonus = 8,
			IsSoulBound = true,
		};

		var plan = InventoryAddService.CreateAddItemPlan(
			player,
			Array.Empty<InventoryItem>(),
			template,
			5,
			() => 99,
			sourceItem: sourceItem);

		Assert.True(plan.Succeeded);
		var addedItem = Assert.Single(plan.AddedItems);
		Assert.Equal(99, addedItem.ObjectId);
		Assert.Equal(5, addedItem.Count);
		Assert.Equal(0, addedItem.Enchant);
		Assert.Equal(0, addedItem.OptionalSocket);
		Assert.Equal(0, addedItem.RandomBonus);
		Assert.False(addedItem.IsSoulBound);
	}

	private static ItemTemplateSummary CreateTemplate(int itemId, int maxStackCount, int extraInventoryId = -1, string itemGroup = "NONE")
	{
		return new ItemTemplateSummary(
			itemId,
			"Reward",
			DescriptionId: 1,
			Mask: 0,
			Level: 1,
			ItemGroup: itemGroup,
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: maxStackCount,
			Price: 0,
			ValidEquipmentSlots: 0,
			ExtraInventoryId: extraInventoryId);
	}
}
