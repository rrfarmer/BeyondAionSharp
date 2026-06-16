using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Event.Upgradearcade;

/// <summary>Java parity: model/templates/event/upgradearcade/ArcadeRewards (ginho1).</summary>
[XmlType("ArcadeRewards")]
public class ArcadeRewards
{
    [XmlAttribute("min_level")] public int minLevel;
    [XmlElement("item")] public List<ArcadeRewardItem> arcadeRewardItems;

    public int GetMinLevel()
    {
        return minLevel;
    }

    public List<ArcadeRewardItem> GetArcadeRewardItems()
    {
        return arcadeRewardItems;
    }
}
