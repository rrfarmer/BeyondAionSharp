using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.World;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/WorldMapsData (Luno). @XmlRootElement(world_maps); Iterable→IEnumerable; LinkedHashMap→Dictionary; afterUnmarshal→AfterUnmarshal(object).</summary>
public class WorldMapsData : IEnumerable<WorldMapTemplate>
{
    // NOTE: this holder implements IEnumerable, which XmlSerializer treats as a collection type — it cannot
    // be deserialized directly from <world_maps> with a [XmlElement("map")] field. Instead the source XML is
    // read into WorldMapsDataDto (a plain [XmlRoot("world_maps")] DTO) and transferred via SetData, mirroring
    // JAXB's afterUnmarshal which builds the mapsById index from the unmarshalled <map> list.
    [XmlIgnore] private readonly Dictionary<int, WorldMapTemplate> mapsById = new();

    /// <summary>Java parity: afterUnmarshal — index every <map> by its id (1:1 with the JAXB unmarshalled list).</summary>
    public void SetData(List<WorldMapTemplate> maps)
    {
        mapsById.Clear();
        if (maps == null)
            return;
        foreach (WorldMapTemplate map in maps)
            mapsById[map.GetMapId()] = map;
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

/// <summary>
/// Plain XML DTO for <c>world_maps.xml</c>. WorldMapsData implements IEnumerable (XmlSerializer treats such
/// types as collections and ignores element-bound fields), so the source XML is deserialized into this DTO and
/// its <c>Maps</c> list handed to <see cref="WorldMapsData.SetData"/>.
/// </summary>
[XmlRoot("world_maps")]
public sealed class WorldMapsDataDto
{
    [XmlElement("map")] public List<WorldMapTemplate> Maps { get; set; } = new();
}
