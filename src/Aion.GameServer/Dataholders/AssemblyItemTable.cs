using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class AssemblyItemTable
{
	private readonly IReadOnlyDictionary<int, AssemblyItemSummary> _templatesByItemId;

	public AssemblyItemTable(IReadOnlyList<AssemblyItemSummary> templates)
	{
		Templates = templates;
		_templatesByItemId = new ReadOnlyDictionary<int, AssemblyItemSummary>(
			templates.ToDictionary(template => template.ItemId));
	}

	public IReadOnlyList<AssemblyItemSummary> Templates { get; }

	public int Count => Templates.Count;

	public AssemblyItemSummary? GetAssemblyItem(int itemId)
	{
		// Java parity: dataholders/AssemblyItemsData.getAssemblyItem.
		return _templatesByItemId.GetValueOrDefault(itemId);
	}
}

// Java parity: model/templates/item/AssemblyItem.
public sealed record AssemblyItemSummary(int ItemId, IReadOnlyList<int> Parts);
