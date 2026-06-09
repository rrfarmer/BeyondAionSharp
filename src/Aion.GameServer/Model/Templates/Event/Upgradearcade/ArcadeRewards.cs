using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Event.Upgradearcade;

/// <summary>Java parity: model/templates/event/upgradearcade/ArcadeRewards (ginho1).</summary>
[XmlType("ArcadeRewards")]
public class ArcadeRewards
{
    [XmlAttribute("min_level")] private int minLevel;
    [XmlElement("item")] private List<ArcadeRewardItem> arcadeRewardItems;

    public int GetMinLevel()
    {
        return minLevel;
    }

    public List<ArcadeRewardItem> GetArcadeRewardItems()
    {
        return arcadeRewardItems;
    }
}
