using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed record RepurchaseStateSnapshot(
	int PlayerObjectId,
	IReadOnlyList<RepurchaseSourceItem> RepurchaseItems,
	string JavaSource)
{
	public bool IsLive => false;
}

public enum RepurchaseStateReplacePlanStatus
{
	SnapshotReplaced,
}

public sealed record RepurchaseStateReplacePlan(
	RepurchaseStateReplacePlanStatus Status,
	int PlayerObjectId,
	RepurchaseStateSnapshot Snapshot,
	IReadOnlyList<RepurchaseStateSnapshot> UpdatedSnapshots,
	IReadOnlyList<int> DuplicateObjectIds,
	bool WouldReplaceMapEntry,
	bool DidReplaceMapEntry,
	bool PreservesJavaHashSetIterationOrder,
	string JavaSource,
	bool IsLive);

public enum RepurchaseStateGetPlanStatus
{
	SnapshotFound,
	EmptySnapshot,
}

public sealed record RepurchaseStateGetPlan(
	RepurchaseStateGetPlanStatus Status,
	int PlayerObjectId,
	RepurchaseStateSnapshot Snapshot,
	bool WouldQueryMapEntry,
	bool DidQueryMapEntry,
	string JavaSource,
	bool IsLive);

public enum RepurchaseStateRemovePlanStatus
{
	SnapshotRemoved,
	NoSnapshot,
}

public sealed record RepurchaseStateRemovePlan(
	RepurchaseStateRemovePlanStatus Status,
	int PlayerObjectId,
	IReadOnlyList<RepurchaseStateSnapshot> UpdatedSnapshots,
	bool WouldRemoveMapEntry,
	bool DidRemoveMapEntry,
	string JavaSource,
	bool IsLive);

public sealed record RepurchaseStateCanRepurchasePlan(
	int PlayerObjectId,
	int ItemObjectId,
	bool CanRepurchase,
	bool WouldQueryMapEntry,
	bool DidQueryMapEntry,
	string JavaSource,
	bool IsLive);

public static class RepurchaseStatePlanService
{
	public static RepurchaseStateReplacePlan CreateReplaceDisabledPlan(
		int playerObjectId,
		IReadOnlyList<RepurchaseSourceItem> repurchaseItems,
		IReadOnlyList<RepurchaseStateSnapshot>? currentSnapshots = null)
	{
		// Java parity: RepurchaseService.addRepurchaseItems(player, items)
		// replaces the ConcurrentHashMap entry with a new HashSet<>(items).
		var duplicateObjectIds = new List<int>();
		var snapshotItems = DeduplicateLikeAionObjectHashSet(repurchaseItems, duplicateObjectIds);
		var snapshot = new RepurchaseStateSnapshot(
			playerObjectId,
			snapshotItems,
			"RepurchaseService.addRepurchaseItems -> repurchaseItems.put(player.getObjectId(), new HashSet<>(items))");

		var updatedSnapshots = ReplaceSnapshot(currentSnapshots ?? Array.Empty<RepurchaseStateSnapshot>(), snapshot);
		return new RepurchaseStateReplacePlan(
			RepurchaseStateReplacePlanStatus.SnapshotReplaced,
			playerObjectId,
			snapshot,
			updatedSnapshots,
			duplicateObjectIds,
			WouldReplaceMapEntry: true,
			DidReplaceMapEntry: false,
			PreservesJavaHashSetIterationOrder: false,
			"RepurchaseService.addRepurchaseItems disabled plan records map replacement and nonzero object-id HashSet dedupe; Java HashSet bucket iteration order is not emulated",
			IsLive: false);
	}

