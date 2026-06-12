using System.Xml.Serialization;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.Templates.Vortex;

/// <summary>Java parity: model/templates/vortex/HomePoint (Source).</summary>
[XmlType("HomePoint")]
public class HomePoint
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

    public WorldPosition GetHomePoint()
    {
        WorldPosition home = new WorldPosition(map);
        home.SetXYZH(x, y, z, h);
        return home;
    }
}
