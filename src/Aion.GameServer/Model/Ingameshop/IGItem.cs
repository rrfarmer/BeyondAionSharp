namespace Aion.GameServer.Model.Ingameshop;

/// <summary>Java parity: model/ingameshop/IGItem (xTz). Java signed byte→sbyte. Plain holder.</summary>
public class IGItem
{
    private int objectId;
    private int itemId;
    private long itemCount;
    private long itemPrice;
    private sbyte category;
    private sbyte subCategory;
    private int list;
    private int salesRanking;
    private sbyte itemType;
    private sbyte gift;
    private string titleDescription;
    private string itemDescription;

    public IGItem(int objectId, int itemId, long itemCount, long itemPrice, sbyte category, sbyte subCategory, int list, int salesRanking, sbyte itemType,
        sbyte gift, string titleDescription, string itemDescription)
    {
        this.objectId = objectId;
        this.itemId = itemId;
        this.itemCount = itemCount;
        this.itemPrice = itemPrice;
        this.category = category;
        this.subCategory = subCategory;
        this.list = list;
        this.salesRanking = salesRanking;
        this.itemType = itemType;
        this.gift = gift;
        this.titleDescription = titleDescription;
        this.itemDescription = itemDescription;
    }

    public int GetObjectId()
    {
        return objectId;
    }

    public int GetItemId()
    {
        return itemId;
    }

    public long GetItemCount()
    {
        return itemCount;
    }

    public long GetItemPrice()
    {
        return itemPrice;
    }

    public sbyte GetCategory()
    {
        return category;
    }

    public sbyte GetSubCategory()
    {
        return subCategory;
    }

    public int GetList()
    {
        return list;
    }

    public int GetSalesRanking()
    {
        return salesRanking;
    }

    public sbyte GetItemType()
    {
        return itemType;
    }

    public sbyte GetGift()
    {
        return gift;
    }

    public string GetItemDescription()
    {
        return itemDescription;
    }

    public string GetTitleDescription()
    {
        return titleDescription;
    }

    public void IncreaseSales()
    {
        salesRanking++;
    }
}
