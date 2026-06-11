using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace Aion.GameServer.QuestEngine.Handlers.Models;

/// <summary>Java parity: questEngine/handlers/models/QuestSkillData. @XmlAttribute List&lt;Integer&gt;→Raw space-sep.</summary>
[XmlType("QuestSkillData")]
public class QuestSkillData
{
    protected List<int> skillIds;

    [XmlAttribute("ids")]
    public string SkillIdsRaw
    {
        get => skillIds == null ? null : string.Join(" ", skillIds);
        set => skillIds = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("start_var")] protected int startVar;
    [XmlAttribute("end_var")] protected int endVar;
    [XmlAttribute("var_num")] protected int varNum;

    public List<int> GetSkillIds()
    {
        return skillIds;
    }

    public int GetVarNum()
    {
        return varNum;
    }

    public int GetStartVar()
    {
        return startVar;
    }

    public int GetEndVar()
    {
        return endVar;
    }
}
