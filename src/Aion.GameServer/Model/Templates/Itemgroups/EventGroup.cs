using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/EventGroup.</summary>
[XmlType("EventGroup")]
public class EventGroup : BonusItemGroup
{
    [XmlElement("item")] private List<FullRewardItem> items;

    public override IReadOnlyList<ItemRaceEntry> GetItems()
    {
        if (items == null)
            return new List<FullRewardItem>();
        return items;
    }
}
