using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Worldraid;

/// <summary>Java parity: model/templates/worldraid/MarkerSpot (Sykra).</summary>
[XmlType("MarkerSpot")]
public class MarkerSpot
{
    [XmlAttribute("x")] private float x;
    [XmlAttribute("y")] private float y;
    [XmlAttribute("z")] private float z;
    [XmlAttribute("h")] private byte h = 0;

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

    public override string ToString()
    {
        return "MarkerSpot[" + "x=" + x + ", y=" + y + ", z=" + z + ", h=" + h + ']';
    }
}
