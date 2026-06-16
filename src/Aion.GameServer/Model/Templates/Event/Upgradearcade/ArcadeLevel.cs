using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Event.Upgradearcade;

/// <summary>Java parity: model/templates/event/upgradearcade/ArcadeLevel (Neon).</summary>
public class ArcadeLevel
{
    [XmlAttribute("level")] public int level;
    [XmlAttribute("icon")] public string icon;
    [XmlAttribute("upgrade_chance")] public float upgradeChance;

    public int GetLevel()
    {
        return level;
    }

    public string GetIcon()
    {
        return icon;
    }

    public float GetUpgradeChance()
    {
        return upgradeChance;
    }
}
