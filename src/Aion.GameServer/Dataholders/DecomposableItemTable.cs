using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class DecomposableItemTable
{
	private readonly IReadOnlyDictionary<int, DecomposableItemSummary> _templatesByItemId;
	private readonly IReadOnlyDictionary<int, IReadOnlyList<ExtractedItemsCollectionSummary>> _normalItemsByItemId;
	private readonly IReadOnlyDictionary<int, IReadOnlyList<ResultedItemSummary>> _selectableItemsByItemId;

	public DecomposableItemTable(IReadOnlyList<DecomposableItemSummary> templates)
	{
		Templates = templates;
		_templatesByItemId = new ReadOnlyDictionary<int, DecomposableItemSummary>(
			templates.ToDictionary(template => template.ItemId));

		var normalItems = new Dictionary<int, IReadOnlyList<ExtractedItemsCollectionSummary>>();
		var selectableItems = new Dictionary<int, IReadOnlyList<ResultedItemSummary>>();
		foreach (var template in templates)
		{
			if (template.IsSelectable)
			{
				// Java parity: dataholders/DecomposableItemsData.afterUnmarshal stores only first group for selectable boxes.
				selectableItems[template.ItemId] = template.ItemsCollections.FirstOrDefault()?.Items ?? [];
			}
			else
			{
				normalItems[template.ItemId] = template.ItemsCollections;
			}
		}

		_normalItemsByItemId = new ReadOnlyDictionary<int, IReadOnlyList<ExtractedItemsCollectionSummary>>(normalItems);
		_selectableItemsByItemId = new ReadOnlyDictionary<int, IReadOnlyList<ResultedItemSummary>>(selectableItems);
	}

	public IReadOnlyList<DecomposableItemSummary> Templates { get; }

	public int Count => Templates.Count;

	public int NormalCount => _normalItemsByItemId.Count;

	public int SelectableCount => _selectableItemsByItemId.Count;

	public DecomposableItemSummary? GetTemplate(int itemId)
	{
		return _templatesByItemId.GetValueOrDefault(itemId);
	}

	public IReadOnlyList<ExtractedItemsCollectionSummary>? GetInfoByItemId(int itemId)
	{
		// Java parity: dataholders/DecomposableItemsData.getInfoByItemId.
		return _normalItemsByItemId.GetValueOrDefault(itemId);
	}

	public IReadOnlyList<ResultedItemSummary>? GetSelectableItems(int itemId)
	{
		// Java parity: dataholders/DecomposableItemsData.getSelectableItems returns a defensive copy.
		return _selectableItemsByItemId.TryGetValue(itemId, out var items) ? items.ToArray() : null;
	}
}

// Java parity: model/templates/item/DecomposableItemInfo.
public sealed record DecomposableItemSummary(
	int ItemId,
	bool IsSelectable,
	IReadOnlyList<ExtractedItemsCollectionSummary> ItemsCollections);

// Java parity: model/templates/rewards/ExtractedItemsCollection.
public sealed record ExtractedItemsCollectionSummary(
	float Chance,
	int MinLevel,
	int MaxLevel,
	IReadOnlyList<ResultedItemSummary> Items,
	IReadOnlyList<RandomItemSummary> RandomItems);

// Java parity: model/templates/rewards/ResultedItem.
public sealed record ResultedItemSummary(
	int ItemId,
	int MinCount,
	int MaxCount,
	string Race,
	IReadOnlySet<string> PlayerClasses)
{
	public bool HasClassRestrictions => PlayerClasses.Count > 0;
}

// Java parity: model/templates/rewards/RandomItem.
public sealed record RandomItemSummary(
	string Type,
	int MinCount,
	int MaxCount);
