using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Panels;

/// <summary>Java parity: model/templates/panels/SkillPanel (xTz).</summary>
[XmlType("SkillPanel")]
public class SkillPanel
{
    [XmlAttribute("panel_id")] public sbyte id;

    // Java parity: @XmlAttribute(name="panel_skills") List<Integer> — space-separated attribute.
    protected List<int> skills;

    [XmlAttribute("panel_skills")]
    public string SkillsRaw
    {
        get => skills == null ? null : string.Join(" ", skills);
        set => skills = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    public int GetPanelId()
    {
        return id;
    }

    // Java parity: getSkills() returns null (intentional).
    public List<int> GetSkills()
    {
        return null;
    }

    public bool CanUseSkill(int skillId, int level)
    {
        foreach (int skill in skills)
        {
            if ((skill >> 8) == skillId && (skill & 0xFF) == level)
            {
                return true;
            }
        }
        return false;
    }

    public bool IsSkillPresent(int skillId)
    {
        foreach (int skill in skills)
        {
            if ((skill >> 8) == skillId)
            {
                return true;
            }
        }
        return false;
    }
}
