using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>Java parity: model/templates/quest/InventoryItems (Rolandas).</summary>
[XmlType("InventoryItems")]
public class InventoryItems
{
    [XmlElement("inventory_item")] protected List<InventoryItem> inventoryItems;

    /// <summary>Gets the value of the inventoryItems property.</summary>
    public List<InventoryItem> GetInventoryItems()
    {
        if (inventoryItems == null)
        {
            return new List<InventoryItem>();
        }
        return inventoryItems;
    }
}
