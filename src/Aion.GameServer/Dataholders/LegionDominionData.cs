using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/LegionDominionData. @XmlRootElement(legion_dominion_template).</summary>
[XmlRoot("legion_dominion_template")]
public class LegionDominionData
{
    [XmlElement("legion_dominion_location")] private List<LegionDominionLocationTemplate> ldl;

    public int Size()
    {
        return ldl.Count;
    }

    public List<LegionDominionLocationTemplate> GetLocationTemplates()
    {
        return ldl;
    }
}
