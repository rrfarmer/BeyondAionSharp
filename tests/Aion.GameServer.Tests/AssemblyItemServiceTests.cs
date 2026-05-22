using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AssemblyItemServiceTests
{
	[Fact]
	public void CanAct_RequiresAssemblyRecipeAndParts()
	{
		var assemblyItems = new AssemblyItemTable([new AssemblyItemSummary(900, [100, 101])]);
		var sourceTemplate = CreateTemplate(100, assemblyItemId: 900);
		var player = new Player
		{
			InventoryItems = [new InventoryItem { ObjectId = 1, ItemId = 100, Location = 0, Count = 1 }],
		};

		var result = AssemblyItemService.CanAct(player, sourceTemplate, assemblyItems);

		Assert.False(result.Succeeded);
		Assert.Equal(AssemblyItemFailure.MissingPart, result.Failure);
	}

	[Fact]
	public void CreateMutationPlan_ConsumesPartsAndAddsReward()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100),
			CreateTemplate(101),
			CreateTemplate(900),
		]);
		var assemblyItem = new AssemblyItemSummary(900, [100, 101]);
		var rewardTemplate = itemTemplates.GetItemTemplate(900)!;
		var player = new Player
		{
			ObjectId = 700,
			InventoryItems =
			[
				new InventoryItem { ObjectId = 1, ItemId = 100, Location = 0, Count = 3, OwnerId = 700 },
				new InventoryItem { ObjectId = 2, ItemId = 101, Location = 0, Count = 1, OwnerId = 700 },
			],
		};

		var plan = AssemblyItemService.CreateMutationPlan(
			player,
			player.InventoryItems,
			assemblyItem,
			rewardTemplate,
			itemTemplates,
			() => 9001);

		Assert.True(plan.Succeeded);
		Assert.True(plan.RewardSucceeded);
		var updatedPart = Assert.Single(plan.UpdatedPartItems);
		Assert.Equal(1, updatedPart.ObjectId);
		Assert.Equal(2, updatedPart.Count);
		Assert.Equal([2], plan.DeletedPartObjectIds);
		var reward = Assert.Single(plan.AddedRewardItems);
		Assert.Equal(9001, reward.ObjectId);
		Assert.Equal(900, reward.ItemId);
		Assert.Equal(1, reward.Count);
		Assert.Empty(plan.UpdatedRewardItems);
	}

	[Fact]
	public void CreateMutationPlan_ConsumesPartsWhenRewardInventoryIsFull()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateTemplate(100),
			CreateTemplate(200),
			CreateTemplate(300),
		]);
		var player = new Player
		{
			ObjectId = 700,
			InventoryItems = Enumerable.Range(0, 26)
				.Select(index => new InventoryItem { ObjectId = 1000 + index, ItemId = 300, Location = 0, Count = 1, OwnerId = 700 })
				.Prepend(new InventoryItem { ObjectId = 1, ItemId = 100, Location = 0, Count = 2, OwnerId = 700 })
				.ToArray(),
		};

		var plan = AssemblyItemService.CreateMutationPlan(
			player,
			player.InventoryItems,
			new AssemblyItemSummary(200, [100]),
			itemTemplates.GetItemTemplate(200)!,
			itemTemplates,
			() => 9001);

		Assert.True(plan.Succeeded);
		Assert.False(plan.RewardSucceeded);
		var updatedPart = Assert.Single(plan.UpdatedPartItems);
		Assert.Equal(1, updatedPart.ObjectId);
		Assert.Equal(1, updatedPart.Count);
		Assert.Empty(plan.AddedRewardItems);
		Assert.Equal(1, plan.RewardRemainingCount);
	}

	private static ItemTemplateSummary CreateTemplate(int itemId, int assemblyItemId = 0)
	{
		return new ItemTemplateSummary(
			itemId,
			"Item " + itemId.ToString(),
			0,
			0,
			1,
			"NONE",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			0,
			AssemblyItemId: assemblyItemId);
	}
}
