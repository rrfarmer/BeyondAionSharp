using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Shield;

/// <summary>Java parity: model/templates/shield/ShieldPoint (M@xx, Wakizashi).</summary>
[XmlType("ShieldPoint")]
public class ShieldPoint
{
    [XmlAttribute("x")] public float x;

    [XmlAttribute("y")] public float y;

    [XmlAttribute("z")] public float z;

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
}
