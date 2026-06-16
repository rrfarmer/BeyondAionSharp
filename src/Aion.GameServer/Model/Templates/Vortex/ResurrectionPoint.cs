using System.Xml.Serialization;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Templates.Vortex;

/// <summary>Java parity: model/templates/vortex/ResurrectionPoint (Source).</summary>
[XmlType("ResurrectionPoint")]
public class ResurrectionPoint
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

    public WorldPosition GetResurrectionPoint()
    {
        WorldPosition home = new WorldPosition(map);
        home.SetXYZH(x, y, z, h);
        return home;
    }
}
