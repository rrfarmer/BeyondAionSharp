using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/DoorRepairStone.</summary>
[XmlType("DoorRepairStone")]
public class DoorRepairStone
{
    // Java parity: package-private field accessed by sibling DoorRepairData → internal (closest C# analog).
    [XmlAttribute("static_id")] internal int staticId;
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
