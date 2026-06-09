using System.Collections;
using System.Collections.Generic;

namespace Aion.GameServer.Utils.Collections;

/// <summary>
/// Java parity: utils/collections/SplitList (xTz, Rolandas, Sykra, Neon). Generic list splitter into partitions.
/// Iterable&lt;ListPart&gt;→IEnumerable&lt;ListPart&gt;; Collections.singletonList/emptyList→new List.
/// </summary>
public abstract class SplitList<TType> : IEnumerable<ListPart<TType>>
{
    private readonly List<TType> listToSplit;
    private readonly bool oneTimeSplitOnEmptyData;

    protected SplitList(List<TType> listToSplit, bool oneTimeSplitOnEmptyData)
    {
        this.listToSplit = listToSplit;
        this.oneTimeSplitOnEmptyData = oneTimeSplitOnEmptyData;
    }

    private List<ListPart<TType>> PartitionList()
    {
        if (listToSplit.Count == 0 && oneTimeSplitOnEmptyData)
            return new List<ListPart<TType>> { NewListPart(1, true) };
        else if (listToSplit.Count == 0)
            return new List<ListPart<TType>>();

        List<ListPart<TType>> parts = new();
        int startIndex = 0, partNo = 1;
        while (startIndex < listToSplit.Count)
        {
            ListPart<TType> listPart = NewListPart(partNo++, false);
            while (startIndex < listToSplit.Count && listPart.Fits(listToSplit[startIndex]))
                listPart.Add(listToSplit[startIndex++]);
            parts.Add(listPart);
        }
        if (parts.Count != 0)
        {
            ListPart<TType> types = parts[parts.Count - 1];
            types.SetLast(true);
        }
        return parts;
    }

    public IEnumerator<ListPart<TType>> GetEnumerator()
    {
        return PartitionList().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    protected abstract ListPart<TType> NewListPart(int partNo, bool isLast);
}
