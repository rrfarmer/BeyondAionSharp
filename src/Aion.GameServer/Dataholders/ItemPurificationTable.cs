using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class ItemPurificationTable
{
	private readonly IReadOnlyDictionary<int, ItemPurificationSummary> _templatesByBaseItemId;
	private readonly IReadOnlyDictionary<int, IReadOnlyDictionary<int, ItemPurificationResultSummary>> _resultsByBaseItemId;

	public ItemPurificationTable(IReadOnlyList<ItemPurificationSummary> templates)
	{
		Templates = templates;
		_templatesByBaseItemId = new ReadOnlyDictionary<int, ItemPurificationSummary>(
			templates.ToDictionary(template => template.BaseItemId));
		_resultsByBaseItemId = new ReadOnlyDictionary<int, IReadOnlyDictionary<int, ItemPurificationResultSummary>>(
			templates.ToDictionary(
				template => template.BaseItemId,
				template => (IReadOnlyDictionary<int, ItemPurificationResultSummary>)new ReadOnlyDictionary<int, ItemPurificationResultSummary>(
					template.Results.ToDictionary(result => result.ResultItemId))));
	}

	public IReadOnlyList<ItemPurificationSummary> Templates { get; }

	public int Count => Templates.Count;

	public int ResultCount => Templates.Sum(template => template.Results.Count);

	public ItemPurificationSummary? GetItemPurificationTemplate(int baseItemId)
	{
		return _templatesByBaseItemId.GetValueOrDefault(baseItemId);
	}

	public IReadOnlyDictionary<int, ItemPurificationResultSummary>? GetResultItemMap(int baseItemId)
	{
		return _resultsByBaseItemId.GetValueOrDefault(baseItemId);
	}

	public ItemPurificationResultSummary? GetResultItem(int baseItemId, int resultItemId)
	{
		return GetResultItemMap(baseItemId)?.GetValueOrDefault(resultItemId);
	}
}

public sealed record ItemPurificationSummary(
	int BaseItemId,
	IReadOnlyList<ItemPurificationResultSummary> Results);

public sealed record ItemPurificationResultSummary(
	int ResultItemId,
	int MinEnchantCount,
	int NecessaryAbyssPoints,
	long NecessaryKinah,
	IReadOnlyList<ItemPurificationMaterialSummary> RequiredMaterials);

public sealed record ItemPurificationMaterialSummary(int ItemId, long ItemCount);
