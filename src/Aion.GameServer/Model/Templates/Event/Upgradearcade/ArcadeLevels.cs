using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Event.Upgradearcade;

/// <summary>Java parity: model/templates/event/upgradearcade/ArcadeLevels (Neon).</summary>
public class ArcadeLevels
{
    [XmlAttribute("min_resumable_level")] private int minResumableLevel;
    [XmlElement("level")] private List<ArcadeLevel> upgradeLevels;

    public int GetMinResumableLevel()
    {
        return minResumableLevel;
    }

    public List<ArcadeLevel> GetLevels()
    {
        return upgradeLevels;
    }

    public ArcadeLevel GetMaxUpgradeLevel()
    {
        return upgradeLevels[upgradeLevels.Count - 1];
    }
}
