using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Housing;

/// <summary>Java parity: model/templates/housing/BuildingCapabilities (Rolandas).</summary>
[XmlRoot("caps")]
public class BuildingCapabilities
{
    [XmlAttribute("addon")] protected bool addon;

    [XmlAttribute("emblemId")] protected int emblemId;

    [XmlAttribute("floor")] protected bool floor;

    [XmlAttribute("room")] protected bool room;

    [XmlAttribute("interior")] protected int interior;

    [XmlAttribute("exterior")] protected int exterior;

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
