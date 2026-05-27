using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class ItemRestrictionCleanupTable
{
	private readonly IReadOnlyDictionary<int, ItemRestrictionCleanupSummary> _cleanupsByItemId;

	public ItemRestrictionCleanupTable(IReadOnlyList<ItemRestrictionCleanupSummary> cleanups)
	{
		Cleanups = cleanups;
		_cleanupsByItemId = new ReadOnlyDictionary<int, ItemRestrictionCleanupSummary>(
			cleanups
				.GroupBy(cleanup => cleanup.ItemId)
				.ToDictionary(group => group.Key, group => group.Last()));
	}

	public IReadOnlyList<ItemRestrictionCleanupSummary> Cleanups { get; }

	public int Count => Cleanups.Count;

	public ItemRestrictionCleanupSummary? GetCleanup(int itemId)
	{
		return _cleanupsByItemId.GetValueOrDefault(itemId);
	}

	public bool HasAccountOrLegionWarehouseStorabilityDisabled(int itemId)
	{
		// Java parity: dataholders/ItemRestrictionCleanupData.hasAccountOrLegionWhStorabilityDisabled.
		return Cleanups.Any(cleanup => cleanup.ItemId == itemId && (cleanup.AccountWarehouse == 0 || cleanup.LegionWarehouse == 0));
	}
}

public sealed record ItemRestrictionCleanupSummary(
	int ItemId,
	sbyte Trade = -1,
	sbyte Sell = -1,
	sbyte Warehouse = -1,
	sbyte AccountWarehouse = -1,
	sbyte LegionWarehouse = -1);
