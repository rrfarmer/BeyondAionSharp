using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class RecipeTemplateTable
{
	private readonly IReadOnlyDictionary<int, RecipeTemplateSummary> _templatesById;
	private readonly IReadOnlyList<RecipeTemplateSummary> _autoLearnRecipes;

	public RecipeTemplateTable(IReadOnlyList<RecipeTemplateSummary> templates)
	{
		Templates = templates;
		_templatesById = new ReadOnlyDictionary<int, RecipeTemplateSummary>(
			templates.ToDictionary(template => template.RecipeId));
		_autoLearnRecipes = templates.Where(template => template.AutoLearn != 0).ToArray();
	}

	public IReadOnlyList<RecipeTemplateSummary> Templates { get; }

	public int Count => Templates.Count;

	public RecipeTemplateSummary? GetRecipeTemplateById(int recipeId)
	{
		return _templatesById.GetValueOrDefault(recipeId);
	}

	public IReadOnlyList<RecipeTemplateSummary> GetAutolearnRecipes(string race, int skillId, int maxLevel)
	{
		return _autoLearnRecipes
			.Where(template =>
				template.SkillId == skillId
				&& template.SkillPoint <= maxLevel
				&& (template.Race == "PC_ALL" || template.Race == race))
			.ToArray();
	}
}

public sealed record RecipeTemplateSummary(
	int RecipeId,
	int NameId,
	int SkillId,
	string Race,
	int SkillPoint,
	int Dp,
	int AutoLearn,
	int ProductId,
	int Quantity,
	IReadOnlyList<int>? ComboProducts = null,
	int? CraftDelayId = null,
	int? CraftDelayTime = null,
	IReadOnlyList<RecipeComponentDataSummary>? ComponentsData = null,
	int? MaxProductionCount = null)
{
	public IReadOnlyList<RecipeComponentDataSummary> ComponentGroups => ComponentsData ?? Array.Empty<RecipeComponentDataSummary>();

	public int? GetComboProduct(int count)
	{
		var comboProducts = ComboProducts;
		if (count <= 0 || comboProducts == null || count > comboProducts.Count)
			return null;

		return comboProducts[count - 1];
	}
}

public sealed record RecipeComponentDataSummary(IReadOnlyList<RecipeComponentSummary> Components)
{
	public RecipeComponentSummary? FirstComponent => Components.Count == 0 ? null : Components[0];
}

public sealed record RecipeComponentSummary(int ItemId, long Quantity);
