using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Road;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/RoadData (SheppeR). @XmlRootElement(roads).</summary>
[XmlRoot("roads")]
public class RoadData
{
    // Public so XmlSerializer can populate it (JAXB used a private field via @XmlAccessorType(FIELD)).
    [XmlElement("road")] public List<RoadTemplate> roadTemplates;

    public int Size()
    {
        return GetRoadTemplates().Count;
    }

    public List<RoadTemplate> GetRoadTemplates()
    {
        return roadTemplates == null ? new List<RoadTemplate>() : roadTemplates;
    }
}
