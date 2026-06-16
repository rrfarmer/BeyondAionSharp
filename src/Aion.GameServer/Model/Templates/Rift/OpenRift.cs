using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Rift;

/// <summary>Java parity: model/templates/rift/OpenRift (Source).</summary>
[XmlType("OpenRift")]
public class OpenRift
{
    // XmlSerializer binds PUBLIC members only (Java JAXB used @XmlAccessorType(FIELD)); keep public for binding.
    [XmlAttribute("schedule")] public string schedule;
    [XmlAttribute("spawn")] public bool guards;

    public string GetSchedule()
    {
        return schedule;
    }

    public bool SpawnGuards()
    {
        return guards;
    }
}
