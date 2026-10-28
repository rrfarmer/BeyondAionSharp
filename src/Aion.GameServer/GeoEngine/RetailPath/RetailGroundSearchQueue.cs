namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Inactive native queue mechanics: linked 0x140221ce0; F-heap 0x140227240/360/440/5f0/840. Never substitute stable binary-heap ties.</summary>
internal sealed class RetailGroundSearchQueue(RetailGroundQueueKind kind, int rankCapacity)
{
    internal sealed class Entry(int label)
    {
        internal int Label { get; } = label;
        internal float Key;
        internal bool Queued, Mark;
        internal int Degree;
        internal Entry? Previous, Next, Parent, Child;
    }

    private Entry? head;
    private readonly Entry?[] ranks = kind == RetailGroundQueueKind.FibonacciHeap ? new Entry?[rankCapacity] : [];
    internal int Count { get; private set; }
    internal bool IsEmpty => head is null;
    internal Entry? Minimum => head;

    internal void Add(Entry entry)
    {
        if (entry.Queued) throw new QueueSupportException("Native queue already contains this entry.");
        if (Count >= 1_000_000) throw new QueueSupportException("Native queue count refusal.");
        entry.Queued = true; entry.Mark = false; Count++;
        if (kind == RetailGroundQueueKind.LinkedList)
        {
            var current = head; Entry? previous = null;
            while (current is not null && entry.Key > current.Key) { previous = current; current = current.Next; }
            entry.Previous = previous; entry.Next = current;
            if (previous is null) head = entry; else previous.Next = entry;
            if (current is not null) current.Previous = entry;
            return;
        }
        if (head is null) { head = entry; entry.Previous = entry.Next = entry; return; }
        InsertBefore(head, entry);
        if (entry.Key < head.Key) head = entry;
    }

    internal Entry Pop()
    {
        var minimum = head ?? throw new InvalidOperationException("Queue is empty.");
        if (kind == RetailGroundQueueKind.LinkedList)
        {
            head = minimum.Next;
            if (head is not null) head.Previous = null;
            minimum.Previous = minimum.Next = null; minimum.Queued = false; Count--; return minimum;
        }
        var child = minimum.Child;
        while (child is not null)
        {
            var next = ReferenceEquals(child, child.Next) ? null : child.Next;
            RemoveRing(child); child.Parent = null; child.Queued = false; Count--;
            Add(child); child = next;
        }
        var nextRoot = ReferenceEquals(head, head!.Next) ? null : head.Next;
        head = nextRoot;
        RemoveRing(minimum);
        minimum.Previous = minimum.Next = minimum;
        minimum.Parent = minimum.Child = null; minimum.Queued = false;
        Consolidate(); Count--; return minimum;
    }

    internal void Decrease(Entry entry, float newKey)
    {
        if (!entry.Queued || newKey > entry.Key) throw new QueueSupportException("Decrease requires a queued entry and a nonincreasing key.");
        if (kind == RetailGroundQueueKind.LinkedList)
        {
            if (entry.Previous is null) head = entry.Next; else entry.Previous.Next = entry.Next;
            if (entry.Next is not null) entry.Next.Previous = entry.Previous;
            entry.Queued = false; Count--; entry.Key = newKey; Add(entry); return;
        }
        entry.Key = newKey;
        if (entry.Parent is null)
        {
            if (head is null || entry.Key < head.Key) head = entry;
            return;
        }
        var current = entry; var parent = current.Parent;
        if (current.Key >= parent.Key) return;
        while (parent is not null)
        {
            var grandparent = parent.Parent;
            var nextChild = ReferenceEquals(current, current.Next) ? null : current.Next;
            RemoveRing(current); parent.Degree--;
            if (ReferenceEquals(parent.Child, current)) parent.Child = nextChild;
            current.Parent = null; current.Queued = false; Count--; Add(current);
            if (!parent.Mark)
            {
                if (grandparent is not null) parent.Mark = true;
                return;
            }
            current = parent; parent = grandparent;
        }
    }

    private void Consolidate()
    {
        if (head is null || ReferenceEquals(head, head.Previous)) return;
        Array.Clear(ranks);
        var last = head.Previous!; var scan = head;
        while (true)
        {
            var original = scan; var next = scan.Next!;
            if (scan.Key < head!.Key) head = scan;
            while (true)
            {
                if ((uint)scan.Degree >= (uint)ranks.Length)
                    throw new QueueSupportException("Observed native heap rank capacity is insufficient; native unchecked access is not reproduced.");
                var other = ranks[scan.Degree];
                if (other is null) break;
                ranks[scan.Degree] = null; scan = Link(scan, other);
            }
            ranks[scan.Degree] = scan;
            if (scan.Key <= head.Key) head = scan;
            if (ReferenceEquals(original, last)) break;
            scan = next;
        }
    }

    private static Entry Link(Entry first, Entry second)
    {
        // Equal-key first argument stays parent. Consolidation's later <= min update is a separate native tie rule.
        if (first.Key > second.Key) (first, second) = (second, first);
        RemoveRing(second);
        if (first.Child is null) { first.Child = second; second.Previous = second.Next = second; }
        else InsertBefore(first.Child, second);
        second.Mark = false; second.Parent = first; first.Degree++; return first;
    }
    private static void InsertBefore(Entry anchor, Entry entry)
    {
        entry.Next = anchor; entry.Previous = anchor.Previous;
        anchor.Previous!.Next = entry; anchor.Previous = entry;
    }
    private static void RemoveRing(Entry entry)
    {
        entry.Previous!.Next = entry.Next; entry.Next!.Previous = entry.Previous;
    }
    internal sealed class QueueSupportException(string message) : Exception(message);
}
