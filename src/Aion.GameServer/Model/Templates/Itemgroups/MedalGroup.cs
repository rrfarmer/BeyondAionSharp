using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Rewards;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/MedalGroup.</summary>
[XmlType("MedalGroup")]
public class MedalGroup : BonusItemGroup
{
    [XmlElement("item")] public List<FullRewardItem> items;

    public override IReadOnlyList<ItemRaceEntry> GetItems()
    {
        if (items == null)
            return new List<FullRewardItem>();
        return items;
    }
}
