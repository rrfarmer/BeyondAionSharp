using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/LegionDominionData. @XmlRootElement(legion_dominion_template).</summary>
[XmlRoot("legion_dominion_template")]
public class LegionDominionData
{
    // Public so XmlSerializer can populate it (JAXB used a private field via @XmlAccessorType(FIELD)).
    [XmlElement("legion_dominion_location")] public List<LegionDominionLocationTemplate> ldl;

    public int Size()
    {
        return ldl.Count;
    }

    public List<LegionDominionLocationTemplate> GetLocationTemplates()
    {
        return ldl;
    }
}
