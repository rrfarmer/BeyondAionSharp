using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Event;

/// <summary>
/// An event skill buff — skill ids, application pool, triggers, and optional restrictions.
/// Java parity: model/templates/event/Buff.
/// </summary>
/// <remarks>
/// Java Buff.BuffMapType is an enum with Predicate&lt;WorldMapInstance&gt; fields.
/// C# enums cannot hold fields. The enum members are ported faithfully; the
/// Matches(WorldMapInstance) extension method is recorded as backlog (requires
/// WorldMapInstance which is not yet ported — upward behavior, callers are in the
/// event buff application layer).
///
/// TODO-backlog: BuffMapTypeExtensions.Matches(WorldMapInstance) — wire when WorldMapInstance ported.
///
/// Buff.afterUnmarshal pool-size validation (log.warn) is init-time behavior ported
/// as a comment; no equivalent C# XmlSerializer callback exists.
/// TODO-backlog: validate pool &lt;= skillIds.Count after deserialization.
/// </remarks>
[XmlType("Buff")]
public class Buff
{
    // Java parity: @XmlAttribute(name="skill_ids") Set<Integer> — space-separated
    // C#: string attribute parsed to HashSet<int> via property setter
    private HashSet<int>? _skillIds;

    [XmlAttribute("skill_ids")]
    public string? SkillIdsRaw
    {
        get => _skillIds == null ? null : string.Join(" ", _skillIds);
        set => _skillIds = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToHashSet();
    }

    [XmlAttribute("pool")]       public int  Pool        { get; set; }
    [XmlAttribute("permanent")]  public bool IsPermanent { get; set; }
    [XmlAttribute("team")]       public bool IsTeam      { get; set; }

    [XmlElement("restriction")] public BuffRestriction? Restriction { get; set; }
    [XmlElement("trigger")]     public List<Trigger>?   Triggers    { get; set; }

    public HashSet<int>? GetSkillIds()       => _skillIds;
    public List<Trigger>? GetTriggers()      => Triggers;
    public int GetPool()                     => Pool;
    public bool GetIsPermanent()             => IsPermanent;
    public bool GetIsTeam()                  => IsTeam;
    public BuffRestriction? GetRestriction() => Restriction;

    // Java parity: inner enum BuffMapType (predicate fields omitted as backlog)
    [XmlType("BuffMapType")]
    public enum BuffMapType
    {
        // Java parity: WORLD_MAP(instance -> !instance.getTemplate().isInstance())
        WORLD_MAP,
        // Java parity: SOLO_INSTANCE(instance -> instance.getMaxPlayers() == 1)
        SOLO_INSTANCE,
        // Java parity: GROUP_INSTANCE(instance -> instance.getMaxPlayers() > 1 && <= 6)
        GROUP_INSTANCE,
        // Java parity: ALLIANCE_INSTANCE(instance -> instance.getMaxPlayers() > 6 && <= 24)
        ALLIANCE_INSTANCE,
    }

    // Java parity: inner enum TriggerCondition
    [XmlType("TriggerCondition")]
    public enum TriggerCondition
    {
        ENTER_MAP,
        ENTER_TEAM,
        PVE_KILL,
        PVP_KILL,
    }

    // Java parity: inner static class Trigger
    [XmlType("Trigger")]
    public class Trigger
    {
        [XmlAttribute("condition")] public TriggerCondition Condition { get; set; }
        [XmlAttribute("chance")]    public float            Chance    { get; set; } = 100f;

        public TriggerCondition GetCondition() => Condition;
        public float GetChance()               => Chance;
    }
}
