using System;
using System.Collections.Generic;

namespace Aion.GameServer.Utils.Collections;

/// <summary>Java parity: utils/collections/DynamicElementCountSplitList (Sykra, Neon). Partition size by element length; Function&lt;Type,Integer&gt;→Func&lt;TType,int&gt;.</summary>
public class DynamicElementCountSplitList<TType> : SplitList<TType>
{
    private readonly Func<TType, int> lengthCalculator;
    private readonly int maxLength;

    public DynamicElementCountSplitList(List<TType> listToSplit, bool oneTimeSplitOnEmptyData, int maxLength,
        Func<TType, int> lengthCalculator)
        : base(listToSplit, oneTimeSplitOnEmptyData)
    {
        this.maxLength = maxLength;
        if (this.maxLength <= 0)
            throw new ArgumentException("maxLength needs to be larger than 0");
        this.lengthCalculator = lengthCalculator;
    }

    protected override ListPart<TType> NewListPart(int partNo, bool isLast)
    {
        return new DynamicElementCountListPart(this, partNo, isLast);
    }

    private class DynamicElementCountListPart : ListPart<TType>
    {
        private int currentLength;
        private readonly DynamicElementCountSplitList<TType> owner;

        public DynamicElementCountListPart(DynamicElementCountSplitList<TType> owner, int partNo, bool isLast)
            : base(partNo, isLast)
        {
            this.owner = owner;
        }

        public override bool Add(TType type)
        {
            if (base.Add(type))
            {
                currentLength += owner.lengthCalculator(type);
                return true;
            }
            return false;
        }

        protected internal override bool Fits(TType element)
        {
            int elementLength = owner.lengthCalculator(element);
            if (elementLength < 0)
                throw new InvalidOperationException("elementLength(" + elementLength + ") cannot be lesser than 0");
            if (elementLength > owner.maxLength)
                throw new InvalidOperationException("elementLength(" + elementLength + ") is greater than the maxLength (" + owner.maxLength + ")");
            return elementLength + currentLength <= owner.maxLength;
        }
    }
}
