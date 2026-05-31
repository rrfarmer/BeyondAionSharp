using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CmCraftRuntimePlanServiceTests
{
	[Fact]
	public void CreatePlan_ReturnsNoPlayerOrNotSpawnedWhenPlayerMissingOrUnspawned()
	{
		var missingPlayerPlan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer: false,
			isPlayerSpawned: false,
			isShuttingDownSoon: false,
			unknownByte: 1,
			recipeId: 155000001,
			targetObjectId: 9001,
			craftType: 0,
			materialsData: null,
			targetExists: true,
			targetIsInRange: true,
			targetTemplateMatches: true);
		var unspawnedPlan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer: true,
			isPlayerSpawned: false,
			isShuttingDownSoon: false,
			unknownByte: 1,
			recipeId: 155000001,
			targetObjectId: 9001,
			craftType: 0,
			materialsData: null,
			targetExists: true,
			targetIsInRange: true,
			targetTemplateMatches: true);

		Assert.Equal(CmCraftRuntimePlanStatus.NoPlayerOrNotSpawned, missingPlayerPlan.Status);
		Assert.Equal(CmCraftRuntimePlanStatus.NoPlayerOrNotSpawned, unspawnedPlan.Status);
		Assert.Null(missingPlayerPlan.StartIntent);
		Assert.Null(unspawnedPlan.StartIntent);
	}

	[Fact]
	public void CreatePlan_ReturnsShuttingDownSoonBeforeTargetValidation()
	{
		var plan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer: true,
			isPlayerSpawned: true,
			isShuttingDownSoon: true,
			unknownByte: 1,
			recipeId: 155000001,
			targetObjectId: 9001,
			craftType: 0,
			materialsData: new Dictionary<int, long> { [186000040] = 1 },
			targetExists: false,
			targetIsInRange: false,
			targetTemplateMatches: false);

		Assert.Equal(CmCraftRuntimePlanStatus.ShuttingDownSoon, plan.Status);
		Assert.Null(plan.StartIntent);
		Assert.Contains("ShuttingDownSoon", plan.JavaSource, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(false, true, true)]
	[InlineData(true, false, true)]
	[InlineData(true, true, false)]
	public void CreatePlan_ReturnsInvalidNonMorphTargetForJavaStaticObjectGuardFailures(bool targetExists, bool targetIsInRange, bool targetTemplateMatches)
	{
		var plan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer: true,
			isPlayerSpawned: true,
			isShuttingDownSoon: false,
			unknownByte: 1,
			recipeId: 155000001,
			targetObjectId: 9001,
			craftType: 0,
			materialsData: new Dictionary<int, long> { [186000040] = 2 },
			targetExists: targetExists,
			targetIsInRange: targetIsInRange,
			targetTemplateMatches: targetTemplateMatches);

		Assert.Equal(CmCraftRuntimePlanStatus.InvalidNonMorphTarget, plan.Status);
		Assert.Null(plan.StartIntent);
		Assert.True(plan.RequiresStaticTargetValidation);
	}

	[Fact]
	public void CreatePlan_AllowsMorphMarkerToBypassStaticTargetChecks()
	{
		var plan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer: true,
			isPlayerSpawned: true,
			isShuttingDownSoon: false,
			unknownByte: CmCraftRuntimePlanService.MorphSubstancesMarker,
			recipeId: 155000078,
			targetObjectId: 0,
			craftType: 1,
			materialsData: new Dictionary<int, long>
			{
				[186000040] = 3,
				[186000041] = 7,
			},
			targetExists: false,
			targetIsInRange: false,
			targetTemplateMatches: false);

		Assert.Equal(CmCraftRuntimePlanStatus.StartCrafting, plan.Status);
		Assert.NotNull(plan.StartIntent);
		Assert.False(plan.RequiresStaticTargetValidation);
		Assert.True(plan.StartIntent!.UsesMorphTargetBypass);
		Assert.Equal(155000078, plan.StartIntent.RecipeId);
		Assert.Equal(1, plan.StartIntent.CraftType);
		Assert.Equal(2, plan.StartIntent.MaterialsData.Count);
		Assert.Contains("morph", plan.JavaSource, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void CreatePlan_StartCraftingPreservesRecipeTargetCraftTypeAndMaterials()
	{
		var materials = new Dictionary<int, long>
		{
			[186000040] = 2,
			[186000041] = 4,
		};
		var plan = CmCraftRuntimePlanService.CreatePlan(
			hasPlayer: true,
			isPlayerSpawned: true,
			isShuttingDownSoon: false,
			unknownByte: 1,
			recipeId: 155000001,
			targetObjectId: 9001,
			craftType: 0,
			materialsData: materials,
			targetExists: true,
			targetIsInRange: true,
			targetTemplateMatches: true);

		Assert.Equal(CmCraftRuntimePlanStatus.StartCrafting, plan.Status);
		Assert.NotNull(plan.StartIntent);
		Assert.True(plan.RequiresStaticTargetValidation);
		Assert.False(plan.StartIntent!.UsesMorphTargetBypass);
		Assert.Equal(155000001, plan.StartIntent.RecipeId);
		Assert.Equal(9001, plan.StartIntent.TargetObjectId);
		Assert.Equal(0, plan.StartIntent.CraftType);
		Assert.Equal(materials, plan.StartIntent.MaterialsData);
	}
}
