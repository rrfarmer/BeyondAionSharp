using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Windstreams;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/WindstreamData. @XmlRootElement(windstreams); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("windstreams")]
public class WindstreamData
{
    [XmlElement("windstream")] public List<WindstreamTemplate> wts;

    [XmlIgnore] private readonly Dictionary<int, WindstreamTemplate> windstreams = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (WindstreamTemplate wt in wts)
        {
            windstreams[wt.GetMapId()] = wt;
        }
        wts = null;
    }

    public WindstreamTemplate GetStreamTemplate(int mapId)
    {
        return windstreams.TryGetValue(mapId, out var v) ? v : null;
    }

    public int Size()
    {
        return windstreams.Count;
    }
}
