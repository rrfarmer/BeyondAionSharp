using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Worldraid;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/WorldRaidData (Alcapwnd, Whoop, Sykra). @XmlRootElement(world_raid_locations); putIfAbsent→TryAdd; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("world_raid_locations")]
public class WorldRaidData
{
    [XmlElement("world_raid_location")] private List<WorldRaidLocation> worldRaidLocations;

    [XmlIgnore] private Dictionary<int, WorldRaidLocation> locationsById = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (WorldRaidLocation location in worldRaidLocations)
            locationsById.TryAdd(location.GetLocationId(), location);
        worldRaidLocations.Clear();
        worldRaidLocations = null;
    }

    public WorldRaidLocation GetLocationsById(int locationId)
    {
        return locationsById.TryGetValue(locationId, out var v) ? v : null;
    }

    public Dictionary<int, WorldRaidLocation> GetLocations()
    {
        return locationsById;
    }

    public int Size()
    {
        return locationsById.Count;
    }
}
