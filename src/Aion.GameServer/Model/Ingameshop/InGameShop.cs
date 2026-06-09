namespace Aion.GameServer.Model.Ingameshop;

/// <summary>Java parity: model/ingameshop/InGameShop. Java byte → sbyte.</summary>
public class InGameShop
{
    private sbyte subCategory;
    private sbyte category = 2;

    public sbyte GetSubCategory()
    {
        return subCategory;
    }

    public void SetSubCategory(sbyte subCategory)
    {
        this.subCategory = subCategory;
    }

    public sbyte GetCategory()
    {
        return category;
    }

    public void SetCategory(sbyte category)
    {
        this.category = category;
    }
}
