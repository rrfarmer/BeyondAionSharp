using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationLiveMutationServiceTests
{
	[Fact]
	public void Apply_MutatesInventoryAndSpendsApWithoutKinahMutation()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);

		var result = ItemPurificationLiveMutationService.Apply(
			player,
			application,
			npcExpands: 1,
			questExpands: 0,
			itemExpands: 1);

		Assert.True(result.Succeeded);
		Assert.Equal(ItemPurificationLiveMutationStatus.Ready, result.Status);
		Assert.NotNull(result.MutationPreview);
		Assert.True(result.MutationPreview.Succeeded);
		Assert.Equal([5, 7], result.MutationPreview.CubeSnapshotsByPacketOperationIndex.Keys.Order().ToArray());
		Assert.Equal(1, result.MutationPreview.CubeSnapshotsByPacketOperationIndex[5].ItemsCount);
		Assert.Equal(2, result.MutationPreview.CubeSnapshotsByPacketOperationIndex[7].ItemsCount);
		Assert.NotNull(result.AbyssPointsPlan);
		Assert.True(result.AbyssPointsPlan.Applied);
		Assert.Equal(-1_200, result.AbyssPointsPlan.Added);
		Assert.Equal(3_800, player.AbyssRank.Ap);
		Assert.Equal([20, 30, 9001], player.InventoryItems.Select(item => item.ObjectId).Order().ToArray());
		Assert.DoesNotContain(player.InventoryItems, item => item.ObjectId == baseItem.ObjectId);
		Assert.Equal(1, player.InventoryItems.Single(item => item.ObjectId == material.ObjectId).Count);
		Assert.Equal(10_000, player.InventoryItems.Single(item => item.ObjectId == kinah.ObjectId).Count);
		var target = player.InventoryItems.Single(item => item.ObjectId == 9001);
		Assert.Equal(100000002, target.ItemId);
		Assert.Equal(20, target.Enchant);
		Assert.Equal(7, target.RandomBonus);
		Assert.Same(result.AppliedInventoryItems, player.InventoryItems);
	}

	[Fact]
	public void Apply_DoesNotMutateWhenGeneratedSnapshotsAreNotReady()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		player.InventoryItems = [baseItem, kinah];

		var result = ItemPurificationLiveMutationService.Apply(
			player,
			application,
			npcExpands: 0,
			questExpands: 0,
			itemExpands: 0);

		Assert.False(result.Succeeded);
		Assert.Equal(ItemPurificationLiveMutationStatus.MutationSnapshotNotReady, result.Status);
		Assert.NotNull(result.MutationPreview);
		Assert.Equal(ItemPurificationMutationSnapshotStatus.MissingCurrentInventoryItems, result.MutationPreview.Status);
		Assert.Equal([20], result.MutationPreview.MissingObjectIds);
		Assert.Null(result.AbyssPointsPlan);
		Assert.Equal([10, 30], player.InventoryItems.Select(item => item.ObjectId).Order().ToArray());
		Assert.Equal(5_000, player.AbyssRank.Ap);
		Assert.Same(result.AppliedInventoryItems, player.InventoryItems);
		Assert.Equal(3, material.Count);
	}

	[Fact]
	public void Apply_RejectsUnreadyApplicationWithoutMutation()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 0);

		var result = ItemPurificationLiveMutationService.Apply(
			player,
			application,
			npcExpands: 0,
			questExpands: 0,
			itemExpands: 0);

		Assert.False(result.Succeeded);
		Assert.Equal(ItemPurificationLiveMutationStatus.ApplicationPlanNotReady, result.Status);
		Assert.Null(result.MutationPreview);
		Assert.Null(result.AbyssPointsPlan);
		Assert.Empty(result.AppliedInventoryItems);
		Assert.Equal([10, 20, 30], player.InventoryItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(5_000, player.AbyssRank.Ap);
		Assert.Equal(3, material.Count);
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
