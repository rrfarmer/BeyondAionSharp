using Aion.GameServer.GeoEngine.RetailPath;

namespace Aion.GameServer.Tests;

public sealed class RetailGroundSearchQueueTests
{
    [Fact]
    public void EqualLinkedKeysAreNewBeforeOldWhileHeapConsolidationHasItsOwnTieOrder()
    {
        Assert.Equal(new[] { 2, 1, 0 }, EqualOrder(RetailGroundQueueKind.LinkedList));
        // A is initially minimum. Removing A links equal C (current scanned root) over B.
        Assert.Equal(new[] { 0, 2, 1 }, EqualOrder(RetailGroundQueueKind.FibonacciHeap));
    }

    [Theory]
    [InlineData(RetailGroundQueueKind.LinkedList)]
    [InlineData(RetailGroundQueueKind.FibonacciHeap)]
    public void MixedPrioritiesAndDecreaseKeepLogicalCountAndEachIdentityOnce(RetailGroundQueueKind kind)
    {
        var queue = new RetailGroundSearchQueue(kind, 16);
        var entries = new[] { 7, 3, 5, 1, 4, 2, 6 }.Select((key, label) => new RetailGroundSearchQueue.Entry(label) { Key = key }).ToArray();
        foreach (var entry in entries) queue.Add(entry);
        queue.Decrease(entries[0], 0);
        Assert.Equal(7, queue.Count);
        var actual = new List<float>();
        while (!queue.IsEmpty) actual.Add(queue.Pop().Key);
        Assert.Equal(new float[] { 0, 1, 2, 3, 4, 5, 6 }, actual);
        Assert.Equal(0, queue.Count); Assert.All(entries, e => Assert.False(e.Queued));
    }

    [Fact]
    public void HeapCutsMarkANonrootThenCascadeAfterItsSecondLostChild()
    {
        var queue = new RetailGroundSearchQueue(RetailGroundQueueKind.FibonacciHeap, 16);
        var entries = Enumerable.Range(0, 17).Select(i => new RetailGroundSearchQueue.Entry(i) { Key = i }).ToArray();
        foreach (var entry in entries) queue.Add(entry);
        Assert.Equal(0, queue.Pop().Label);
        var parent = entries.First(e => e.Queued && e.Parent is not null && e.Degree >= 2);
        var grandparent = parent.Parent;
        var first = parent.Child!;
        queue.Decrease(first, -100);
        Assert.Null(first.Parent); Assert.True(parent.Mark); Assert.Same(grandparent, parent.Parent);
        var second = parent.Child!;
        queue.Decrease(second, -101);
        Assert.Null(second.Parent); Assert.Null(parent.Parent);
        Assert.Equal(16, queue.Count);
        Assert.Same(second, queue.Pop()); Assert.Same(first, queue.Pop());
        var remaining = new List<int>(); while (!queue.IsEmpty) remaining.Add(queue.Pop().Label);
        Assert.Equal(entries.Where(e => e.Label != 0 && !ReferenceEquals(e, first) && !ReferenceEquals(e, second)).Select(e => e.Label).OrderBy(i => i), remaining);
    }

    [Fact]
    public void InsufficientObservedRankCapacityIsVisibleInsteadOfUncheckedWrite()
    {
        var queue = new RetailGroundSearchQueue(RetailGroundQueueKind.FibonacciHeap, 1);
        for (int i = 0; i < 3; i++) queue.Add(new(i) { Key = i });
        var error = Assert.Throws<RetailGroundSearchQueue.QueueSupportException>(() => queue.Pop());
        Assert.Contains("rank capacity", error.Message);
    }

    [Theory]
    [InlineData(RetailGroundQueueKind.LinkedList)]
    [InlineData(RetailGroundQueueKind.FibonacciHeap)]
    public void DuplicateOrIncreasingKeyInputsDoNotSilentlyCorruptQueue(RetailGroundQueueKind kind)
    {
        var queue = new RetailGroundSearchQueue(kind, 16); var entry = new RetailGroundSearchQueue.Entry(1) { Key = 3 };
        queue.Add(entry);
        Assert.Throws<RetailGroundSearchQueue.QueueSupportException>(() => queue.Add(entry));
        Assert.Throws<RetailGroundSearchQueue.QueueSupportException>(() => queue.Decrease(entry, 4));
        Assert.Equal(1, queue.Count); Assert.Same(entry, queue.Pop());
    }

    private static int[] EqualOrder(RetailGroundQueueKind kind)
    {
        var queue = new RetailGroundSearchQueue(kind, 16);
        for (int i = 0; i < 3; i++) queue.Add(new(i) { Key = 1 });
        return [queue.Pop().Label, queue.Pop().Label, queue.Pop().Label];
    }
}
