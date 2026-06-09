using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Event.Upgradearcade;

/// <summary>Java parity: model/templates/event/upgradearcade/ArcadeLevel (Neon).</summary>
public class ArcadeLevel
{
    [XmlAttribute("level")] private int level;
    [XmlAttribute("icon")] private string icon;
    [XmlAttribute("upgrade_chance")] private float upgradeChance;

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
