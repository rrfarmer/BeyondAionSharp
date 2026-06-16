using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>Java parity: model/templates/spawns/HouseSpawn (Rolandas).</summary>
[XmlType("HouseSpawn")]
public class HouseSpawn
{
    [XmlAttribute("x")] public float x;

    [XmlAttribute("y")] public float y;

    [XmlAttribute("z")] public float z;

    [XmlAttribute("h")] public byte h;

    [XmlAttribute("type")] public SpawnType type;

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

    public SpawnType GetType_()
    {
        return type;
    }
}
