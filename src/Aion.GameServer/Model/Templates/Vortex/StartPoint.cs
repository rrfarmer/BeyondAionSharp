using System.Xml.Serialization;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Templates.Vortex;

/// <summary>Java parity: model/templates/vortex/StartPoint (Source).</summary>
[XmlType("StartPoint")]
public class StartPoint
{
    [XmlAttribute("map")] public int map;
    [XmlAttribute("x")] public float x;
    [XmlAttribute("y")] public float y;
    [XmlAttribute("z")] public float z;
    [XmlAttribute("h")] public byte h;

    public int GetWorldId()
    {
        return map;
    }

    public WorldPosition GetStartPoint()
    {
        WorldPosition start = new WorldPosition(map);
        start.SetXYZH(x, y, z, h);
        return start;
    }
}
