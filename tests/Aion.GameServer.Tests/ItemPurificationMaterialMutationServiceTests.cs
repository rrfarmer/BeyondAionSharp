using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationMaterialMutationServiceTests
{
	[Fact]
	public void CreateDecreaseMaterialsPlan_ConsumesMaterialsPlansApAndDeletesBaseItem()
	{
		var baseItem = new InventoryItem { ObjectId = 10, ItemId = 100000001, Count = 1, Location = 0 };
		var player = CreatePlayer(
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
			new InventoryItem { ObjectId = 21, ItemId = 186000001, Count = 3, Location = 0 },
			new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 5_000, Location = 0 });

		var plan = ItemPurificationMaterialMutationService.CreateDecreaseMaterialsPlan(
			player,
			baseItem,
			[new ItemPurificationMaterialRequirement(186000001, 3)],
			necessaryAbyssPoints: 1_200,
			necessaryKinah: 1_000);

		Assert.True(plan.Succeeded);
		Assert.Equal(1_200, plan.AbyssPointsToSpend);
		Assert.Equal(1_000, plan.NecessaryKinah);
		Assert.False(plan.KinahSpendApplied);
		Assert.True(plan.BaseItemDeleteAttempted);
		Assert.True(plan.BaseItemDeleted);
		Assert.Equal([20, 10], plan.DeletedObjectIds);
		var materialUpdate = Assert.Single(plan.UpdatedItems);
		Assert.Equal(21, materialUpdate.ObjectId);
		Assert.Equal(1, materialUpdate.Count);
		Assert.Equal(
			[
				new ItemPurificationMutationStep(186000001, 20, 1, 0, IsKinah: false),
				new ItemPurificationMutationStep(186000001, 21, 2, 1, IsKinah: false),
				new ItemPurificationMutationStep(100000001, 10, 1, 0, IsKinah: false),
			],
			plan.MutationSteps);
		Assert.Equal([10, 20, 21, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
	}

	[Fact]
	public void CreateDecreaseMaterialsPlan_PreservesJavaPartialMaterialConsumptionOnLateFailure()
	{
		var baseItem = new InventoryItem { ObjectId = 10, ItemId = 100000001, Count = 1, Location = 0 };
		var player = CreatePlayer(
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 },
			new InventoryItem { ObjectId = 21, ItemId = 186000002, Count = 1, Location = 0 });

		var plan = ItemPurificationMaterialMutationService.CreateDecreaseMaterialsPlan(
			player,
			baseItem,
			[
				new ItemPurificationMaterialRequirement(186000001, 1),
				new ItemPurificationMaterialRequirement(186000002, 2),
			],
			necessaryAbyssPoints: 1_200,
			necessaryKinah: 0);

		Assert.False(plan.Succeeded);
		Assert.Equal(ItemPurificationMaterialMutationStatus.MissingRequiredMaterial, plan.Status);
		Assert.Equal(186000002, plan.MissingItemId);
		Assert.Equal(1, plan.MissingCount);
		Assert.Equal([20, 21], plan.DeletedObjectIds);
		Assert.Empty(plan.UpdatedItems);
		Assert.Equal(
			[
				new ItemPurificationMutationStep(186000001, 20, 1, 0, IsKinah: false),
				new ItemPurificationMutationStep(186000002, 21, 1, 0, IsKinah: false),
			],
			plan.MutationSteps);
		Assert.Equal(0, plan.AbyssPointsToSpend);
		Assert.False(plan.BaseItemDeleteAttempted);
	}

	[Fact]
	public void CreateDecreaseMaterialsPlan_DocumentsJavaKinahNegativeSpendAsNoMutation()
	{
		var baseItem = new InventoryItem { ObjectId = 10, ItemId = 100000001, Count = 1, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 5_000, Location = 0 };
		var player = CreatePlayer(baseItem, kinah);

		var plan = ItemPurificationMaterialMutationService.CreateDecreaseMaterialsPlan(
			player,
			baseItem,
			Array.Empty<ItemPurificationMaterialRequirement>(),
			necessaryAbyssPoints: 0,
			necessaryKinah: 1_000);

		Assert.True(plan.Succeeded);
		Assert.False(plan.KinahSpendApplied);
		Assert.Equal(1_000, plan.NecessaryKinah);
		Assert.DoesNotContain(plan.MutationSteps, step => step.IsKinah);
		Assert.DoesNotContain(plan.UpdatedItems, item => item.ObjectId == kinah.ObjectId);
	}

	[Fact]
	public void CreateDecreaseMaterialsPlan_IgnoresBaseDeleteFailureLikeJavaCaller()
	{
		var baseItem = new InventoryItem { ObjectId = 10, ItemId = 100000001, Count = 1, Location = 0 };
		var player = CreatePlayer();

		var plan = ItemPurificationMaterialMutationService.CreateDecreaseMaterialsPlan(
			player,
			baseItem,
			Array.Empty<ItemPurificationMaterialRequirement>(),
			necessaryAbyssPoints: 0,
			necessaryKinah: 0);

		Assert.True(plan.Succeeded);
		Assert.True(plan.BaseItemDeleteAttempted);
		Assert.False(plan.BaseItemDeleted);
		Assert.Empty(plan.DeletedObjectIds);
	}

	private static Player CreatePlayer(params InventoryItem[] items)
	{
		return new Player
		{
			ObjectId = 700,
			InventoryItems = items,
		};
	}
}
