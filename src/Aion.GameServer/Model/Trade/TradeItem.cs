using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Model.Trade;

/// <summary>Java parity: model/trade/TradeItem (ATracer, Neon).</summary>
public class TradeItem
{
    private readonly int itemId;
    protected long count;

    public TradeItem(int itemId, long count)
    {
        this.itemId = itemId;
        this.count = count;
    }

    public ItemTemplate GetItemTemplate()
    {
        return DataManager.ITEM_DATA.GetItemTemplate(itemId);
    }

    public int GetItemId()
    {
        return itemId;
    }

    public long GetCount()
    {
        return count;
    }
}
