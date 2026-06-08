using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>A charge-skill definition (min time + the charged skills). Java parity: skillengine/model/ChargeSkillEntry (@XmlType("ChargeSkill")).</summary>
[XmlType("ChargeSkill")]
public class ChargeSkillEntry
{
    [XmlElement("skill")] public List<ChargedSkill>? Skills { get; set; }
    [XmlAttribute("id")] public int Id { get; set; }
    [XmlAttribute("min_time")] public int MinTime { get; set; }

    public List<ChargedSkill>? GetSkills() => Skills;
    public int GetMinTime() => MinTime;
    public int GetId() => Id;
}
