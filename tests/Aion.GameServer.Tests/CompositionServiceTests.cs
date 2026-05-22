using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CompositionServiceTests
{
	[Fact]
	public void CanAct_RequiresCompositionToolAndEnchantStones()
	{
		var toolTemplate = CreateTemplate(165010000, "COMBINATION", hasCompositionAction: true);
		var firstTemplate = CreateTemplate(166000010, "ENCHANTMENT", level: 10);
		var secondTemplate = CreateTemplate(166000011, "NONE", level: 10);

		var validation = CompositionService.CanAct(
			toolTemplate,
			new InventoryItem { ObjectId = 2, ItemId = 166000010, Count = 1, Location = 0 },
			firstTemplate,
			new InventoryItem { ObjectId = 3, ItemId = 166000011, Count = 1, Location = 0 },
			secondTemplate);

		Assert.False(validation.Succeeded);
		Assert.Equal(CompositionFailure.InvalidSecondStone, validation.Failure);
	}

	[Fact]
	public void CreateMutationPlan_ConsumesInputsAndAddsCalculatedReward()
	{
		var toolTemplate = CreateTemplate(165010000, "COMBINATION", hasCompositionAction: true);
		var firstTemplate = CreateTemplate(166000020, "ENCHANTMENT", level: 20);
		var secondTemplate = CreateTemplate(166000030, "ENCHANTMENT", level: 30);
		var rewardTemplate = CreateTemplate(166000024, "ENCHANTMENT", level: 24);
		var itemTemplates = new ItemTemplateTable([toolTemplate, firstTemplate, secondTemplate, rewardTemplate]);
		var player = new Player
		{
			ObjectId = 700,
			InventoryItems =
			[
				new InventoryItem { ObjectId = 1, ItemId = 165010000, Count = 2, Location = 0, OwnerId = 700 },
				new InventoryItem { ObjectId = 2, ItemId = 166000020, Count = 1, Location = 0, OwnerId = 700 },
				new InventoryItem { ObjectId = 3, ItemId = 166000030, Count = 1, Location = 0, OwnerId = 700 },
			],
		};

		var plan = CompositionService.CreateMutationPlan(
			player,
			player.InventoryItems,
			toolItemId: 165010000,
			firstItemId: 166000020,
			firstItemLevel: 20,
			secondItemId: 166000030,
			secondItemLevel: 30,
			itemTemplates,
			(min, max) => min,
			() => 9001);

		Assert.True(plan.Succeeded);
		Assert.True(plan.RewardSucceeded);
		Assert.Equal(166000024, plan.RewardItemId);
		var updatedTool = Assert.Single(plan.UpdatedConsumedItems);
		Assert.Equal(1, updatedTool.ObjectId);
		Assert.Equal(1, updatedTool.Count);
		Assert.Equal([2, 3], plan.DeletedConsumedObjectIds);
		var reward = Assert.Single(plan.AddedRewardItems);
		Assert.Equal(9001, reward.ObjectId);
		Assert.Equal(166000024, reward.ItemId);
		Assert.Empty(plan.UpdatedRewardItems);
	}

	[Fact]
	public void CreateMutationPlan_ConsumesWhatJavaDecreaseByItemIdCanConsumeWhenSecondStoneIsMissing()
	{
		var toolTemplate = CreateTemplate(165010000, "COMBINATION", hasCompositionAction: true);
		var firstTemplate = CreateTemplate(166000020, "ENCHANTMENT", level: 20);
		var itemTemplates = new ItemTemplateTable([toolTemplate, firstTemplate]);
		var player = new Player
		{
			ObjectId = 700,
			InventoryItems =
			[
				new InventoryItem { ObjectId = 1, ItemId = 165010000, Count = 1, Location = 0, OwnerId = 700 },
				new InventoryItem { ObjectId = 2, ItemId = 166000020, Count = 1, Location = 0, OwnerId = 700 },
			],
		};

		var plan = CompositionService.CreateMutationPlan(
			player,
			player.InventoryItems,
			toolItemId: 165010000,
			firstItemId: 166000020,
			firstItemLevel: 20,
			secondItemId: 166000030,
			secondItemLevel: 30,
			itemTemplates,
			(min, max) => min,
			() => 9001);

		Assert.True(plan.Succeeded);
		Assert.False(plan.RewardSucceeded);
		Assert.Equal([1, 2], plan.DeletedConsumedObjectIds);
		Assert.Empty(plan.AddedRewardItems);
	}

	private static ItemTemplateSummary CreateTemplate(
		int itemId,
		string itemGroup,
		int level = 1,
		bool hasCompositionAction = false)
	{
		return new ItemTemplateSummary(
			itemId,
			"Item " + itemId.ToString(),
			0,
			0,
			level,
			itemGroup,
			"NORMAL",
			"COMMON",
			"PC_ALL",
			100,
			0,
			0,
			HasCompositionAction: hasCompositionAction);
	}
}
