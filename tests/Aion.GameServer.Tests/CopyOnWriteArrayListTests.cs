using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Tests;

/// <summary>
/// Tests for CopyOnWriteArrayList, the C# port of java.util.concurrent.CopyOnWriteArrayList used by
/// RiftManager/Legion/PlayerAlliance. The key guarantee is that iteration walks an immutable snapshot,
/// so a concurrent Add/Remove never throws "Collection was modified" (which a plain List does) — the
/// live crash observed on the running gameserver.
/// </summary>
public sealed class CopyOnWriteArrayListTests
{
    [Fact]
    public void BasicOperations_MatchListSemantics()
    {
        var list = new CopyOnWriteArrayList<int>();
        Assert.Empty(list);

        list.Add(10);
        list.Add(20);
        list.Add(30);
        Assert.Equal(3, list.Count);
        Assert.Contains(20, list);
        Assert.Equal(1, list.IndexOf(20));
        Assert.Equal(20, list[1]);

        Assert.True(list.Remove(20));   // remove by value (not index), matching Java remove(Object)
        Assert.False(list.Remove(999));
        Assert.Equal(new[] { 10, 30 }, list);

        list.Insert(1, 15);
        Assert.Equal(new[] { 10, 15, 30 }, list);

        list.RemoveAt(0);
        Assert.Equal(new[] { 15, 30 }, list);

        list[0] = 99;
        Assert.Equal(new[] { 99, 30 }, list);

        list.Clear();
        Assert.Empty(list);
    }

    [Fact]
    public void ConstructedFromSource_SnapshotsElements()
    {
        var src = new List<int> { 1, 2, 3 };
        var list = new CopyOnWriteArrayList<int>(src);
        src.Add(4); // mutating the source afterwards must not leak into the CoW list
        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void Enumeration_IsSnapshot_UnaffectedByMidIterationMutation()
    {
        var list = new CopyOnWriteArrayList<int>();
        for (int i = 0; i < 5; i++)
            list.Add(i);

        var seen = new List<int>();
        foreach (int x in list)
        {
            seen.Add(x);
            if (x == 2)
            {
                list.Add(99);  // a plain List would throw "Collection was modified" right here
                list.Remove(0);
            }
        }

        // The in-progress enumerator saw the original snapshot, undisturbed by the mutations...
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, seen);
        // ...but the mutations were applied to the live list.
        Assert.Equal(new[] { 1, 2, 3, 4, 99 }, list);
    }

    [Fact]
    public async Task ConcurrentWritesDuringEnumeration_DoNotThrow()
    {
        var list = new CopyOnWriteArrayList<int>();
        for (int i = 0; i < 50; i++)
            list.Add(i);

        using var cts = new CancellationTokenSource();
        Task writer = Task.Run(() =>
        {
            int n = 1000;
            while (!cts.Token.IsCancellationRequested)
            {
                list.Add(n);
                list.Remove(n);
                n++;
            }
        });

        // Hammer enumeration while the writer mutates. A plain List here throws
        // InvalidOperationException; the snapshot iterator must not.
        for (int iter = 0; iter < 2000; iter++)
        {
            int count = 0;
            foreach (int _ in list)
                count++;
            Assert.True(count >= 50); // never fewer than the initial elements
        }

        cts.Cancel();
        await writer;
    }
}
