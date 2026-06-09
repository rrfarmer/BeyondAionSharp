using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.World;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/WorldMapsData (Luno). @XmlRootElement(world_maps); Iterable→IEnumerable; LinkedHashMap→Dictionary; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("world_maps")]
public class WorldMapsData : IEnumerable<WorldMapTemplate>
{
    [XmlElement("map")] private List<WorldMapTemplate> worldMaps;

    [XmlIgnore] private readonly Dictionary<int, WorldMapTemplate> mapsById = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (WorldMapTemplate map in worldMaps)
        {
            mapsById[map.GetMapId()] = map;
        }
        worldMaps = null;
    }

    public IEnumerator<WorldMapTemplate> GetEnumerator()
    {
        return mapsById.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void ForEachParalllel(Action<WorldMapTemplate> consumer)
    {
        mapsById.Values.AsParallel().ForAll(consumer);
    }

    public int Size()
    {
        return mapsById.Count;
    }

    public WorldMapTemplate GetTemplate(int worldId)
    {
        return mapsById.TryGetValue(worldId, out var v) ? v : null;
    }

    public int GetWorldIdByCName(string name)
    {
        foreach (WorldMapTemplate template in mapsById.Values)
        {
            if (string.Equals(template.GetCName(), name, StringComparison.OrdinalIgnoreCase))
            {
                return template.GetMapId();
            }
        }
        return 0;
    }
}
