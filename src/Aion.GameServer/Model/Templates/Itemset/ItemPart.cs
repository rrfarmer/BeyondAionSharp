using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Itemset;

/// <summary>Java parity: model/templates/itemset/ItemPart (ATracer).</summary>
[XmlRoot("ItemPart")]
public class ItemPart
{
    [XmlAttribute("itemid")] protected int itemid;

    /// <returns>the itemid</returns>
    public int GetItemId()
    {
        return itemid;
    }
}
