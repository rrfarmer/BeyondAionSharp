using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class CraftService
{
	private readonly WorldNpcResourceStatsService _resourceStats;

	public CraftService(WorldNpcResourceStatsService resourceStats)
	{
		_resourceStats = resourceStats;
	}

	public async ValueTask<CraftStartDpCostResult> SpendRecipeDpForCraftStartAsync(
		Player? player,
		RecipeTemplateSummary? recipeTemplate,
		int? maxDp = null)
	{
		// Java parity: services/craft/CraftService.checkCraft + startCrafting recipe DP branch.
		if (player == null)
			return CraftStartDpCostResult.MissingPlayer(recipeTemplate?.RecipeId ?? 0);
		if (recipeTemplate == null)
			return CraftStartDpCostResult.MissingRecipe(player.ObjectId, player.Dp);

		var requiredDp = recipeTemplate.Dp;
		if (player.Dp < requiredDp)
			return CraftStartDpCostResult.NotEnoughDp(player.ObjectId, recipeTemplate.RecipeId, requiredDp, player.Dp);

		var previousDp = player.Dp;
		var change = await _resourceStats.AddPlayerDpAsync(player, -requiredDp, maxDp);
		return CraftStartDpCostResult.FromDpChange(change, recipeTemplate.RecipeId, requiredDp, previousDp);
	}
}

public sealed record CraftStartDpCostResult(
	CraftStartDpCostStatus Status,
	int ObjectId,
	int RecipeId,
	int RequiredDp,
	int PreviousDp,
	int CurrentDp,
	WorldNpcResourceChangeResult? Change = null)
{
	public static CraftStartDpCostResult MissingPlayer(int recipeId)
	{
		return new CraftStartDpCostResult(
			CraftStartDpCostStatus.MissingPlayer,
			0,
			recipeId,
			0,
			0,
			0);
	}

	public static CraftStartDpCostResult MissingRecipe(int objectId, int currentDp)
	{
		return new CraftStartDpCostResult(
			CraftStartDpCostStatus.MissingRecipe,
			objectId,
			0,
			0,
			currentDp,
			currentDp);
	}

	public static CraftStartDpCostResult NotEnoughDp(int objectId, int recipeId, int requiredDp, int currentDp)
	{
		return new CraftStartDpCostResult(
			CraftStartDpCostStatus.NotEnoughDp,
			objectId,
			recipeId,
			requiredDp,
			currentDp,
			currentDp);
	}

	public static CraftStartDpCostResult FromDpChange(
		WorldNpcResourceChangeResult change,
		int recipeId,
		int requiredDp,
		int previousDp)
	{
		var status = change.Status is WorldNpcResourceChangeStatus.StartingClass
			or WorldNpcResourceChangeStatus.MissingTarget
			or WorldNpcResourceChangeStatus.MissingMaxResource
			? CraftStartDpCostStatus.DpBoundarySkipped
			: CraftStartDpCostStatus.Applied;
		return new CraftStartDpCostResult(
			status,
			change.ObjectId,
			recipeId,
			requiredDp,
			previousDp,
			change.CurrentValue,
			change);
	}
}

public enum CraftStartDpCostStatus
{
	Applied,
	MissingPlayer,
	MissingRecipe,
	NotEnoughDp,
	DpBoundarySkipped,
}
