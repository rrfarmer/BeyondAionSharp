using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>Java parity: model/templates/spawns/HouseSpawns (Rolandas).</summary>
[XmlRoot("house")]
public class HouseSpawns
{
    [XmlElement("spawn")] private List<HouseSpawn> spawns;

    [XmlAttribute("address")] private int address;

    public List<HouseSpawn> GetSpawns()
    {
        return spawns == null ? new List<HouseSpawn>() : spawns;
    }

    public int GetAddress()
    {
        return address;
    }
}
