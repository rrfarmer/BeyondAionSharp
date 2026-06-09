using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Worldraid;

/// <summary>Java parity: model/templates/worldraid/WorldRaidLocation (Alcapwnd, Whoop, Sykra).</summary>
[XmlType("WorldRaidLocation")]
public class WorldRaidLocation
{
    [XmlArray("world_raid_npcs")]
    [XmlArrayItem("world_raid_npc")]
    private List<WorldRaidNpc> npcPool;

    [XmlArray("location_markers")]
    [XmlArrayItem("spot")]
    private List<MarkerSpot> locationMarkers;

    [XmlAttribute("location_id")] private int locationId;

    [XmlAttribute("map_id")] private int mapId;

    [XmlAttribute("x")] private float x;

    [XmlAttribute("y")] private float y;

    [XmlAttribute("z")] private float z;

    [XmlAttribute("h")] private byte h = 0;

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
