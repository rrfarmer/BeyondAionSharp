using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class RepurchaseStatePlanServiceTests
{
	[Fact]
	public void CreateReplaceDisabledPlan_ReplacesExistingSnapshotAndDedupesNonzeroObjectIds()
	{
		var playerObjectId = 1001;
		var oldSnapshot = new RepurchaseStateSnapshot(
			playerObjectId,
			[new RepurchaseSourceItem(Item(2001, itemId: 100000001), RepurchasePrice: 100)],
			"existing snapshot");
		var otherPlayerSnapshot = new RepurchaseStateSnapshot(
			1002,
			[new RepurchaseSourceItem(Item(3001, itemId: 100000002), RepurchasePrice: 300)],
			"other snapshot");
		var first = new RepurchaseSourceItem(Item(2002, itemId: 100000003, count: 1), RepurchasePrice: 125);
		var duplicate = new RepurchaseSourceItem(Item(2002, itemId: 100000004, count: 5), RepurchasePrice: 999);
		var second = new RepurchaseSourceItem(Item(2003, itemId: 100000005), RepurchasePrice: 250);

		var plan = RepurchaseStatePlanService.CreateReplaceDisabledPlan(
			playerObjectId,
			[first, duplicate, second],
			[oldSnapshot, otherPlayerSnapshot]);

		Assert.Equal(RepurchaseStateReplacePlanStatus.SnapshotReplaced, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.WouldReplaceMapEntry);
		Assert.False(plan.DidReplaceMapEntry);
		Assert.False(plan.PreservesJavaHashSetIterationOrder);
		Assert.Equal([2002], plan.DuplicateObjectIds);
		Assert.Equal([2002, 2003], plan.Snapshot.RepurchaseItems.Select(item => item.Item.ObjectId));
		Assert.DoesNotContain(plan.UpdatedSnapshots, snapshot => snapshot.PlayerObjectId == playerObjectId && snapshot.JavaSource == oldSnapshot.JavaSource);
		Assert.Contains(otherPlayerSnapshot, plan.UpdatedSnapshots);
		Assert.Contains("new HashSet<>(items)", plan.Snapshot.JavaSource, StringComparison.Ordinal);
		Assert.Contains("HashSet bucket iteration order is not emulated", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateGetDisabledPlan_ReturnsCurrentSnapshotOrEmptySnapshot()
	{
		var snapshot = new RepurchaseStateSnapshot(
			1001,
			[new RepurchaseSourceItem(Item(2001, itemId: 100000001), RepurchasePrice: 100)],
			"stored snapshot");

		var found = RepurchaseStatePlanService.CreateGetDisabledPlan(1001, [snapshot]);
		var missing = RepurchaseStatePlanService.CreateGetDisabledPlan(1002, [snapshot]);

		Assert.Equal(RepurchaseStateGetPlanStatus.SnapshotFound, found.Status);
		Assert.Same(snapshot, found.Snapshot);
		Assert.True(found.WouldQueryMapEntry);
		Assert.False(found.DidQueryMapEntry);

		Assert.Equal(RepurchaseStateGetPlanStatus.EmptySnapshot, missing.Status);
		Assert.Empty(missing.Snapshot.RepurchaseItems);
		Assert.Contains("Collections.emptySet", missing.JavaSource, StringComparison.Ordinal);
		Assert.False(missing.IsLive);
	}

	[Fact]
	public void CreateRemoveDisabledPlan_RemovesSnapshotAndLeavesAbsentPlayerAsNoOp()
	{
		var first = new RepurchaseStateSnapshot(
			1001,
			[new RepurchaseSourceItem(Item(2001, itemId: 100000001), RepurchasePrice: 100)],
			"first snapshot");
		var second = new RepurchaseStateSnapshot(
			1002,
			[new RepurchaseSourceItem(Item(3001, itemId: 100000002), RepurchasePrice: 300)],
			"second snapshot");

		var removed = RepurchaseStatePlanService.CreateRemoveDisabledPlan(1001, [first, second]);
		var missing = RepurchaseStatePlanService.CreateRemoveDisabledPlan(9999, [first, second]);

		Assert.Equal(RepurchaseStateRemovePlanStatus.SnapshotRemoved, removed.Status);
		Assert.True(removed.WouldRemoveMapEntry);
		Assert.False(removed.DidRemoveMapEntry);
		Assert.Equal([1002], removed.UpdatedSnapshots.Select(snapshot => snapshot.PlayerObjectId));
		Assert.Contains("repurchaseItems.remove", removed.JavaSource, StringComparison.Ordinal);

		Assert.Equal(RepurchaseStateRemovePlanStatus.NoSnapshot, missing.Status);
		Assert.Equal([1001, 1002], missing.UpdatedSnapshots.Select(snapshot => snapshot.PlayerObjectId));
		Assert.Contains("absent player key is a no-op", missing.JavaSource, StringComparison.Ordinal);
		Assert.False(missing.IsLive);
	}

	[Fact]
	public void CreateCanRepurchaseDisabledPlan_QueriesSnapshotByObjectId()
	{
		var snapshot = new RepurchaseStateSnapshot(
			1001,
			[
				new RepurchaseSourceItem(Item(2001, itemId: 100000001), RepurchasePrice: 100),
				new RepurchaseSourceItem(Item(2002, itemId: 100000002), RepurchasePrice: 200),
			],
			"stored snapshot");

		var allowed = RepurchaseStatePlanService.CreateCanRepurchaseDisabledPlan(1001, 2002, [snapshot]);
		var rejected = RepurchaseStatePlanService.CreateCanRepurchaseDisabledPlan(1001, 9999, [snapshot]);
		var missingPlayer = RepurchaseStatePlanService.CreateCanRepurchaseDisabledPlan(1002, 2002, [snapshot]);

		Assert.True(allowed.CanRepurchase);
		Assert.False(rejected.CanRepurchase);
		Assert.False(missingPlayer.CanRepurchase);
		Assert.True(allowed.WouldQueryMapEntry);
		Assert.False(allowed.DidQueryMapEntry);
		Assert.Contains("canRepurchase", allowed.JavaSource, StringComparison.Ordinal);
		Assert.False(allowed.IsLive);
	}

	private static InventoryItem Item(int objectId, int itemId, long count = 1)
	{
		return new InventoryItem
		{
			ObjectId = objectId,
			ItemId = itemId,
			Count = count,
			OwnerId = 1001,
			Location = 0,
			Slot = 65535,
		};
	}
}
