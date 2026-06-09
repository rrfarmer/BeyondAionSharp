using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/DoorRepairStone.</summary>
[XmlType("DoorRepairStone")]
public class DoorRepairStone
{
    [XmlAttribute("static_id")] protected int staticId;
    [XmlAttribute("door_id")] protected int doorId;

    public int GetStaticId()
    {
        return staticId;
    }

    public int GetDoorId()
    {
        return doorId;
    }
}
