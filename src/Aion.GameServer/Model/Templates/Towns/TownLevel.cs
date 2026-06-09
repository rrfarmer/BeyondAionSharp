using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Spawns;

namespace Aion.GameServer.Model.Templates.Towns;

/// <summary>Java parity: model/templates/towns/TownLevel (ViAl).</summary>
[XmlType("town_level")]
public class TownLevel
{
    [XmlAttribute("level")] protected int level;
    [XmlElement("spawn")] protected List<Spawn> spawns;

    /// <returns>the level</returns>
    public int GetLevel()
    {
        return level;
    }

    /// <returns>the spawn</returns>
    public List<Spawn> GetSpawns()
    {
        return spawns;
    }
}
