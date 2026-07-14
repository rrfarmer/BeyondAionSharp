using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/DoorRepairStone.</summary>
[XmlType("DoorRepairStone")]
public class DoorRepairStone
{
    // Java protected members are package-visible, so sibling DoorRepairData can index this field directly.
    [XmlIgnore] internal int staticId;

    // Java JAXB binds the protected field directly. XmlSerializer binds only public members, so retain the
    // package-visible backing field used by DoorRepairData and expose a public XML proxy for static_id.
    [XmlAttribute("static_id")]
    public int StaticIdXml
    {
        get => staticId;
        set => staticId = value;
    }
    [XmlAttribute("door_id")] public int doorId;

    public int GetStaticId()
    {
        return staticId;
    }

    public int GetDoorId()
    {
        return doorId;
    }
}
