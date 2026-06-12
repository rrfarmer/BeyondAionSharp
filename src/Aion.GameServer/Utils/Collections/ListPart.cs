using System.Collections.Generic;

namespace Aion.GameServer.Utils.Collections;

/// <summary>
/// Java parity: utils/collections/ListPart (Neon). Java <c>ListPart extends ArrayList</c>.
/// C#-vs-Java foundational diff (rule 8): Java ArrayList.add is overridable, but C# List&lt;T&gt;.Add is non-virtual —
/// so ListPart declares a <c>new virtual bool Add</c> (matching Java's boolean return) that subclasses override.
/// fits/setLast are package-private in Java (called by SplitList in the same package) → protected internal.
/// </summary>
public abstract class ListPart<TType> : List<TType>
{
    private readonly int partNo;
    private bool isLast;

    protected ListPart(int partNo, bool isLast)
    {
        this.partNo = partNo;
        this.isLast = isLast;
    }

    public int GetPartNo()
    {
        return partNo;
    }

    // Java parity: java.util.List.size()
    public int Size()
    {
        return Count;
    }

    public bool IsFirst()
    {
        return partNo == 1;
    }

    public bool IsLast()
    {
        return isLast;
    }

    protected internal void SetLast(bool isLast)
    {
        this.isLast = isLast;
    }

    // Java ArrayList.add(E)→boolean; hide the non-virtual List<T>.Add so subclasses can override.
    public new virtual bool Add(TType item)
    {
        base.Add(item);
        return true;
    }

    protected internal abstract bool Fits(TType element);
}
