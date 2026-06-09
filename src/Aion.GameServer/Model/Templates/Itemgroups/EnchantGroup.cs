using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/EnchantGroup.</summary>
[XmlType("EnchantGroup")]
public class EnchantGroup : BonusItemGroup
{
    [XmlElement("item")] private List<IdLevelReward> items;

    public override IReadOnlyList<ItemRaceEntry> GetItems()
    {
        if (items == null)
            return new List<IdLevelReward>();
        return items;
    }
}
