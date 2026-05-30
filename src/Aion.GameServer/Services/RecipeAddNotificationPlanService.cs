using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum RecipeAddNotificationPlanStatus
{
	PlanCreated,
}

public sealed record RecipeAddNotificationPlan(
	RecipeAddNotificationPlanStatus Status,
	int RecipeId,
	string PlayerName,
	SmSystemMessage LearnNotification,
	bool ShouldSendToPlayer,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class RecipeAddNotificationPlanService
{
	public static RecipeAddNotificationPlan CreatePlan(int recipeId, string playerName)
	{
		// Java parity: services/RecipeService.addRecipe sends STR_CRAFT_RECIPE_LEARN(recipeId, player.getName())
		// on successful recipe addition. Live player.getRecipeList().addRecipe is deferred.
		return new RecipeAddNotificationPlan(
			RecipeAddNotificationPlanStatus.PlanCreated,
			recipeId,
			playerName,
			SmSystemMessage.CraftRecipeLearn(recipeId, playerName),
			ShouldSendToPlayer: true,
			$"RecipeService.addRecipe -> addRecipe success -> STR_CRAFT_RECIPE_LEARN({recipeId}, {playerName})"
		);
	}
}
