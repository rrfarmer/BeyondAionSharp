using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/OreGroup.</summary>
[XmlType("OreGroup")]
public class OreGroup : BonusItemGroup
{
    [XmlElement("item")] private List<ItemRaceEntry> items;

    public override IReadOnlyList<ItemRaceEntry> GetItems()
    {
        if (items == null)
            return new List<ItemRaceEntry>();
        return items;
    }
}
