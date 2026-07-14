using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.GlobalDrops;

namespace Aion.GameServer.Model.Templates.Event;

/// <summary>Java parity: model/templates/event/EventTemplate.</summary>
[XmlType("EventTemplate")]
public class EventTemplate
{
    [XmlAttribute("name")]  public string? Name { get; set; }

    // Java: @XmlJavaTypeAdapter(LocalDateTimeAdapter) → string setter parses ISO-8601
    private DateTime? _startDate;
    private DateTime? _endDate;

    [XmlAttribute("start")]
    public string? Start
    {
        get => _startDate?.ToString("s");
        set => _startDate = value is null ? null : DateTime.Parse(value);
    }

    [XmlAttribute("end")]
    public string? End
    {
        get => _endDate?.ToString("s");
        set => _endDate = value is null ? null : DateTime.Parse(value);
    }

    // Java JAXB leaves a missing optional enum attribute as null. XmlSerializer cannot bind
    // Nullable<enum> directly as an attribute, so preserve the wire value through a string proxy.
    [XmlIgnore] public EventTheme? Theme { get; set; }

    [XmlAttribute("theme")]
    public string? ThemeRaw
    {
        get => Theme?.ToString();
        set => Theme = string.IsNullOrEmpty(value) ? null : Enum.Parse<EventTheme>(value);
    }

    [XmlElement("login_message")] public string? LoginMessage { get; set; }

    // Java: @XmlElementWrapper("config_properties") @XmlElement("property")
    [XmlArray("config_properties")]
    [XmlArrayItem("property")]
    public List<string>? ConfigProperties { get; set; }

    // Java: @XmlElementWrapper("event_drops") @XmlElement("gd_rule")
    [XmlArray("event_drops")]
    [XmlArrayItem("gd_rule")]
    public List<GlobalRule>? EventDropRules { get; set; }

    [XmlElement("quests")]        public EventQuestList?  Quests        { get; set; }
    [XmlElement("spawns")]        public SpawnsData?      Spawns        { get; set; }
    [XmlElement("inventory_drop")] public InventoryDrop?  InventoryDrop { get; set; }

    // Java: @XmlList @XmlElement("surveys") List<String> → space-separated string attribute
    private List<string>? _surveys;

    [XmlElement("surveys")]
    public string? SurveysRaw
    {
        get => _surveys is { Count: > 0 } ? string.Join(" ", _surveys) : null;
        set => _surveys = value is null ? null
            : [.. value.Split(' ', StringSplitOptions.RemoveEmptyEntries)];
    }

    // Java: @XmlElementWrapper("buffs") @XmlElement("buff")
    [XmlArray("buffs")]
    [XmlArrayItem("buff")]
    public List<Buff>? Buffs { get; set; }

    // ── getters ──────────────────────────────────────────────────────────────
    public string?         GetName()          => Name;
    public DateTime?       GetStartDate()     => _startDate;
    public DateTime?       GetEndDate()       => _endDate;
    public EventTheme?     GetTheme()         => Theme;
    public string?         GetLoginMessage()  => LoginMessage;
    public bool            HasConfigProperties() => ConfigProperties != null;
    public List<GlobalRule>? GetEventDropRules() => EventDropRules;
    public SpawnsData?     GetSpawns()        => Spawns;
    public InventoryDrop?  GetInventoryDrop() => InventoryDrop;
    public List<string>?   GetSurveys()       => _surveys;
    public List<Buff>?     GetBuffs()         => Buffs;

    public List<int> GetStartableQuests()   => Quests?.GetStartableQuests()   ?? [];
    public List<int> GetMaintainableQuests() => Quests?.GetMaintainQuests()   ?? [];

    /// <summary>
    /// Java parity: isInEventPeriod(LocalDateTime time)
    /// Null start = already started. Null end = never ends.
    /// </summary>
    public bool IsInEventPeriod(DateTime time) =>
        (_startDate == null || time >= _startDate.Value) &&
        (_endDate   == null || time <  _endDate.Value);

    /// <summary>Java parity: loadConfigProperties() — parses configProperties list as key=value pairs.</summary>
    public Dictionary<string, string>? LoadConfigProperties()
    {
        if (ConfigProperties == null) return null;
        var props = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in ConfigProperties)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
            var idx = trimmed.IndexOf('=');
            if (idx > 0)
                props[trimmed[..idx].Trim()] = trimmed[(idx + 1)..].Trim();
        }
        return props;
    }
}
