using System.Xml.Serialization;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Templates.Vortex;

/// <summary>Java parity: model/templates/vortex/StartPoint (Source).</summary>
[XmlType("StartPoint")]
public class StartPoint
{
    [XmlAttribute("map")] protected int map;
    [XmlAttribute("x")] protected float x;
    [XmlAttribute("y")] protected float y;
    [XmlAttribute("z")] protected float z;
    [XmlAttribute("h")] protected byte h;

    public int GetWorldId()
    {
        return map;
    }

    public WorldPosition GetStartPoint()
    {
        WorldPosition start = new WorldPosition(map);
        start.SetXyzh(x, y, z, h);
        return start;
    }
}
