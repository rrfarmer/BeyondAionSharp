using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Stats;

/// <summary>
/// Kisk (resurrection totem) parameters.
/// Java parity: model/templates/stats/KiskStatsTemplate (@XmlRootElement("kisk_stats")).
/// </summary>
[XmlRoot("kisk_stats")]
public class KiskStatsTemplate
{
    [XmlAttribute("usemask")] public int UseMask { get; set; } = 4;
    [XmlAttribute("members")] public int MaxMembers { get; set; } = 6;
    [XmlAttribute("resurrects")] public int MaxResurrects { get; set; } = 18;

    public int GetUseMask() => UseMask;
    public int GetMaxMembers() => MaxMembers;
    public int GetMaxResurrects() => MaxResurrects;
}
