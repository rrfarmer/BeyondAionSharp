using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/FeedItemGroup.</summary>
[XmlType("FeedItemGroup")]
public abstract class FeedItemGroup
{
    [XmlAttribute("group")] protected ItemGroupIndex index = ItemGroupIndex.NONE;
    [XmlElement("item")] private List<ItemRaceEntry> items;

    public ItemGroupIndex GetIndex()
    {
        return index;
    }

    public List<ItemRaceEntry> GetItems()
    {
        if (items == null)
        {
            items = new List<ItemRaceEntry>();
        }
        return this.items;
    }
}
