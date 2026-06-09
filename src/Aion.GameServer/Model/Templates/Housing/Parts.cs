using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>
/// Java parity: model/templates/housing/Parts (Rolandas).
/// propOrder = fence, garden, frame, outwall, roof, infloor, inwall, door (matches field declaration order).
/// </summary>
[XmlType("Parts")]
public class Parts
{
    [XmlElement("fence")] protected int? fence;
    [XmlElement("garden")] protected int? garden;
    [XmlElement("frame")] protected int? frame;
    [XmlElement("outwall")] protected int? outwall;
    [XmlElement("roof")] protected int? roof;
    [XmlElement("infloor")] protected int infloor;
    [XmlElement("inwall")] protected int inwall;
    [XmlElement("door")] protected int door;

    public int? GetFence()
    {
        return fence;
    }

    public int? GetGarden()
    {
        return garden;
    }

    public int? GetFrame()
    {
        return frame;
    }

    public int? GetOutwall()
    {
        return outwall;
    }

    public int? GetRoof()
    {
        return roof;
    }

    public int GetInfloor()
    {
        return infloor;
    }

    public int GetInwall()
    {
        return inwall;
    }

    public int GetDoor()
    {
        return door;
    }
}
