using System.Xml.Linq;

namespace Aion.GameServer.Dataholders;

public sealed class WorkOrderRecipeTable
{
	private readonly IReadOnlyDictionary<int, int> _recipeIdsByQuestId;

	public WorkOrderRecipeTable(IEnumerable<WorkOrderRecipeSummary> workOrders)
	{
		// Java parity: dataholders/XMLQuests.afterUnmarshal indexes XMLQuest handlers by quest id.
		_recipeIdsByQuestId = workOrders.ToDictionary(workOrder => workOrder.QuestId, workOrder => workOrder.RecipeId);
	}

	public int Count => _recipeIdsByQuestId.Count;

	public bool TryGetRecipeId(int questId, out int recipeId)
	{
		return _recipeIdsByQuestId.TryGetValue(questId, out recipeId);
	}

	public static WorkOrderRecipeTable LoadFromImportedFiles(IReadOnlyList<string> importedFiles)
	{
		// Java parity: static_data/quest_script_data/work_order.xml maps WorkOrdersData id -> recipe_id.
		var workOrderFile = importedFiles.FirstOrDefault(file => Path.GetFileName(file).Equals("work_order.xml", StringComparison.OrdinalIgnoreCase));
		if (workOrderFile == null)
			return new WorkOrderRecipeTable(Array.Empty<WorkOrderRecipeSummary>());

		return Load(workOrderFile);
	}

	public static WorkOrderRecipeTable Load(string filePath)
	{
		var document = XDocument.Load(filePath, LoadOptions.None);
		var workOrders = document
			.Descendants()
			.Where(element => element.Name.LocalName == "work_order")
			.Select(element => new WorkOrderRecipeSummary(
				ReadRequiredIntAttribute(element, "id"),
				ReadRequiredIntAttribute(element, "recipe_id")))
			.ToArray();
		return new WorkOrderRecipeTable(workOrders);
	}

	private static int ReadRequiredIntAttribute(XElement element, string attributeName)
	{
		var value = element.Attribute(attributeName)?.Value;
		if (!int.TryParse(value, out var parsed))
			throw new FormatException($"Missing or invalid work_order attribute '{attributeName}'.");

		return parsed;
	}
}

public sealed record WorkOrderRecipeSummary(int QuestId, int RecipeId);
