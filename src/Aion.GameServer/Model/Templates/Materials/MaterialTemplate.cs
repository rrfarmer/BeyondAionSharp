using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Materials;

/// <summary>Java parity: model/templates/materials/MaterialTemplate (Rolandas).</summary>
[XmlType("MaterialTemplate")]
public class MaterialTemplate
{
    [XmlElement("skill")] public List<MaterialSkill> skills;

    // Java parity: nullable Integer skill_obstacle. XmlSerializer cannot bind Nullable<int> to an attribute,
    // so a public string proxy carries the wire value and the backing field stays the faithful int?.
    [XmlIgnore] public int? skillObstacle;

    [XmlAttribute("skill_obstacle")]
    public string SkillObstacleRaw
    {
        get => skillObstacle?.ToString();
        set => skillObstacle = string.IsNullOrEmpty(value) ? null : int.Parse(value);
    }

    [XmlAttribute("id")] public int id;

    public List<MaterialSkill> GetSkills()
    {
        return skills;
    }

    public int? GetSkillObstacle()
    {
        return skillObstacle;
    }

    public int GetId()
    {
        return id;
    }
}
