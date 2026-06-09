using System;

namespace Aion.GameServer.Model.Trade;

/// <summary>Java parity: model/trade/TradePSItem (Simple, Neon).</summary>
public class TradePSItem : TradeItem
{
    private int itemObjId;
    private long price;

    public TradePSItem(int itemObjId, int itemId, long count, long price)
        : base(itemId, count)
    {
        this.SetPrice(price);
        this.SetItemObjId(itemObjId);
    }

    public void SetPrice(long price)
    {
        this.price = price;
    }

    public long GetPrice()
    {
        return price;
    }

    public void SetItemObjId(int itemObjId)
    {
        this.itemObjId = itemObjId;
    }

    public int GetItemObjId()
    {
        return itemObjId;
    }

    /// <summary>Decreases the count only if it would really decrease and wouldn't become negative.</summary>
    public void DecreaseCount(long decreaseCount)
    {
        if (decreaseCount > 0)
            this.count -= Math.Min(decreaseCount, count);
    }
}
