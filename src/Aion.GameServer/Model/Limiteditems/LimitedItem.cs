using System.Collections.Generic;

namespace Aion.GameServer.Model.Limiteditems;

/// <summary>Java parity: model/limiteditems/LimitedItem (xTz, Neon).</summary>
public class LimitedItem
{
    private int itemId;
    private int sellLimit;
    private int buyLimit;
    private int defaultSellLimit;
    private string salesTime;
    private Dictionary<int, int> buyCounts = new Dictionary<int, int>();

    public LimitedItem(int itemId, int sellLimit, int buyLimit, string salesTime)
    {
        this.itemId = itemId;
        this.sellLimit = sellLimit;
        this.buyLimit = buyLimit;
        this.defaultSellLimit = sellLimit;
        this.salesTime = salesTime;
    }

    public int GetItemId()
    {
        return itemId;
    }

    public void SetBuyCount(int playerObjectId, int count)
    {
        buyCounts[playerObjectId] = count;
    }

    public int GetBuyCount(int playerObjectId)
    {
        return buyCounts.TryGetValue(playerObjectId, out var v) ? v : 0;
    }

    public void SetItem(int itemId)
    {
        this.itemId = itemId;
    }

    public int GetSellLimit()
    {
        return sellLimit;
    }

    public int GetBuyLimit()
    {
        return buyLimit;
    }

    public void SetToDefault()
    {
        sellLimit = defaultSellLimit;
        buyCounts.Clear();
    }

    public void SetSellLimit(int sellLimit)
    {
        this.sellLimit = sellLimit;
    }

    public int GetDefaultSellLimit()
    {
        return defaultSellLimit;
    }

    public string GetSalesTime()
    {
        return salesTime;
    }
}
