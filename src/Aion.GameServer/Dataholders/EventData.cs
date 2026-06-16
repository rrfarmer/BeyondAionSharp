using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Event;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/EventData. @XmlRootElement(timed_events); afterUnmarshal→AfterUnmarshal(object); isBefore→ &lt; comparison.</summary>
[XmlType("EventData")]
[XmlRoot("timed_events")]
public class EventData
{
    // Java parity: @XmlElement(name="event") List<EventTemplate> events (private field under
    // @XmlAccessorType(FIELD)). XmlSerializer only binds public members, so expose it publicly.
    [XmlElement("event")] public List<EventTemplate> events { get; set; }

    public void AfterUnmarshal(object parent)
    {
        if (events == null)
            events = new List<EventTemplate>();
        foreach (EventTemplate ev in events)
        {
            // Java parity: JAXB invokes SpawnsData.afterUnmarshal(u, parent=EventTemplate) automatically,
            // building the spawn lookup maps and (since parent is EventTemplate) keeping Templates.
            // XmlSerializer does not call that callback, so fire it children-first here.
            ev.GetSpawns()?.Initialize(ev);

            if (ev.GetEndDate() != null && ev.GetStartDate() != null && !(ev.GetStartDate().Value < ev.GetEndDate().Value))
                throw new ArgumentException("Event \"" + ev.GetName() + "\" has an invalid start or end date: start date must be before end date");
        }
    }

    /// <summary>
    /// Merge another deserialized EventData's raw event rows into this one (pre-AfterUnmarshal).
    /// Java imports the timed_events/ dir (custom_events.xml + retail_events.xml), each its own
    /// &lt;timed_events&gt; root; the rows are concatenated before the single AfterUnmarshal pass.
    /// </summary>
    public void MergePending(EventData other)
    {
        if (other.events == null)
            return;
        events ??= new List<EventTemplate>();
        events.AddRange(other.events);
    }

    public int Size()
    {
        return events.Count;
    }

    public List<EventTemplate> GetEvents()
    {
        return events;
    }

    public void SetEvents(List<EventTemplate> events)
    {
        this.events = events;
        AfterUnmarshal(null);
    }

    public void AddAllNpcIdsToSet(ISet<int> npcIds)
    {
        foreach (var spawns in events.Select(ev => ev.GetSpawns()).Where(s => s != null))
            spawns.AddAllNpcIdsToSet(npcIds);
    }
}
