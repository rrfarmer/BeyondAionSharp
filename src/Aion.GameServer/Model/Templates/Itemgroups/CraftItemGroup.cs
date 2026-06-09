using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/CraftItemGroup.</summary>
[XmlType("CraftItemGroup")]
public class CraftItemGroup : BonusItemGroup
{
    [XmlElement("item")] private List<CraftItem> items;

    public override IReadOnlyList<ItemRaceEntry> GetItems()
    {
        if (items == null)
            return new List<CraftItem>();
        return items;
    }
}
