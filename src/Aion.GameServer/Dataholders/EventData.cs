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
    [XmlElement("event")] private List<EventTemplate> events;

    public void AfterUnmarshal(object parent)
    {
        if (events == null)
            events = new List<EventTemplate>();
        foreach (EventTemplate ev in events)
        {
            if (ev.GetEndDate() != null && ev.GetStartDate() != null && !(ev.GetStartDate().Value < ev.GetEndDate().Value))
                throw new ArgumentException("Event \"" + ev.GetName() + "\" has an invalid start or end date: start date must be before end date");
        }
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
