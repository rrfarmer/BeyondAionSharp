using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/BuildingCapabilities (Rolandas).</summary>
[XmlRoot("caps")]
public class BuildingCapabilities
{
    [XmlAttribute("addon")] public bool addon;

    [XmlAttribute("emblemId")] public int emblemId;

    [XmlAttribute("floor")] public bool floor;

    [XmlAttribute("room")] public bool room;

    [XmlAttribute("interior")] public int interior;

    [XmlAttribute("exterior")] public int exterior;

    public bool CanHaveAddon()
    {
        return addon;
    }

    public int GetEmblemId()
    {
        return emblemId;
    }

    public bool CanChangeFloor()
    {
        return floor;
    }

    public bool CanChangeRoom()
    {
        return room;
    }

    public int CanChangeInterior()
    {
        return interior;
    }

    public int CanChangeExterior()
    {
        return exterior;
    }
}
