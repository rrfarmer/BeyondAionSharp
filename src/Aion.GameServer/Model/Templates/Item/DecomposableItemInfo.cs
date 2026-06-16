using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>Java parity: model/templates/item/DecomposableItemInfo (antness).</summary>
[XmlType("DecomposableItem")]
public class DecomposableItemInfo
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields).
    [XmlAttribute("item_id")] public int itemId;
    [XmlAttribute("selectable")] public bool isSelectable = false;
    [XmlElement("items")] public List<ExtractedItemsCollection> itemsCollections;

    public int GetItemId()
    {
        return itemId;
    }

    public bool IsIsSelectable()
    {
        return isSelectable;
    }

    public List<ExtractedItemsCollection> GetItemsCollections()
    {
        return itemsCollections;
    }
}
