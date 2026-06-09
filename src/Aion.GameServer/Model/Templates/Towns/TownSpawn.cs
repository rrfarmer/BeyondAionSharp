using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Towns;

/// <summary>Java parity: model/templates/towns/TownSpawn (ViAl).</summary>
[XmlType("town_spawn")]
public class TownSpawn
{
    [XmlAttribute("town_id")] private int townId;
    [XmlElement("town_level")] private List<TownLevel> townLevels;

    [XmlIgnore] private readonly Dictionary<int, TownLevel> townLevelsData = new Dictionary<int, TownLevel>();

    // Java parity: afterUnmarshal(Unmarshaller, Object parent).
    public void AfterUnmarshal(object parent)
    {
        townLevelsData.Clear();
        foreach (TownLevel level in townLevels)
        {
            townLevelsData[level.GetLevel()] = level;
        }
        townLevels = null;
    }

    public int GetTownId()
    {
        return townId;
    }

    public TownLevel GetSpawnsForLevel(int level)
    {
        return townLevelsData.TryGetValue(level, out var v) ? v : null;
    }

    public ICollection<TownLevel> GetTownLevels()
    {
        return townLevelsData.Values;
    }
}
