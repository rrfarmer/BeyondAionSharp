using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum RecipeLearnValidationPlanStatus
{
	Valid,
	BlockedRecipeListFull,
	BlockedRecipeNotFound,
	BlockedWrongRace,
	BlockedAlreadyKnown,
	BlockedMissingSkill,
	BlockedSkillPointTooLow,
}

public sealed record RecipeLearnValidationPlan(
	RecipeLearnValidationPlanStatus Status,
	int RecipeId,
	SmSystemMessage? DenialMessage,
	string? DenialText,
	bool IsValid,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class RecipeLearnValidationPlanService
{
	public const int MaxRecipeListSize = 1600;

	public static RecipeLearnValidationPlan CreatePlan(
		int recipeId,
		int currentRecipeListSize,
		bool recipeTemplateExists,
		string recipeRace,
		string playerRace,
		bool recipeAlreadyKnown,
		bool hasRequiredSkill,
		int requiredSkillPoint,
		int playerSkillLevel,
		string? requiredSkillName = null)
	{
		// Java parity: services/RecipeService.validateNewRecipe guard order.
		if (currentRecipeListSize >= MaxRecipeListSize)
		{
			return new RecipeLearnValidationPlan(
				RecipeLearnValidationPlanStatus.BlockedRecipeListFull,
				recipeId,
				DenialMessage: null,
				DenialText: "You are unable to have more than 1600 recipes at the same time.",
				IsValid: false,
				"RecipeService.validateNewRecipe -> recipeList.size() >= 1600 -> sendMessage (text, not SM_SYSTEM_MESSAGE)"
			);
		}

		if (!recipeTemplateExists)
		{
			return new RecipeLearnValidationPlan(
				RecipeLearnValidationPlanStatus.BlockedRecipeNotFound,
				recipeId,
				SmSystemMessage.RecipeItemCannotUseNoRecipe(),
				DenialText: null,
				IsValid: false,
				"RecipeService.validateNewRecipe -> template == null -> STR_RECIPEITEM_CANT_USE_NO_RECIPE"
			);
		}

		if (!string.Equals(recipeRace, "PC_ALL", StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(recipeRace, playerRace, StringComparison.OrdinalIgnoreCase))
		{
			return new RecipeLearnValidationPlan(
				RecipeLearnValidationPlanStatus.BlockedWrongRace,
				recipeId,
				SmSystemMessage.CraftRecipeRaceCheck(),
				DenialText: null,
				IsValid: false,
				"RecipeService.validateNewRecipe -> template.getRace() != PC_ALL && != player.getRace() -> STR_CRAFTRECIPE_RACE_CHECK"
			);
		}

		if (recipeAlreadyKnown)
		{
			return new RecipeLearnValidationPlan(
				RecipeLearnValidationPlanStatus.BlockedAlreadyKnown,
				recipeId,
				SmSystemMessage.CraftRecipeLearnedAlready(),
				DenialText: null,
				IsValid: false,
				"RecipeService.validateNewRecipe -> isRecipePresent(recipeId) -> STR_CRAFT_RECIPE_LEARNED_ALREADY"
			);
		}

		if (!hasRequiredSkill)
		{
			return new RecipeLearnValidationPlan(
				RecipeLearnValidationPlanStatus.BlockedMissingSkill,
				recipeId,
				SmSystemMessage.CraftRecipeCantLearnSkill(requiredSkillName ?? string.Empty),
				DenialText: null,
				IsValid: false,
				"RecipeService.validateNewRecipe -> !isSkillPresent(skillId) -> STR_CRAFT_RECIPE_CANT_LEARN_SKILL(skillName)"
			);
		}

		if (requiredSkillPoint > playerSkillLevel)
		{
			return new RecipeLearnValidationPlan(
				RecipeLearnValidationPlanStatus.BlockedSkillPointTooLow,
				recipeId,
				SmSystemMessage.CraftRecipeCantLearnSkillPoint(),
				DenialText: null,
				IsValid: false,
				$"RecipeService.validateNewRecipe -> skillpoint({requiredSkillPoint}) > playerSkillLevel({playerSkillLevel}) -> STR_CRAFT_RECIPE_CANT_LEARN_SKILLPOINT"
			);
		}

		return new RecipeLearnValidationPlan(
			RecipeLearnValidationPlanStatus.Valid,
			recipeId,
			DenialMessage: null,
			DenialText: null,
			IsValid: true,
			"RecipeService.validateNewRecipe -> all guards passed -> return template"
		);
	}
}
