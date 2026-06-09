using System;
using System.Collections.Generic;

namespace Aion.GameServer.Utils.Collections;

/// <summary>Java parity: utils/collections/FixedElementCountSplitList (Sykra, Neon). Partition length by fixed max element count.</summary>
public class FixedElementCountSplitList<TType> : SplitList<TType>
{
    private readonly int maxElementCount;

    public FixedElementCountSplitList(List<TType> listToSplit, bool oneTimeSplitOnEmptyData, int maxElementCount)
        : base(listToSplit, oneTimeSplitOnEmptyData)
    {
        this.maxElementCount = maxElementCount;
        if (maxElementCount <= 0)
            throw new ArgumentException("maxElementCount needs to be larger than 0");
    }

    protected override ListPart<TType> NewListPart(int partNo, bool isLast)
    {
        return new FixedElementCountListPart(this, partNo, isLast);
    }

    private class FixedElementCountListPart : ListPart<TType>
    {
        private readonly FixedElementCountSplitList<TType> owner;

        public FixedElementCountListPart(FixedElementCountSplitList<TType> owner, int partNo, bool isLast)
            : base(partNo, isLast)
        {
            this.owner = owner;
        }

        protected internal override bool Fits(TType element)
        {
            return Count < owner.maxElementCount;
        }
    }
}
