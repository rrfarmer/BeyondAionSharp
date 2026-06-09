using System.Collections.Generic;

namespace Aion.GameServer.Model.Limiteditems;

/// <summary>Java parity: model/limiteditems/LimitedTradeNpc (xTz).</summary>
public class LimitedTradeNpc
{
    private readonly List<LimitedItem> limitedItems = new List<LimitedItem>();

    public void AddLimitedItems(List<LimitedItem> limitedItems)
    {
        this.limitedItems.AddRange(limitedItems);
    }

    public List<LimitedItem> GetLimitedItems()
    {
        return limitedItems;
    }
}
