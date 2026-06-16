using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Worldraid;

/// <summary>Java parity: model/templates/worldraid/WorldRaidLocation (Alcapwnd, Whoop, Sykra).</summary>
[XmlType("WorldRaidLocation")]
public class WorldRaidLocation
{
    [XmlArray("world_raid_npcs")]
    [XmlArrayItem("world_raid_npc")]
    public List<WorldRaidNpc> npcPool;

    [XmlArray("location_markers")]
    [XmlArrayItem("spot")]
    public List<MarkerSpot> locationMarkers;

    [XmlAttribute("location_id")] public int locationId;

    [XmlAttribute("map_id")] public int mapId;

    [XmlAttribute("x")] public float x;

    [XmlAttribute("y")] public float y;

    [XmlAttribute("z")] public float z;

    [XmlAttribute("h")] public byte h = 0;

    public int GetLocationId()
    {
        return locationId;
    }

    public int GetMapId()
    {
        return mapId;
    }

    public float GetX()
    {
        return x;
    }

    public float GetY()
    {
        return y;
    }

    public float GetZ()
    {
        return z;
    }

    public byte GetH()
    {
        return h;
    }

    public List<WorldRaidNpc> GetNpcPool()
    {
        return npcPool;
    }

    public List<MarkerSpot> GetLocationMarkers()
    {
        return locationMarkers;
    }
}
