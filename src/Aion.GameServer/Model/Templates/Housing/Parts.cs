using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>
/// Java parity: model/templates/housing/Parts (Rolandas).
/// propOrder = fence, garden, frame, outwall, roof, infloor, inwall, door (matches field declaration order).
/// </summary>
[XmlType("Parts")]
public class Parts
{
    [XmlElement("fence")] public int? fence;
    [XmlElement("garden")] public int? garden;
    [XmlElement("frame")] public int? frame;
    [XmlElement("outwall")] public int? outwall;
    [XmlElement("roof")] public int? roof;
    [XmlElement("infloor")] public int infloor;
    [XmlElement("inwall")] public int inwall;
    [XmlElement("door")] public int door;

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
