using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationWorkflowServiceTests
{
	[Fact]
	public void CreateWorkflowPlan_ComposesValidationMaterialMutationAndInheritanceWithoutLiveApMutation()
	{
		var baseItem = CreateBaseItem(enchant: 25, randomBonus: 7);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });

		var plan = ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			baseItem,
			CreatePurificationTable(),
			CreateItemTemplates(sourceStatBonusSetId: 1, targetStatBonusSetId: 1),
			resultItemId: 100000002,
			targetObjectId: 9001);

		Assert.True(plan.Succeeded);
		Assert.NotNull(plan.Validation);
		Assert.Equal(ItemPurificationApStatus.Allowed, plan.Validation.Status);
		Assert.NotNull(plan.MaterialMutation);
		Assert.Equal(1_200, plan.MaterialMutation.AbyssPointsToSpend);
		Assert.Equal([20, 10], plan.MaterialMutation.DeletedObjectIds);
		Assert.NotNull(plan.Inheritance);
		Assert.True(plan.Inheritance.Succeeded);
		Assert.Equal(9001, plan.Inheritance.TargetItem?.ObjectId);
		Assert.Equal(100000002, plan.Inheritance.TargetItem?.ItemId);
		Assert.Equal(20, plan.Inheritance.TargetItem?.Enchant);
		Assert.Equal(7, plan.Inheritance.TargetItem?.RandomBonus);
		Assert.Equal(5_000, player.AbyssRank.Ap);
	}

	[Fact]
	public void CreateWorkflowPlan_StopsBeforeMaterialMutationWhenValidationFails()
	{
		var baseItem = CreateBaseItem(enchant: 9);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });

		var plan = ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			baseItem,
			CreatePurificationTable(),
			CreateItemTemplates(),
			resultItemId: 100000002,
			targetObjectId: 9001);

		Assert.Equal(ItemPurificationWorkflowStatus.ValidationFailed, plan.Status);
		Assert.Equal(ItemPurificationApStatus.EnchantTooLow, plan.Validation?.Status);
		Assert.Null(plan.MaterialMutation);
		Assert.Null(plan.Inheritance);
	}

	[Fact]
	public void CreateWorkflowPlan_StopsBeforeMaterialMutationWhenValidationFindsMissingMaterials()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 1, Location = 0 });

		var plan = ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			baseItem,
			CreatePurificationTable(),
			CreateItemTemplates(),
			resultItemId: 100000002,
			targetObjectId: 9001);

		Assert.Equal(ItemPurificationWorkflowStatus.ValidationFailed, plan.Status);
		Assert.Equal(ItemPurificationApStatus.MissingRequiredMaterial, plan.Validation?.Status);
		Assert.Null(plan.MaterialMutation);
		Assert.Null(plan.Inheritance);
	}

	[Fact]
	public void CreateWorkflowPlan_ReportsLookupAndTemplateFailures()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var table = CreatePurificationTable();

		var missingTemplate = ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			CreateBaseItem(itemId: 999, enchant: 25),
			table,
			CreateItemTemplates(),
			resultItemId: 100000002,
			targetObjectId: 9001);
		var invalidResult = ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			baseItem,
			table,
			CreateItemTemplates(),
			resultItemId: 999,
			targetObjectId: 9001);
		var missingTargetTemplate = ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			baseItem,
			table,
			CreateItemTemplates(includeTargetTemplate: false),
			resultItemId: 100000002,
			targetObjectId: 9001);

		Assert.Equal(ItemPurificationWorkflowStatus.MissingTemplate, missingTemplate.Status);
		Assert.Equal(ItemPurificationWorkflowStatus.InvalidResultItem, invalidResult.Status);
		Assert.Equal(ItemPurificationWorkflowStatus.TargetInheritanceFailed, missingTargetTemplate.Status);
		Assert.Equal(ItemPurificationInheritanceStatus.MissingTargetTemplate, missingTargetTemplate.Inheritance?.Status);
	}

	private static Player CreatePlayer(int abyssPoints, long kinah, params InventoryItem[] items)
	{
		return new Player
		{
			ObjectId = 700,
			AbyssRank = PlayerAbyssRank.Default() with { Ap = abyssPoints },
			InventoryItems =
			[
				new InventoryItem { ObjectId = 30, ItemId = 182400001, Count = kinah, Location = 0 },
				.. items,
			],
		};
	}

	private static InventoryItem CreateBaseItem(int enchant, int randomBonus = 0, int itemId = 100000001)
	{
		return new InventoryItem
		{
			ObjectId = 10,
			ItemId = itemId,
			Count = 1,
			Location = 0,
			Enchant = enchant,
			RandomBonus = randomBonus,
			TuneCount = 2,
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

	private static ItemTemplateTable CreateItemTemplates(
		bool includeTargetTemplate = true,
		int sourceStatBonusSetId = 0,
		int targetStatBonusSetId = 0)
	{
		var templates = new List<ItemTemplateSummary>
		{
			CreateTemplate(100000001, sourceStatBonusSetId, maxTuneCount: 5, maxEnchantLevel: 15),
		};
		if (includeTargetTemplate)
			templates.Add(CreateTemplate(100000002, targetStatBonusSetId, maxTuneCount: 1, maxEnchantLevel: 20));

		return new ItemTemplateTable(templates);
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
