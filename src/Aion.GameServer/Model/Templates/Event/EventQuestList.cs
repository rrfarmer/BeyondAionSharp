using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Event;

/// <summary>
/// Quest lists for an event — quests that start automatically on login and
/// quests that are maintained (started by other quests).
/// Java parity: model/templates/event/EventQuestList.
/// </summary>
[XmlType("EventQuestList")]
public class EventQuestList
{
    // Java parity: @XmlList @XmlElement(name="startable") List<Integer> startQuests
    // @XmlList in JAXB serialises a list of primitives as space-separated values in an element.
    // C# equivalent: string element + parse on access.
    private List<int>? _startQuests;
    private List<int>? _maintainQuests;

    [XmlElement("startable")]
    public string? StartableRaw
    {
        get => _startQuests == null ? null : string.Join(" ", _startQuests);
        set => _startQuests = ParseInts(value);
    }

    [XmlElement("maintainable")]
    public string? MaintainableRaw
    {
        get => _maintainQuests == null ? null : string.Join(" ", _maintainQuests);
        set => _maintainQuests = ParseInts(value);
    }

    // Java parity: getStartableQuests()
    public List<int> GetStartableQuests() => _startQuests ?? [];

    // Java parity: getMaintainQuests()
    public List<int> GetMaintainQuests() => _maintainQuests ?? [];

    private static List<int>? ParseInts(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? null
            : raw.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
}
