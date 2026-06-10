using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Trade;

/// <summary>Java parity: model/trade/ExchangeItem (ATracer). Item red-tolerated.</summary>
public class ExchangeItem
{
    private int itemObjId;
    private long itemCount;
    private Item item;

    /// <summary>Used when exchange item != original item</summary>
    public ExchangeItem(int itemObjId, long itemCount, Item item)
    {
        this.itemObjId = itemObjId;
        this.itemCount = itemCount;
        this.item = item;
    }

    /// <param name="item">the item to set</param>
    public void SetItem(Item item)
    {
        this.item = item;
    }

    public void AddCount(long countToAdd)
    {
        this.itemCount += countToAdd;
        this.item.SetItemCount(itemCount);
    }

    /// <returns>the newItem</returns>
    public Item GetItem()
    {
        return item;
    }

    /// <returns>the itemObjId</returns>
    public int GetItemObjId()
    {
        return itemObjId;
    }

    /// <returns>the itemCount</returns>
    public long GetItemCount()
    {
        return itemCount;
    }
}
