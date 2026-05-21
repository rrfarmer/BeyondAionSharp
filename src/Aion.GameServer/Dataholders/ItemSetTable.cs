using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class ItemSetTable
{
	private readonly IReadOnlyDictionary<int, ItemSetSummary> _setsById;
	private readonly IReadOnlyDictionary<int, ItemSetSummary> _setsByItemId;

	public ItemSetTable(IReadOnlyList<ItemSetSummary> sets)
	{
		Sets = sets;
		_setsById = new ReadOnlyDictionary<int, ItemSetSummary>(
			sets.ToDictionary(set => set.SetId));
		var setsByItemId = new Dictionary<int, ItemSetSummary>();
		foreach (var set in sets)
		{
			foreach (var itemId in set.ItemIds)
				setsByItemId[itemId] = set;
		}

		_setsByItemId = new ReadOnlyDictionary<int, ItemSetSummary>(setsByItemId);
	}

	public IReadOnlyList<ItemSetSummary> Sets { get; }

	public int Count => Sets.Count;

	public ItemSetSummary? GetItemSetTemplate(int setId)
	{
		// Java parity: dataholders/ItemSetData.getItemSetTemplate.
		return _setsById.GetValueOrDefault(setId);
	}

	public ItemSetSummary? GetItemSetTemplateByItemId(int itemId)
	{
		// Java parity: dataholders/ItemSetData.getItemSetTemplateByItemId.
		return _setsByItemId.GetValueOrDefault(itemId);
	}
}

public sealed record ItemSetSummary(
	int SetId,
	string Name,
	IReadOnlySet<int> ItemIds,
	IReadOnlyList<ItemSetPartBonus> PartBonuses,
	ItemSetFullBonus? FullBonus);

public sealed record ItemSetPartBonus(
	int Count,
	IReadOnlyList<ItemStatModifier> Modifiers);

public sealed record ItemSetFullBonus(
	int Count,
	IReadOnlyList<ItemStatModifier> Modifiers);
