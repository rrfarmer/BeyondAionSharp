using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/InventoryItem (Rolandas).</summary>
[XmlType("InventoryItem")]
public class InventoryItem
{
    [XmlAttribute("item_id")] protected int? itemId;
    [XmlAttribute("count")] private int? count;

    /// <summary>Gets the value of the itemId property.</summary>
    public int? GetItemId()
    {
        return itemId;
    }

    /// <returns>the count, or null if scripts should handle that the counts probably may depend on scenario</returns>
    public int? GetCount()
    {
        return count;
    }
}
