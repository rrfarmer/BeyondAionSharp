using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Trade;

/// <summary>Java parity: model/trade/ExchangeItem (ATracer). Used when exchange item != original item.</summary>
public class ExchangeItem
{
    private int itemObjId;
    private long itemCount;
    private Item item;

    public ExchangeItem(int itemObjId, long itemCount, Item item)
    {
        this.itemObjId = itemObjId;
        this.itemCount = itemCount;
        this.item = item;
    }

    public void SetItem(Item item)
    {
        this.item = item;
    }

    public void AddCount(long countToAdd)
    {
        this.itemCount += countToAdd;
        this.item.SetItemCount(itemCount);
    }

    public Item GetItem()
    {
        return item;
    }

    public int GetItemObjId()
    {
        return itemObjId;
    }

    public long GetItemCount()
    {
        return itemCount;
    }
}
