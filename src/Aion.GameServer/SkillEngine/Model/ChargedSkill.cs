using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>One charged-skill entry (id + charge time). Java parity: skillengine/model/ChargedSkill (@XmlType("ChargedSkill")).</summary>
[XmlType("ChargedSkill")]
public class ChargedSkill
{
    [XmlAttribute("id")] public int Id { get; set; }
    [XmlAttribute("time")] public int Time { get; set; }

    public int GetTime() => Time;
    public int GetId() => Id;
}
