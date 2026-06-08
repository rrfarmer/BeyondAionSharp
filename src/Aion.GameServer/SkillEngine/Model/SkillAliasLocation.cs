using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Named alias location (world + alias positions). Java parity: skillengine/model/SkillAliasLocation (@XmlType("alias_location")).</summary>
[XmlType("alias_location")]
public class SkillAliasLocation
{
    [XmlElement("alias_pos")] public List<SkillAliasPosition>? SkillAliasPositionList { get; set; }
    [XmlAttribute("name")] public string AliasName { get; set; } = string.Empty;
    [XmlAttribute("world_id")] public int WorldId { get; set; }

    public List<SkillAliasPosition>? GetSkillAliasPositionList() => SkillAliasPositionList;
    public string GetAliasName() => AliasName;
    public int GetWorldId() => WorldId;
}