	public static RepurchaseStateGetPlan CreateGetDisabledPlan(
		int playerObjectId,
		IReadOnlyList<RepurchaseStateSnapshot> currentSnapshots)
	{
		// Java parity: RepurchaseService.getRepurchaseItems returns the
		// current map entry or Collections.emptySet() when the player key is absent.
		var snapshot = FindSnapshot(playerObjectId, currentSnapshots)
			?? new RepurchaseStateSnapshot(
				playerObjectId,
				RepurchaseItems: Array.Empty<RepurchaseSourceItem>(),
				"RepurchaseService.getRepurchaseItems -> Collections.emptySet()");

		return new RepurchaseStateGetPlan(
			snapshot.RepurchaseItems.Count == 0 && FindSnapshot(playerObjectId, currentSnapshots) == null
				? RepurchaseStateGetPlanStatus.EmptySnapshot
				: RepurchaseStateGetPlanStatus.SnapshotFound,
			playerObjectId,
			snapshot,
			WouldQueryMapEntry: true,
			DidQueryMapEntry: false,
			snapshot.JavaSource,
			IsLive: false);
	}

	public static RepurchaseStateRemovePlan CreateRemoveDisabledPlan(
		int playerObjectId,
		IReadOnlyList<RepurchaseStateSnapshot> currentSnapshots)
	{
		// Java parity: RepurchaseService.removeRepurchaseItems removes the
		// player map entry if present and otherwise leaves the map unchanged.
		var hadSnapshot = FindSnapshot(playerObjectId, currentSnapshots) != null;
		var updatedSnapshots = currentSnapshots
			.Where(snapshot => snapshot.PlayerObjectId != playerObjectId)
			.ToArray();

		return new RepurchaseStateRemovePlan(
			hadSnapshot ? RepurchaseStateRemovePlanStatus.SnapshotRemoved : RepurchaseStateRemovePlanStatus.NoSnapshot,
			playerObjectId,
			updatedSnapshots,
			WouldRemoveMapEntry: true,
			DidRemoveMapEntry: false,
			hadSnapshot
				? "RepurchaseService.removeRepurchaseItems -> repurchaseItems.remove(player.getObjectId())"
				: "RepurchaseService.removeRepurchaseItems -> remove absent player key is a no-op",
			IsLive: false);
	}

	public static RepurchaseStateCanRepurchasePlan CreateCanRepurchaseDisabledPlan(
		int playerObjectId,
		int itemObjectId,
		IReadOnlyList<RepurchaseStateSnapshot> currentSnapshots)
	{
		// Java parity: RepurchaseService.canRepurchase streams the current
		// getRepurchaseItems snapshot and compares item.getObjectId().
		var getPlan = CreateGetDisabledPlan(playerObjectId, currentSnapshots);
		var canRepurchase = getPlan.Snapshot.RepurchaseItems.Any(item => item.Item.ObjectId == itemObjectId);
		return new RepurchaseStateCanRepurchasePlan(
			playerObjectId,
			itemObjectId,
			canRepurchase,
			WouldQueryMapEntry: true,
			DidQueryMapEntry: false,
			"RepurchaseService.canRepurchase -> getRepurchaseItems(player.getObjectId()).stream().anyMatch(item.getObjectId() == itemObjectId)",
			IsLive: false);
	}

	private static IReadOnlyList<RepurchaseSourceItem> DeduplicateLikeAionObjectHashSet(
		IReadOnlyList<RepurchaseSourceItem> repurchaseItems,
		List<int> duplicateObjectIds)
	{
		var seenNonzeroObjectIds = new HashSet<int>();
		var snapshotItems = new List<RepurchaseSourceItem>();

		foreach (var item in repurchaseItems)
		{
			var objectId = item.Item.ObjectId;
			if (objectId != 0 && !seenNonzeroObjectIds.Add(objectId))
			{
				duplicateObjectIds.Add(objectId);
				continue;
			}

			snapshotItems.Add(item);
		}

		return snapshotItems.ToArray();
	}

	private static RepurchaseStateSnapshot[] ReplaceSnapshot(
		IReadOnlyList<RepurchaseStateSnapshot> currentSnapshots,
		RepurchaseStateSnapshot replacement)
	{
		return currentSnapshots
			.Where(snapshot => snapshot.PlayerObjectId != replacement.PlayerObjectId)
			.Append(replacement)
			.ToArray();
	}

	private static RepurchaseStateSnapshot? FindSnapshot(
		int playerObjectId,
		IReadOnlyList<RepurchaseStateSnapshot> currentSnapshots)
	{
		return currentSnapshots.FirstOrDefault(snapshot => snapshot.PlayerObjectId == playerObjectId);
	}
}
