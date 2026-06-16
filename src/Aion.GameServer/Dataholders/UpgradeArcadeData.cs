using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Event.Upgradearcade;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/UpgradeArcadeData (ginho1). @XmlRootElement(arcadelist).</summary>
[XmlRoot("arcadelist")]
public class UpgradeArcadeData
{
    [XmlElement("levels")] public ArcadeLevels levels;
    [XmlElement("rewards")] public List<ArcadeRewards> rewards;

    public int Size()
    {
        return rewards.Count;
    }

    public int GetMinResumableLevel()
    {
        return levels.GetMinResumableLevel();
    }

    public List<ArcadeLevel> GetUpgradeLevels()
    {
        return levels.GetLevels();
    }

    public ArcadeLevel GetMaxUpgradeLevel()
    {
        return levels.GetMaxUpgradeLevel();
    }

    public List<ArcadeRewards> GetRewards()
    {
        return rewards;
    }
}
