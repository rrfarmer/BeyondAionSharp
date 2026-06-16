using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Curingzones;

/// <summary>Java parity: model/templates/curingzones/CuringTemplate (xTz).</summary>
[XmlType("CuringTemplate")]
public class CuringTemplate
{
    // Public so XmlSerializer can bind these attributes (JAXB used private fields via @XmlAccessorType(FIELD)).
    [XmlAttribute("map_id")] public int mapId;
    [XmlAttribute("x")] public float x;
    [XmlAttribute("y")] public float y;
    [XmlAttribute("z")] public float z;
    [XmlAttribute("range")] public float range;

    public int GetMapId()
    {
        return mapId;
    }

    public void SetMapId(int value)
    {
        this.mapId = value;
    }

    public float GetX()
    {
        return x;
    }

    public void SetX(float value)
    {
        this.x = value;
    }

    public float GetY()
    {
        return y;
    }

    public void SetY(float value)
    {
        this.y = value;
    }

    public float GetZ()
    {
        return z;
    }

    public void SetZ(float value)
    {
        this.z = value;
    }

    public float GetRange()
    {
        return range;
    }
}
