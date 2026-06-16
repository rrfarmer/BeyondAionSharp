using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Portal;

/// <summary>Java parity: model/templates/portal/PortalLoc (xTz).</summary>
[XmlType("PortalLoc")]
public class PortalLoc
{
    [XmlAttribute("world_id")] public int worldId;
    [XmlAttribute("loc_id")] public int locId;
    [XmlAttribute("x")] public float x;
    [XmlAttribute("y")] public float y;
    [XmlAttribute("z")] public float z;
    [XmlAttribute("h")] public sbyte h;

    public int GetWorldId()
    {
        return worldId;
    }

    public void SetWorldId(int value)
    {
        this.worldId = value;
    }

    public int GetLocId()
    {
        return locId;
    }

    public void SetLocId(int value)
    {
        this.locId = value;
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

    public sbyte GetH()
    {
        return h;
    }

    public void SetH(sbyte value)
    {
        this.h = value;
    }
}
