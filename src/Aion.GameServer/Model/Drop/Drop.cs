using System.Xml.Serialization;

namespace Aion.GameServer.Model.Drop;

/// <summary>Java parity: model/drop/Drop (MrPoke).</summary>
[XmlRoot("drop")]
public class Drop
{
    [XmlAttribute("item_id")] public int itemId;
    [XmlAttribute("min_amount")] public int minAmount = 1;
    [XmlAttribute("max_amount")] public int maxAmount;
    [XmlAttribute("chance")] public float chance = 100;
    [XmlAttribute("each_member")] public bool eachMember = false;

    /// <summary>Parameterless constructor for deserialization (XmlSerializer requires a public one).</summary>
    public Drop()
    {
    }

    public Drop(int itemId, int minAmount, int maxAmount, float chance)
    {
        this.itemId = itemId;
        this.minAmount = minAmount;
        this.maxAmount = maxAmount;
        this.chance = chance;
        AfterUnmarshal();
    }

    // Java parity: afterUnmarshal — invoked post-load by the drop loader.
    public void AfterUnmarshal()
    {
        if (chance <= 0)
            throw new System.ArgumentException("chance (" + chance + ") for drop " + itemId + " must be greater than zero");
        if (minAmount <= 0)
            throw new System.ArgumentException("minAmount (" + minAmount + ") for drop " + itemId + " must be greater than zero");
        if (maxAmount == 0)
            maxAmount = minAmount;
        else if (maxAmount < minAmount)
            throw new System.ArgumentException("maxAmount (" + maxAmount + ") for drop " + itemId + " must be greater than minAmount (" + minAmount + ")");
    }

    public int GetItemId()
    {
        return itemId;
    }

    public int GetMinAmount()
    {
        return minAmount;
    }

    public int GetMaxAmount()
    {
        return maxAmount;
    }

    public float GetChance()
    {
        return chance;
    }

    public bool IsEachMember()
    {
        return eachMember;
    }

    public override string ToString()
    {
        return "Drop [itemId=" + itemId + ", minAmount=" + minAmount + ", maxAmount=" + maxAmount + ", chance=" + chance + ", eachMember=" + eachMember + "]";
    }
}
