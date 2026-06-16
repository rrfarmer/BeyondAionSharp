using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Dataholders;

/// <summary>
/// Java parity: dataholders/AbsoluteStatsData (@author Rolandas, @XmlRootElement "absolute_stats").
/// </summary>
[XmlRoot("absolute_stats")]
public class AbsoluteStatsData
{
    [XmlElement("stats_set")]
    public List<AbsoluteStatsTemplate> absoluteStats;

    [XmlIgnore]
    private readonly Dictionary<int, ModifiersTemplate> absoluteStatsData = new Dictionary<int, ModifiersTemplate>();

    // Java parity: afterUnmarshal(Unmarshaller, Object) — index templates by id, then drop the raw list.
    // Single object param matches JaxbHolderLoader's reflective AfterUnmarshal(object) invocation.
    public void AfterUnmarshal(object parent)
    {
        foreach (AbsoluteStatsTemplate stats in absoluteStats)
        {
            absoluteStatsData[stats.GetId()] = stats.GetModifiers();
        }
        absoluteStats = null;
    }

    public ModifiersTemplate GetTemplate(int statSetId)
    {
        absoluteStatsData.TryGetValue(statSetId, out ModifiersTemplate? template);
        return template;
    }

    public int Size()
    {
        return absoluteStatsData.Count;
    }
}
