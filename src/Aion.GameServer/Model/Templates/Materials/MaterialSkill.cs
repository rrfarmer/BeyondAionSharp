using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Materials;

/// <summary>Java parity: model/templates/materials/MaterialSkill (Rolandas).</summary>
[XmlType("MaterialSkill")]
public class MaterialSkill
{
    // Java parity: @XmlList @XmlAttribute List<MaterialActCondition> conditions — space-separated.
    private List<MaterialActCondition> conditions;

    [XmlAttribute("conditions")]
    public string ConditionsRaw
    {
        get => conditions == null ? null : string.Join(" ", conditions);
        set => conditions = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => (MaterialActCondition) Enum.Parse(typeof(MaterialActCondition), s)).ToList();
    }

    [XmlAttribute("frequency")] private int frequency;
    [XmlAttribute("target")] private MaterialTarget target = MaterialTarget.ALL;
    [XmlAttribute("level")] private int level;
    [XmlAttribute("id")] private int id;

    public List<MaterialActCondition> GetConditions()
    {
        return conditions ?? new List<MaterialActCondition>();
    }

    public int GetFrequency()
    {
        return frequency;
    }

    public MaterialTarget GetTarget()
    {
        return target;
    }

    public int GetSkillLevel()
    {
        return level;
    }

    public int GetId()
    {
        return id;
    }
}
