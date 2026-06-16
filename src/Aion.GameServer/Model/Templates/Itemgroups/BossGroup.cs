using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/BossGroup.</summary>
[XmlType("BossGroup")]
public class BossGroup : BonusItemGroup
{
    [XmlElement("item")] public List<ItemRaceEntry> items;

    public override IReadOnlyList<ItemRaceEntry> GetItems()
    {
        if (items == null)
            return new List<ItemRaceEntry>();
        return items;
    }
}
