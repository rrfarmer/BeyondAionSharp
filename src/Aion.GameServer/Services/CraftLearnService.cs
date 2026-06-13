using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class CraftLearnService
{
	public static CraftLearnValidation ValidateNewRecipe(Player player, int recipeId, StaticData staticData)
	{
		// Java parity: services/RecipeService.validateNewRecipe.
		if (player.Recipes.Count >= 1600)
			return CraftLearnValidation.Fail(CraftLearnFailure.RecipeListFull);

		var recipeTemplate = staticData.RecipeTemplates.GetRecipeTemplateById(recipeId);
		if (recipeTemplate == null)
			return CraftLearnValidation.Fail(CraftLearnFailure.MissingRecipe);

		if (recipeTemplate.Race != "PC_ALL" && recipeTemplate.Race != player.Race.ToString())
			return CraftLearnValidation.Fail(CraftLearnFailure.InvalidRace);

		if (player.Recipes.Contains(recipeId))
			return CraftLearnValidation.Fail(CraftLearnFailure.AlreadyKnown);

		var playerSkill = player.Skills.FirstOrDefault(skill => skill.SkillId == recipeTemplate.SkillId);
		if (playerSkill == null)
		{
			var skillName = staticData.SkillTemplates.GetSkillTemplate(recipeTemplate.SkillId)?.GetClientName() ?? string.Empty;
			return CraftLearnValidation.Fail(CraftLearnFailure.MissingSkill, skillName);
		}

		if (recipeTemplate.SkillPoint > playerSkill.SkillLevel)
			return CraftLearnValidation.Fail(CraftLearnFailure.SkillPointTooLow);

		return new CraftLearnValidation(CraftLearnFailure.None, recipeTemplate);
	}
}

public sealed record CraftLearnValidation(
	CraftLearnFailure Failure,
	RecipeTemplateSummary? RecipeTemplate = null,
	string SkillName = "")
{
	public bool Succeeded => Failure == CraftLearnFailure.None && RecipeTemplate != null;

	public static CraftLearnValidation Fail(CraftLearnFailure failure, string skillName = "")
	{
		return new CraftLearnValidation(failure, SkillName: skillName);
	}
}

public enum CraftLearnFailure
{
	None,
	RecipeListFull,
	MissingRecipe,
	InvalidRace,
	AlreadyKnown,
	MissingSkill,
	SkillPointTooLow,
}
