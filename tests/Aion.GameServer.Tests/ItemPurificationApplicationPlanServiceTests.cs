using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ItemPurificationApplicationPlanServiceTests
{
	[Fact]
	public void CreateApplicationPlan_OrdersMaterialApKinahBaseAndTargetOperationsLikeJava()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var workflow = CreateWorkflow(player, baseItem, targetObjectId: 9001);

		var plan = ItemPurificationApplicationPlanService.CreateApplicationPlan(workflow);

		Assert.True(plan.Succeeded);
		Assert.False(plan.RequiresTargetObjectIdAllocation);
		Assert.False(plan.RequiresRandomBonusSelection);
		Assert.Equal(1_200, plan.AbyssPointsToSpend);
		Assert.Equal(1_000, plan.NecessaryKinah);
		Assert.False(plan.KinahMutationApplied);
		Assert.Equal(
			[
				ItemPurificationApplicationOperationType.DeleteMaterialItem,
				ItemPurificationApplicationOperationType.SpendAbyssPoints,
				ItemPurificationApplicationOperationType.PreserveKinahNoOp,
				ItemPurificationApplicationOperationType.DeleteBaseItem,
				ItemPurificationApplicationOperationType.AddTargetItem,
			],
			plan.Operations.Select(operation => operation.Type).ToArray());
		Assert.Equal(20, plan.Operations[0].ObjectId);
		Assert.Equal(1_200, plan.Operations[1].Count);
		Assert.Equal(182400001, plan.Operations[2].ItemId);
		Assert.Equal(10, plan.Operations[3].ObjectId);
		Assert.Equal(9001, plan.Operations[4].ObjectId);
		Assert.True(plan.Operations[4].Effects.HasFlag(ItemPurificationApplicationEffect.Persistence));
		Assert.True(plan.Operations[4].Effects.HasFlag(ItemPurificationApplicationEffect.Packet));
		Assert.True(plan.Operations[4].Effects.HasFlag(ItemPurificationApplicationEffect.QuestNotification));
	}

	[Fact]
	public void CreateApplicationPlan_PreservesMaterialUpdateBeforeApAndBaseDelete()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 3, Location = 0 });
		var workflow = CreateWorkflow(player, baseItem, targetObjectId: 9001);

		var plan = ItemPurificationApplicationPlanService.CreateApplicationPlan(workflow);

		Assert.True(plan.Succeeded);
		Assert.Equal(ItemPurificationApplicationOperationType.UpdateMaterialItemCount, plan.Operations[0].Type);
		Assert.Equal(20, plan.Operations[0].ObjectId);
		Assert.Equal(2, plan.Operations[0].Count);
		Assert.Equal(1, plan.Operations[0].NewCount);
		Assert.Equal(ItemPurificationApplicationOperationType.SpendAbyssPoints, plan.Operations[1].Type);
		Assert.Equal(ItemPurificationApplicationOperationType.DeleteBaseItem, plan.Operations[3].Type);
	}

	[Fact]
	public void CreateApplicationPlan_FlagsPlaceholderTargetObjectId()
	{
		var baseItem = CreateBaseItem(enchant: 25);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var workflow = CreateWorkflow(player, baseItem, targetObjectId: 0);

		var plan = ItemPurificationApplicationPlanService.CreateApplicationPlan(workflow);

		Assert.Equal(ItemPurificationApplicationPlanStatus.NeedsTargetObjectIdAllocation, plan.Status);
		Assert.True(plan.RequiresTargetObjectIdAllocation);
		Assert.False(plan.Succeeded);
		var targetAdd = Assert.Single(plan.Operations, operation => operation.Type == ItemPurificationApplicationOperationType.AddTargetItem);
		Assert.Equal(0, targetAdd.ObjectId);
	}

	[Fact]
	public void CreateApplicationPlan_FlagsRandomBonusSelectionWhenRerollWasNotInjected()
	{
		var baseItem = CreateBaseItem(enchant: 25, randomBonus: 7);
		var player = CreatePlayer(
			abyssPoints: 5_000,
			kinah: 10_000,
			baseItem,
			new InventoryItem { ObjectId = 20, ItemId = 186000001, Count = 2, Location = 0 });
		var workflow = CreateWorkflow(
			player,
			baseItem,
			targetObjectId: 9001,
			sourceStatBonusSetId: 1,
			targetStatBonusSetId: 2);

		var plan = ItemPurificationApplicationPlanService.CreateApplicationPlan(workflow);

		Assert.Equal(ItemPurificationApplicationPlanStatus.NeedsRandomBonusSelection, plan.Status);
		Assert.True(plan.RequiresRandomBonusSelection);
		Assert.Equal(0, plan.TargetItem?.RandomBonus);
	}

	[Fact]
	public void CreateApplicationPlan_RejectsMissingOrUnplannedWorkflow()
	{
		var missing = ItemPurificationApplicationPlanService.CreateApplicationPlan(null);
		var failed = ItemPurificationApplicationPlanService.CreateApplicationPlan(
			ItemPurificationWorkflowPlan.Failed(ItemPurificationWorkflowStatus.MissingBaseItem));

		Assert.Equal(ItemPurificationApplicationPlanStatus.MissingWorkflow, missing.Status);
		Assert.Empty(missing.Operations);
		Assert.Equal(ItemPurificationApplicationPlanStatus.WorkflowNotPlanned, failed.Status);
		Assert.Empty(failed.Operations);
	}

	private static ItemPurificationWorkflowPlan CreateWorkflow(
		Player player,
		InventoryItem baseItem,
		int targetObjectId,
		int sourceStatBonusSetId = 0,
		int targetStatBonusSetId = 0)
	{
		return ItemPurificationWorkflowService.CreateWorkflowPlan(
			player,
			baseItem,
			CreatePurificationTable(),
			CreateItemTemplates(sourceStatBonusSetId, targetStatBonusSetId),
			resultItemId: 100000002,
			targetObjectId);
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

	private static InventoryItem CreateBaseItem(int enchant, int randomBonus = 0)
	{
		return new InventoryItem
		{
			ObjectId = 10,
			ItemId = 100000001,
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

	private static ItemTemplateTable CreateItemTemplates(int sourceStatBonusSetId, int targetStatBonusSetId)
	{
		return new ItemTemplateTable(
		[
			CreateTemplate(100000001, sourceStatBonusSetId, maxTuneCount: 5, maxEnchantLevel: 15),
			CreateTemplate(100000002, targetStatBonusSetId, maxTuneCount: 1, maxEnchantLevel: 20),
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
