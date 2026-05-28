using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationPersistencePlanServiceTests
{
	[Fact]
	public void CreatePersistencePlan_MapsMaterialBaseTargetAndAbyssRankWrites()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		var mutation = ItemPurificationLiveMutationService.Apply(
			player,
			application,
			npcExpands: 1,
			questExpands: 0,
			itemExpands: 1);

		var persistence = ItemPurificationPersistencePlanService.CreatePersistencePlan(
			application,
			mutation.MutationPreview,
			mutation.AbyssPointsPlan);

		Assert.True(persistence.Succeeded);
		Assert.Equal(ItemPurificationPersistencePlanStatus.Ready, persistence.Status);
		var materialUpdate = Assert.Single(persistence.MaterialItemUpdates);
		Assert.Equal(material.ObjectId, materialUpdate.ObjectId);
		Assert.Equal(1, materialUpdate.Count);
		Assert.Empty(persistence.DeletedMaterialItemObjectIds);
		Assert.Null(persistence.BaseItemUpdate);
		Assert.Equal(baseItem.ObjectId, persistence.DeletedBaseItemObjectId);
		Assert.Empty(persistence.UpdatedTargetItems);
		var targetAdd = Assert.Single(persistence.AddedTargetItems);
		Assert.Equal(9001, targetAdd.ObjectId);
		Assert.Equal(100000002, targetAdd.ItemId);
		Assert.Equal(20, targetAdd.Enchant);
		Assert.NotNull(persistence.AbyssRank);
		Assert.Equal(3_800, persistence.AbyssRank.Ap);
		Assert.DoesNotContain(persistence.MaterialItemUpdates, item => item.ItemId == 182400001);
		Assert.Equal(10_000, player.InventoryItems.Single(item => item.ObjectId == kinah.ObjectId).Count);
	}

	[Fact]
	public void CreatePersistencePlan_RejectsUnreadyMutationPreview()
	{
		var application = ItemPurificationApplicationPlan.Failed(ItemPurificationApplicationPlanStatus.WorkflowNotPlanned);

		var persistence = ItemPurificationPersistencePlanService.CreatePersistencePlan(
			application,
			ItemPurificationMutationSnapshotPlan.Failed(ItemPurificationMutationSnapshotStatus.ApplicationPlanNotReady),
			abyssPointsPlan: null);

		Assert.False(persistence.Succeeded);
		Assert.Equal(ItemPurificationPersistencePlanStatus.ApplicationPlanNotReady, persistence.Status);
		Assert.Empty(persistence.MaterialItemUpdates);
		Assert.Empty(persistence.AddedTargetItems);
		Assert.Null(persistence.AbyssRank);
	}

	[Fact]
	public void CreatePersistencePlan_RejectsMissingAbyssRankMutationWhenApplicationSpendsAp()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var material = new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 };
		var kinah = new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = 10_000, Location = 0 };
		var player = CreatePlayer(abyssPoints: 5_000, baseItem, material, kinah);
		var application = CreateApplicationPlan(player, baseItem, targetObjectId: 9001);
		var mutation = ItemPurificationLiveMutationService.Apply(
			player,
			application,
			npcExpands: 1,
			questExpands: 0,
			itemExpands: 1);

		var persistence = ItemPurificationPersistencePlanService.CreatePersistencePlan(
			application,
			mutation.MutationPreview,
			abyssPointsPlan: null);

		Assert.False(persistence.Succeeded);
		Assert.Equal(ItemPurificationPersistencePlanStatus.MissingAbyssRankMutation, persistence.Status);
		Assert.Empty(persistence.MaterialItemUpdates);
		Assert.Empty(persistence.DeletedMaterialItemObjectIds);
		Assert.Null(persistence.BaseItemUpdate);
		Assert.Null(persistence.DeletedBaseItemObjectId);
		Assert.Empty(persistence.AddedTargetItems);
		Assert.Null(persistence.AbyssRank);
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
