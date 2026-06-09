using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Item;

/// <summary>Java parity: model/templates/item/DecomposableItemInfo (antness).</summary>
[XmlType("DecomposableItem")]
public class DecomposableItemInfo
{
    [XmlAttribute("item_id")] private int itemId;
    [XmlAttribute("selectable")] private bool isSelectable = false;
    [XmlElement("items")] private List<ExtractedItemsCollection> itemsCollections;

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
