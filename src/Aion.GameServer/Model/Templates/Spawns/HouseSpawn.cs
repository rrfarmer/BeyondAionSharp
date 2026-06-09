using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Spawns;

/// <summary>Java parity: model/templates/spawns/HouseSpawn (Rolandas).</summary>
[XmlType("HouseSpawn")]
public class HouseSpawn
{
    [XmlAttribute("x")] protected float x;

    [XmlAttribute("y")] protected float y;

    [XmlAttribute("z")] protected float z;

    [XmlAttribute("h")] protected byte h;

    [XmlAttribute("type")] protected SpawnType type;

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
