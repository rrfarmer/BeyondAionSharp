using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/FoodGroup.</summary>
[XmlType("FoodGroup")]
public class FoodGroup : BonusItemGroup
{
    [XmlElement("item")] public List<FoodItem> items;

    public override IReadOnlyList<ItemRaceEntry> GetItems()
    {
        if (items == null)
            return new List<FoodItem>();
        return items;
    }
}
