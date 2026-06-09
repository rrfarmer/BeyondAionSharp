using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Factions;

/// <summary>Java parity: model/templates/factions/NpcFactionTemplate (vlog).</summary>
[XmlType("NpcFaction")]
public class NpcFactionTemplate : IL10n
{
    [XmlAttribute("id")] private int id;
    [XmlAttribute("name")] private string name;
    [XmlAttribute("name_id")] private int nameId;
    [XmlAttribute("category")] private FactionCategory category;

    // Java parity: nullable Integer min_level (getMinLevel() unboxes — NPE if null, matched by .Value).
    [XmlAttribute("min_level")] private int? minLevel;

    [XmlAttribute("max_level")] private int maxLevel = 99;
    [XmlAttribute("race")] private Race race;

    // Java parity: @XmlAttribute(name="npc_ids") List<Integer> — space-separated.
    private List<int> npcIds;

    [XmlAttribute("npc_ids")]
    public string NpcIdsRaw
    {
        get => npcIds == null ? null : string.Join(" ", npcIds);
        set => npcIds = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlAttribute("skill_points")] private int skillPoints;

    public int GetId()
    {
        return id;
    }

    public string GetName()
    {
        return name;
    }

    public int GetL10nId()
    {
        return nameId;
    }

    public FactionCategory GetCategory()
    {
        return category;
    }

    public int GetMinLevel()
    {
        return minLevel.Value;
    }

    public int GetMaxLevel()
    {
        return maxLevel;
    }

    public Race GetRace()
    {
        return race;
    }

    public bool IsMentor()
    {
        return category == FactionCategory.MENTOR;
    }

    public List<int> GetNpcIds()
    {
        return npcIds;
    }

    public int GetSkillPoints()
    {
        return skillPoints;
    }
}
