using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Towns;

/// <summary>Java parity: model/templates/towns/TownSpawnMap (ViAl).</summary>
[XmlType("town_spawn_map")]
public class TownSpawnMap
{
    [XmlAttribute("map_id")] private int mapId;
    [XmlElement("town_spawn")] private List<TownSpawn> townSpawns;

    [XmlIgnore] private readonly Dictionary<int, TownSpawn> townSpawnsData = new Dictionary<int, TownSpawn>();

    // Java parity: afterUnmarshal(Unmarshaller, Object parent).
    public void AfterUnmarshal(object parent)
    {
        townSpawnsData.Clear();
        foreach (TownSpawn town in townSpawns)
        {
            townSpawnsData[town.GetTownId()] = town;
        }
        townSpawns = null;
    }

    public int GetMapId()
    {
        return mapId;
    }

    public TownSpawn GetTownSpawn(int townId)
    {
        return townSpawnsData.TryGetValue(townId, out var v) ? v : null;
    }

    public ICollection<TownSpawn> GetTownSpawns()
    {
        return townSpawnsData.Values;
    }
}
