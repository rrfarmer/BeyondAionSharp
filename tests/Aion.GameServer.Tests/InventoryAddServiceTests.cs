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
		Assert.Empty(plan.UpdatedItems);
		Assert.Empty(plan.AddedItems);
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
		Assert.Empty(plan.AddedItems);
	}

	private static ItemTemplateSummary CreateTemplate(int itemId, int maxStackCount, int extraInventoryId = -1)
	{
		return new ItemTemplateSummary(
			itemId,
			"Reward",
			DescriptionId: 1,
			Mask: 0,
			Level: 1,
			ItemGroup: "NONE",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: maxStackCount,
			Price: 0,
			ValidEquipmentSlots: 0,
			ExtraInventoryId: extraInventoryId);
	}
}
