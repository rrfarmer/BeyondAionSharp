using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Model.Templates.Factions;

/// <summary>Java parity: model/templates/factions/NpcFactionTemplate (vlog).</summary>
[XmlType("NpcFaction")]
public class NpcFactionTemplate : IL10n
{
    [XmlAttribute("id")] public int id;
    [XmlAttribute("name")] public string name;
    [XmlAttribute("name_id")] public int nameId;
    [XmlAttribute("category")] public FactionCategory category;

    // Java parity: nullable Integer min_level (getMinLevel() unboxes — NPE if null, matched by .Value).
    // String-proxy: XmlSerializer cannot bind a nullable value type to an [XmlAttribute]; back it with a
    // string and parse it, mirroring JAXB — missing attribute -> null, present -> Integer.parseInt.
    [XmlIgnore] private int? minLevel;

    [XmlAttribute("min_level")]
    public string MinLevelRaw
    {
        get => minLevel?.ToString(CultureInfo.InvariantCulture);
        set => minLevel = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlAttribute("max_level")] public int maxLevel = 99;
    [XmlAttribute("race")] public Race race;

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

    [XmlAttribute("skill_points")] public int skillPoints;

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
