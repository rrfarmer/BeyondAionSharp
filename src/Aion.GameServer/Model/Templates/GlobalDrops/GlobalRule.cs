using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.GlobalDrops;

/// <summary>
/// A single global drop rule — defines which items drop under which conditions.
/// Java parity: model/templates/globaldrops/GlobalRule.
/// </summary>
[XmlType("GlobalRule")]
public class GlobalRule
{
    // Java parity: @XmlElementWrapper(name="gd_items") @XmlElement(name="gd_item") List<GlobalDropItem>
    [XmlArray("gd_items")]
    [XmlArrayItem("gd_item")]
    public List<GlobalDropItem>? GdItems { get; set; }

    [XmlElement("gd_maps")]   public GlobalDropMaps?   GdMaps   { get; set; }
    [XmlElement("gd_races")]  public GlobalDropRaces?  GdRaces  { get; set; }
    [XmlElement("gd_tribes")] public GlobalDropTribes? GdTribes { get; set; }
    [XmlElement("gd_ratings")]public GlobalDropRatings? GdRatings { get; set; }
    [XmlElement("gd_worlds")] public GlobalDropWorlds? GdWorlds { get; set; }
    [XmlElement("gd_npcs")]   public GlobalDropNpcs?   GdNpcs   { get; set; }
    [XmlElement("gd_npc_names")] public GlobalDropNpcNames? GdNpcNames { get; set; }
    [XmlElement("gd_npc_groups")] public GlobalDropNpcGroups? GdNpcGroups { get; set; }
    [XmlElement("gd_excluded_npcs")] public GlobalDropExcludedNpcs? GdExcludedNpcs { get; set; }
    [XmlElement("gd_zones")]  public GlobalDropZones?  GdZones  { get; set; }

    [XmlAttribute("rule_name")] public string RuleName { get; set; } = string.Empty;
    [XmlAttribute("chance")]    public float  Chance   { get; set; }
    [XmlAttribute("min_diff")]  public int    MinDiff  { get; set; } = -99;
    [XmlAttribute("max_diff")]  public int    MaxDiff  { get; set; } = 99;
    // Java parity: @XmlAttribute(name="restriction_race") RestrictionRace (nullable enum).
    // XmlSerializer cannot bind a nullable-enum attribute, so keep the typed backing [XmlIgnore]
    // and bind a string proxy: null/empty -> null, else Enum.Parse. 1:1 with JAXB (getter unchanged).
    [XmlIgnore] public RestrictionRace? RestrictionRaceValue { get; set; }

    [XmlAttribute("restriction_race")]
    public string? RestrictionRaceRaw
    {
        get => RestrictionRaceValue?.ToString();
        set => RestrictionRaceValue = string.IsNullOrEmpty(value)
            ? null
            : Enum.Parse<RestrictionRace>(value);
    }
    [XmlAttribute("level_based_chance_reduction")] public bool UseLevelBasedChanceReduction { get; set; }
    [XmlAttribute("member_limit")] public int MemberLimit { get; set; } = 1;
    [XmlAttribute("max_drop_rule")] public int MaxDropRule { get; set; } = 1;
    [XmlAttribute("dynamic_chance")] public bool DynamicChance { get; set; }

    // Java parity: inner enum RestrictionRace
    [XmlType("race_restriction")]
    public enum RestrictionRace { ASMODIANS, ELYOS }

    // Java parity: getters
    public List<GlobalDropItem>? GetDropItems() => GdItems;
    public GlobalDropWorlds? GetGlobalRuleWorlds() => GdWorlds;
    public GlobalDropRaces? GetGlobalRuleRaces() => GdRaces;
    public GlobalDropRatings? GetGlobalRuleRatings() => GdRatings;
    public GlobalDropMaps? GetGlobalRuleMaps() => GdMaps;
    public GlobalDropTribes? GetGlobalRuleTribes() => GdTribes;
    public GlobalDropNpcs? GetGlobalRuleNpcs() => GdNpcs;
    public void SetNpcs(GlobalDropNpcs? value) => GdNpcs = value;
    public GlobalDropNpcNames? GetGlobalRuleNpcNames() => GdNpcNames;
    public GlobalDropNpcGroups? GetGlobalRuleNpcGroups() => GdNpcGroups;
    public GlobalDropExcludedNpcs? GetGlobalRuleExcludedNpcs() => GdExcludedNpcs;
    public GlobalDropZones? GetGlobalRuleZones() => GdZones;
    public string GetRuleName() => RuleName;
    public float GetChance() => Chance;
    public int GetMinDiff() => MinDiff;
    public int GetMaxDiff() => MaxDiff;
    public RestrictionRace? GetRestrictionRace() => RestrictionRaceValue;
    public bool IsUseLevelBasedChanceReduction() => UseLevelBasedChanceReduction;
    public int GetMemberLimit() => MemberLimit;
    public int GetMaxDropRule() => MaxDropRule;
    public bool IsDynamicChance() => DynamicChance;
}
