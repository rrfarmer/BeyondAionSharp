using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Road;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/RoadData (SheppeR). @XmlRootElement(roads).</summary>
[XmlRoot("roads")]
public class RoadData
{
    [XmlElement("road")] private List<RoadTemplate> roadTemplates;

    public int Size()
    {
        if (roadTemplates == null)
        {
            roadTemplates = new List<RoadTemplate>();
            return 0;
        }
        return roadTemplates.Count;
    }

    public List<RoadTemplate> GetRoadTemplates()
    {
        if (roadTemplates == null)
        {
            return new List<RoadTemplate>();
        }
        return roadTemplates;
    }

    public void AddAll(ICollection<RoadTemplate> templates)
    {
        if (roadTemplates == null)
        {
            roadTemplates = new List<RoadTemplate>();
        }
        roadTemplates.AddRange(templates);
    }
}
