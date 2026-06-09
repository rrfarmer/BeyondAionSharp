using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Rift;

/// <summary>Java parity: model/templates/rift/OpenRift (Source).</summary>
[XmlType("OpenRift")]
public class OpenRift
{
    [XmlAttribute("schedule")] protected string schedule;
    [XmlAttribute("spawn")] protected bool guards;

    public string GetSchedule()
    {
        return schedule;
    }

    public bool SpawnGuards()
    {
        return guards;
    }
}
