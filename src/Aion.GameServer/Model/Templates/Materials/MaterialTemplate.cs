using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Materials;

/// <summary>Java parity: model/templates/materials/MaterialTemplate (Rolandas).</summary>
[XmlType("MaterialTemplate")]
public class MaterialTemplate
{
    [XmlElement("skill")] private List<MaterialSkill> skills;

    // Java parity: nullable Integer skill_obstacle.
    [XmlAttribute("skill_obstacle")] private int? skillObstacle;

    [XmlAttribute("id")] private int id;

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
