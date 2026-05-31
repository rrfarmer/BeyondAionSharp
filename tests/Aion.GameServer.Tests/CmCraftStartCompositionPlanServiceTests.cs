using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class CmCraftStartCompositionPlanServiceTests
{
	[Fact]
	public void CreatePlan_ComposesStartIntentIntoValidationConsumptionAndTaskPlans()
	{
		var service = CreateCraftService();
		var player = CreatePlayer();
		var recipe = CreateRecipe(
			recipeId: 155000101,
			dp: 100,
			productId: 100200203,
			skillId: 40001,
			skillPoint: 200,
			componentGroups: [CreateComponentGroup((152000901, 2), (152000902, 4))]);
		var productTemplate = CreateProductTemplate();
		var target = CreateCraftTarget();
		var runtimePlan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer: true,
			isPlayerSpawned: true,
			isShuttingDownSoon: false,
			unknownByte: 1,
			recipeId: recipe.RecipeId,
			targetObjectId: target.ObjectId,
			craftType: 1,
			materialsData: new Dictionary<int, long>
			{
				[152000901] = 2,
				[152000902] = 4,
			},
			targetExists: true,
			targetIsInRange: true,
			targetTemplateMatches: true);

		var plan = CmCraftStartCompositionPlanService.CreatePlan(
			runtimePlan,
			service,
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CmCraftStartCompositionPlanStatus.ReadyForDpSpendAndTaskStart, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.True(plan.RequiresDpSpend);
		Assert.Equal(100, plan.RequiredDp);
		Assert.Equal(
			[
				CmCraftStartCompositionPlanStep.UseRuntimeGuardPlan,
				CmCraftStartCompositionPlanStep.CreateStartValidationPlan,
				CmCraftStartCompositionPlanStep.CreateConsumptionPlan,
				CmCraftStartCompositionPlanStep.CreateInventoryMutationPlan,
				CmCraftStartCompositionPlanStep.CreateInventoryPersistencePlan,
				CmCraftStartCompositionPlanStep.CreateInventoryPacketPlan,
				CmCraftStartCompositionPlanStep.CreateTaskPlan,
			],
			plan.Steps);
		Assert.NotNull(plan.ValidationPlan);
		Assert.True(plan.ValidationPlan!.IsReadyForNextValidation);
		Assert.Equal(CraftStartConsumptionStatus.Planned, plan.ConsumptionPlan?.Status);
		Assert.Equal(
			[
				CraftStartConsumedItemKind.BonusItem,
				CraftStartConsumedItemKind.Component,
				CraftStartConsumedItemKind.Component,
			],
			plan.ConsumptionPlan!.Decreases.Select(item => item.Kind).ToArray());
		Assert.Equal(CraftStartInventoryMutationStatus.Planned, plan.InventoryMutationPlan?.Status);
		Assert.Equal([2003], plan.InventoryMutationPlan!.DeletedObjectIds);
		Assert.Equal([2001, 2002], plan.InventoryMutationPlan.UpdatedItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(CraftStartInventoryPersistenceStatus.Planned, plan.InventoryPersistencePlan?.Status);
		Assert.Equal([2003], plan.InventoryPersistencePlan!.DeletedObjectIds);
		Assert.Equal([2001, 2002], plan.InventoryPersistencePlan.UpdatedItems.Select(item => item.ObjectId).ToArray());
		Assert.Equal(CraftStartInventoryPacketStatus.Planned, plan.InventoryPacketPlan?.Status);
		Assert.Equal(
			[
				typeof(SmDeleteItem),
				typeof(SmCubeUpdate),
				typeof(SmInventoryUpdateItem),
				typeof(SmInventoryUpdateItem),
			],
			plan.InventoryPacketPlan!.Packets.Select(packet => packet.GetType()).ToArray());
		Assert.Equal(CraftStartTaskPlanStatus.Planned, plan.TaskPlan?.Status);
		Assert.Equal(15, plan.TaskPlan!.BonusCritModifier);
		Assert.Equal(CraftStartSideEffectBoundaryStatus.Planned, plan.SideEffectBoundaryPlan.Status);
		Assert.False(plan.SideEffectBoundaryPlan.IsLive);
		Assert.False(plan.SideEffectBoundaryPlan.ShouldDispatchLiveSideEffects);
		Assert.True(plan.SideEffectBoundaryPlan.RequiresDpSpend);
		Assert.Equal(100, plan.SideEffectBoundaryPlan.RequiredDp);
		Assert.Same(plan.InventoryMutationPlan, plan.SideEffectBoundaryPlan.InventoryMutationPlan);
		Assert.Same(plan.InventoryPacketPlan, plan.SideEffectBoundaryPlan.InventoryPacketPlan);
		Assert.Same(plan.TaskPlan, plan.SideEffectBoundaryPlan.TaskPlan);
		Assert.Equal(
			[
				CraftStartSideEffectBoundaryStep.ApplyCheckCraftInventoryMutation,
				CraftStartSideEffectBoundaryStep.SendCheckCraftInventoryPackets,
				CraftStartSideEffectBoundaryStep.SpendRecipeDp,
				CraftStartSideEffectBoundaryStep.CreateCraftingTask,
				CraftStartSideEffectBoundaryStep.StartCraftingTask,
			],
			plan.SideEffectBoundaryPlan.Steps);
		Assert.Null(plan.CancelPacketPlan);
		Assert.Null(plan.FailurePlan);
	}

	[Fact]
	public void CreatePlan_ComposesValidationFailureAndCancelOrchestration()
	{
		var service = CreateCraftService();
		var player = CreatePlayer();
		var recipe = CreateRecipe(
			recipeId: 155000102,
			dp: 0,
			productId: 100200203,
			skillId: 40001,
			skillPoint: 200,
			componentGroups: [CreateComponentGroup((152000901, 2))]);
		var productTemplate = CreateProductTemplate();
		var target = CreateCraftTarget();
		var runtimePlan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer: true,
			isPlayerSpawned: true,
			isShuttingDownSoon: false,
			unknownByte: 1,
			recipeId: recipe.RecipeId,
			targetObjectId: target.ObjectId,
			craftType: 0,
			materialsData: new Dictionary<int, long> { [152000901] = 2 },
			targetExists: true,
			targetIsInRange: true,
			targetTemplateMatches: true);

		var plan = CmCraftStartCompositionPlanService.CreatePlan(
			runtimePlan,
			service,
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: false,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CmCraftStartCompositionPlanStatus.ValidationFailed, plan.Status);
		Assert.Equal(CraftStartValidationStatus.TooFarFromTool, plan.ValidationPlan?.Status);
		Assert.Equal(CraftStartCancelPacketPlanStatus.Planned, plan.CancelPacketPlan?.Status);
		Assert.Equal(CraftStartFailureOrchestrationStatus.Planned, plan.FailurePlan?.Status);
		Assert.Null(plan.ConsumptionPlan);
		Assert.Null(plan.InventoryMutationPlan);
		Assert.Null(plan.InventoryPersistencePlan);
		Assert.Null(plan.InventoryPacketPlan);
		Assert.Null(plan.TaskPlan);
		Assert.False(plan.RequiresDpSpend);
		Assert.Contains(CmCraftStartCompositionPlanStep.CreateFailureOrchestrationPlan, plan.Steps);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.Equal(CraftStartSideEffectBoundaryStatus.ValidationFailed, plan.SideEffectBoundaryPlan.Status);
		Assert.Empty(plan.SideEffectBoundaryPlan.Steps);
		Assert.False(plan.SideEffectBoundaryPlan.ShouldDispatchLiveSideEffects);
	}

	[Fact]
	public void CreatePlan_OmitsDpBoundaryStepWhenRecipeHasNoDpCost()
	{
		var service = CreateCraftService();
		var player = CreatePlayer();
		var recipe = CreateRecipe(
			recipeId: 155000102,
			dp: 0,
			productId: 100200203,
			skillId: 40001,
			skillPoint: 200,
			componentGroups: [CreateComponentGroup((152000901, 2))]);
		var productTemplate = CreateProductTemplate();
		var target = CreateCraftTarget();
		var runtimePlan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer: true,
			isPlayerSpawned: true,
			isShuttingDownSoon: false,
			unknownByte: 1,
			recipeId: recipe.RecipeId,
			targetObjectId: target.ObjectId,
			craftType: 0,
			materialsData: new Dictionary<int, long> { [152000901] = 2 },
			targetExists: true,
			targetIsInRange: true,
			targetTemplateMatches: true);

		var plan = CmCraftStartCompositionPlanService.CreatePlan(
			runtimePlan,
			service,
			player,
			recipe,
			productTemplate,
			target,
			targetIsStaticObject: true,
			targetIsWithinToolRange: true,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CmCraftStartCompositionPlanStatus.ReadyForDpSpendAndTaskStart, plan.Status);
		Assert.False(plan.RequiresDpSpend);
		Assert.Equal(0, plan.RequiredDp);
		Assert.Equal(CraftStartSideEffectBoundaryStatus.Planned, plan.SideEffectBoundaryPlan.Status);
		Assert.Equal(
			[
				CraftStartSideEffectBoundaryStep.ApplyCheckCraftInventoryMutation,
				CraftStartSideEffectBoundaryStep.SendCheckCraftInventoryPackets,
				CraftStartSideEffectBoundaryStep.CreateCraftingTask,
				CraftStartSideEffectBoundaryStep.StartCraftingTask,
			],
			plan.SideEffectBoundaryPlan.Steps);
		Assert.DoesNotContain(CraftStartSideEffectBoundaryStep.SpendRecipeDp, plan.SideEffectBoundaryPlan.Steps);
	}

	[Fact]
	public void CreatePlan_StopsWhenRuntimeGuardDidNotReachStartCrafting()
	{
		var service = CreateCraftService();
		var runtimePlan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer: false,
			isPlayerSpawned: false,
			isShuttingDownSoon: false,
			unknownByte: 1,
			recipeId: 155000103,
			targetObjectId: 9001,
			craftType: 0,
			materialsData: null,
			targetExists: true,
			targetIsInRange: true,
			targetTemplateMatches: true);

		var plan = CmCraftStartCompositionPlanService.CreatePlan(
			runtimePlan,
			service,
			player: null,
			recipeTemplate: null,
			productTemplate: null,
			target: null,
			targetIsStaticObject: false,
			targetIsWithinToolRange: false,
			hasCraftingTaskInProgress: false);

		Assert.Equal(CmCraftStartCompositionPlanStatus.RuntimeBlocked, plan.Status);
		Assert.Same(runtimePlan, plan.RuntimePlan);
		Assert.Null(plan.ValidationPlan);
		Assert.Null(plan.ConsumptionPlan);
		Assert.Null(plan.InventoryMutationPlan);
		Assert.Null(plan.InventoryPersistencePlan);
		Assert.Null(plan.InventoryPacketPlan);
		Assert.Null(plan.TaskPlan);
		Assert.Equal([CmCraftStartCompositionPlanStep.UseRuntimeGuardPlan], plan.Steps);
		Assert.Equal(CraftStartSideEffectBoundaryStatus.NotPlanned, plan.SideEffectBoundaryPlan.Status);
		Assert.Empty(plan.SideEffectBoundaryPlan.Steps);
	}

	private static CraftService CreateCraftService()
	{
		var itemTemplates = new ItemTemplateTable(
		[
			CreateProductTemplate(),
			new ItemTemplateSummary(152000901, "Material A", 730901, 0, 1, "MATERIAL", "ITEM", "COMMON", "PC_ALL", 10, 1, 0),
			new ItemTemplateSummary(152000902, "Material B", 730902, 0, 1, "MATERIAL", "ITEM", "COMMON", "PC_ALL", 10, 1, 0),
			new ItemTemplateSummary(169401081, "Craft Bonus", 731081, 0, 1, "MATERIAL", "ITEM", "COMMON", "PC_ALL", 10, 1, 0),
		]);
		return new CraftService(resourceStats: null!, itemTemplates);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 1145,
			Name = "CraftAdapter",
			Race = "ELYOS",
			PlayerClass = "RANGER",
			Level = 30,
			Dp = 500,
			IsOnline = true,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
			Recipes = [155000101, 155000102],
			Skills = [new PlayerSkill { SkillId = 40001, SkillLevel = 250 }],
			InventoryItems =
			[
				new InventoryItem { ObjectId = 2001, ItemId = 152000901, Count = 10, Location = 0 },
				new InventoryItem { ObjectId = 2002, ItemId = 152000902, Count = 10, Location = 0 },
				new InventoryItem { ObjectId = 2003, ItemId = 169401081, Count = 1, Location = 0 },
			],
			LifeStats = new PlayerLifeStats(100, 100, 100),
		};
	}

	private static RecipeTemplateSummary CreateRecipe(
		int recipeId,
		int dp,
		int productId,
		int skillId,
		int skillPoint,
		IReadOnlyList<RecipeComponentDataSummary> componentGroups)
	{
		return new RecipeTemplateSummary(
			recipeId,
			0,
			skillId,
			"PC_ALL",
			skillPoint,
			dp,
			0,
			productId,
			1,
			ComboProducts: null,
			CraftDelayId: null,
			CraftDelayTime: null,
			componentGroups);
	}

	private static ItemTemplateSummary CreateProductTemplate()
	{
		return new ItemTemplateSummary(
			100200203,
			"Practice Sword",
			0,
			0,
			1,
			"SWORD",
			"ITEM",
			"COMMON",
			"PC_ALL",
			1,
			1,
			1);
	}

	private static RecipeComponentDataSummary CreateComponentGroup(params (int ItemId, long Quantity)[] components)
	{
		return new RecipeComponentDataSummary(
			components
				.Select(component => new RecipeComponentSummary(component.ItemId, component.Quantity))
				.ToArray());
	}

	private static WorldNpc CreateCraftTarget()
	{
		var template = new NpcTemplateSummary(
			730190,
			"Crafting Station",
			NameId: 730190,
			Level: 1,
			Rank: "NORMAL",
			Rating: "NORMAL",
			Race: "NONE",
			Tribe: "NONE",
			Type: "STATIC");
		return new WorldNpc(9001, 730190, template, new WorldPosition(210010000, 10, 20, 30, 0));
	}
}
