using System.Xml.Serialization;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Templates.Vortex;

/// <summary>Java parity: model/templates/vortex/ResurrectionPoint (Source).</summary>
[XmlType("ResurrectionPoint")]
public class ResurrectionPoint
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

    public WorldPosition GetResurrectionPoint()
    {
        WorldPosition home = new WorldPosition(map);
        home.SetXyzh(x, y, z, h);
        return home;
    }
}
