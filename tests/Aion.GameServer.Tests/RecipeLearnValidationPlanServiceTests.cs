using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class RecipeLearnValidationPlanServiceTests
{
	private static RecipeLearnValidationPlan Valid(int skillPoint = 10, int playerSkill = 10)
	{
		return RecipeLearnValidationPlanService.CreatePlan(
			recipeId: 5001, currentRecipeListSize: 0,
			recipeTemplateExists: true, recipeRace: "PC_ALL", playerRace: "ELYOS",
			recipeAlreadyKnown: false, hasRequiredSkill: true,
			requiredSkillPoint: skillPoint, playerSkillLevel: playerSkill,
			requiredSkillName: "Cooking");
	}

	[Fact]
	public void CreatePlan_AllGuardsPassIsValid()
	{
		var plan = Valid();

		Assert.Equal(RecipeLearnValidationPlanStatus.Valid, plan.Status);
		Assert.True(plan.IsValid);
		Assert.Null(plan.DenialMessage);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_RecipeListFullBlocksWithTextMessage()
	{
		var plan = RecipeLearnValidationPlanService.CreatePlan(
			5001, currentRecipeListSize: 1600, recipeTemplateExists: true,
			recipeRace: "PC_ALL", playerRace: "ELYOS", recipeAlreadyKnown: false,
			hasRequiredSkill: true, requiredSkillPoint: 1, playerSkillLevel: 10);

		Assert.Equal(RecipeLearnValidationPlanStatus.BlockedRecipeListFull, plan.Status);
		Assert.Null(plan.DenialMessage); // text message, not SM_SYSTEM_MESSAGE
		Assert.NotNull(plan.DenialText);
		Assert.Contains("1600", plan.DenialText);
	}

	[Fact]
	public void CreatePlan_RecipeNotFoundBlocksWithSystemMessage()
	{
		var plan = RecipeLearnValidationPlanService.CreatePlan(
			5001, 0, recipeTemplateExists: false, "PC_ALL", "ELYOS",
			false, true, 1, 10);

		Assert.Equal(RecipeLearnValidationPlanStatus.BlockedRecipeNotFound, plan.Status);
		Assert.NotNull(plan.DenialMessage);
		// RecipeItemCannotUseNoRecipe
		Assert.False(plan.IsValid);
	}

	[Fact]
	public void CreatePlan_WrongRaceBlocksWithMessage()
	{
		var plan = RecipeLearnValidationPlanService.CreatePlan(
			5001, 0, true, "ASMODIANS", "ELYOS",
			false, true, 1, 10);

		Assert.Equal(RecipeLearnValidationPlanStatus.BlockedWrongRace, plan.Status);
		Assert.NotNull(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_PcAllRaceAllowsAnyPlayerRace()
	{
		var plan = Valid();

		Assert.Equal(RecipeLearnValidationPlanStatus.Valid, plan.Status);
	}

	[Fact]
	public void CreatePlan_AlreadyKnownBlocksWithMessage()
	{
		var plan = RecipeLearnValidationPlanService.CreatePlan(
			5001, 0, true, "PC_ALL", "ELYOS",
			recipeAlreadyKnown: true, true, 1, 10);

		Assert.Equal(RecipeLearnValidationPlanStatus.BlockedAlreadyKnown, plan.Status);
		Assert.NotNull(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_MissingSkillBlocksWithMessage()
	{
		var plan = RecipeLearnValidationPlanService.CreatePlan(
			5001, 0, true, "PC_ALL", "ELYOS",
			false, hasRequiredSkill: false, 1, 0, "Cooking");

		Assert.Equal(RecipeLearnValidationPlanStatus.BlockedMissingSkill, plan.Status);
		Assert.NotNull(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_SkillPointTooLowBlocksWithMessage()
	{
		var plan = Valid(skillPoint: 100, playerSkill: 50);

		Assert.Equal(RecipeLearnValidationPlanStatus.BlockedSkillPointTooLow, plan.Status);
		Assert.NotNull(plan.DenialMessage);
	}

	[Fact]
	public void CreatePlan_ListFullBlocksBeforeTemplateCheck()
	{
		// Java: recipeList.size() >= 1600 is first guard
		var plan = RecipeLearnValidationPlanService.CreatePlan(
			5001, 1600, false, "PC_ALL", "ELYOS",
			false, false, 1, 0);

		Assert.Equal(RecipeLearnValidationPlanStatus.BlockedRecipeListFull, plan.Status);
	}

	[Fact]
	public void MaxRecipeListSizeConstantIs1600()
	{
		Assert.Equal(1600, RecipeLearnValidationPlanService.MaxRecipeListSize);
	}
}
