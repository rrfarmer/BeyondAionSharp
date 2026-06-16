using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/GatherGroup.</summary>
[XmlType("GatherGroup")]
public class GatherGroup : BonusItemGroup
{
    [XmlElement("item")] public List<ItemRaceEntry> items;

    public override IReadOnlyList<ItemRaceEntry> GetItems()
    {
        if (items == null)
            return new List<ItemRaceEntry>();
        return items;
    }
}
