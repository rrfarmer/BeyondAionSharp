using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/MedicineGroup.</summary>
[XmlType("MedicineGroup")]
public class MedicineGroup : BonusItemGroup
{
    [XmlElement("item")] private List<MedicineItem> items;

    public override IReadOnlyList<ItemRaceEntry> GetItems()
    {
        if (items == null)
            return new List<MedicineItem>();
        return items;
    }
}
