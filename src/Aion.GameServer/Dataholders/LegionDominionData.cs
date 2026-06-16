using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/LegionDominionData. @XmlRootElement(legion_dominion_template).</summary>
[XmlRoot("legion_dominion_template")]
public class LegionDominionData
{
    // Public so XmlSerializer can populate it (JAXB used a private field via @XmlAccessorType(FIELD)).
    // Initialized empty (XmlSerializer add-to-existing / JAXB-faithful) so an unloaded holder yields an empty
    // list, not null — GetLocationTemplates()/Size() never NRE when legion_dominion_template.xml is absent.
    [XmlElement("legion_dominion_location")] public List<LegionDominionLocationTemplate> ldl = new();

    public int Size()
    {
        return ldl.Count;
    }

    public List<LegionDominionLocationTemplate> GetLocationTemplates()
    {
        return ldl;
    }
}
