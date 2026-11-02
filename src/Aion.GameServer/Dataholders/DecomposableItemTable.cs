using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class DecomposableItemTable
{
	private readonly IReadOnlyDictionary<int, DecomposableItemSummary> _templatesByItemId;

	public DecomposableItemTable(IReadOnlyList<DecomposableItemSummary> templates)
	{
		Templates = templates;
		// Java parity: dataholders/DecomposableItemsData.afterUnmarshal keeps the retail and override rows in separate
		// maps and lets the overrides win only after every file of the folder has been read, so the same item id
		// legitimately appears twice here. Entries whose branches are all empty are dropped, as Java drops them.
		var byItemId = new Dictionary<int, DecomposableItemSummary>();
		var overrides = new Dictionary<int, DecomposableItemSummary>();
		foreach (var template in templates)
		{
			var target = template.IsOverride ? overrides : byItemId;
			if (!target.TryAdd(template.ItemId, template))
				throw new InvalidDataException($"Duplicate decomposable item {template.ItemId}");
		}

		foreach (var entry in overrides)
			byItemId[entry.Key] = entry.Value;
		foreach (var itemId in byItemId.Where(e => e.Value.Sets.Count == 0).Select(e => e.Key).ToArray())
			byItemId.Remove(itemId);

		OverrideCount = overrides.Count;
		_templatesByItemId = new ReadOnlyDictionary<int, DecomposableItemSummary>(byItemId);
	}

	public IReadOnlyList<DecomposableItemSummary> Templates { get; }

	public int Count => _templatesByItemId.Count;

	public int OverrideCount { get; }

	public int SelectableCount => _templatesByItemId.Values.Count(template => template.IsSelectable);

	/// <summary>Java parity: dataholders/DecomposableItemsData.get.</summary>
	public DecomposableItemSummary? GetTemplate(int itemId)
	{
		return _templatesByItemId.GetValueOrDefault(itemId);
	}
}

// Java parity: model/templates/item/DecomposableItem.
public sealed record DecomposableItemSummary(
	int ItemId,
	bool IsSelectable,
	bool IsOnlyOne,
	bool IsOverride,
	IReadOnlyList<DecomposableSetSummary> Sets);

// Java parity: model/templates/item/DecomposableSet.
public sealed record DecomposableSetSummary(
	float Chance,
	int MinLevel,
	int MaxLevel,
	string Race,
	IReadOnlySet<string> PlayerClasses,
	IReadOnlyList<DecomposedItemSummary> Items)
{
	public bool HasClassRestrictions => PlayerClasses.Count > 0;
}

// Java parity: model/templates/item/DecomposedItem.
public sealed record DecomposedItemSummary(
	int ItemId,
	int Count,
	float Chance);
