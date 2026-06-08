using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Event;

/// <summary>
/// Optional restriction rules for an event buff.
/// Java parity: model/templates/event/BuffRestriction.
/// </summary>
/// <remarks>
/// Java uses @XmlList @XmlAttribute(name="maps") Set&lt;BuffMapType&gt; — space-separated
/// enum names. C#: string attribute parsed to HashSet&lt;Buff.BuffMapType&gt;.
/// </remarks>
public class BuffRestriction
{
    private HashSet<Buff.BuffMapType>? _maps;

    // Java parity: @XmlList @XmlAttribute(name="maps") Set<BuffMapType>
    [XmlAttribute("maps")]
    public string? MapsRaw
    {
        get => _maps == null ? null : string.Join(" ", _maps.Select(m => m.ToString().ToUpperInvariant()));
        set => _maps = value == null
            ? null
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                   .Select(Enum.Parse<Buff.BuffMapType>)
                   .ToHashSet();
    }

    [XmlAttribute("team_size_max_percent")]  public float TeamSizeMaxPercent  { get; set; }
    [XmlAttribute("random_days_per_month")]  public int   RandomDaysPerMonth  { get; set; }

    public HashSet<Buff.BuffMapType>? GetMaps() => _maps;
    public float GetTeamSizeMaxPercent()         => TeamSizeMaxPercent;
    public int   GetRandomDaysPerMonth()         => RandomDaysPerMonth;
}
