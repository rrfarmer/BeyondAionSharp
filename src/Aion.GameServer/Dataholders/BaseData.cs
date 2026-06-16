using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Base;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/BaseData. @XmlRootElement(base_locations).</summary>
[XmlRoot("base_locations")]
public class BaseData
{
    // Public so XmlSerializer can populate it (JAXB used a private field via @XmlAccessorType(FIELD)).
    [XmlElement("base_location")] public List<BaseTemplate> baseTemplates;

    public int Size()
    {
        return baseTemplates.Count;
    }

    public List<BaseTemplate> GetAllBaseTemplates()
    {
        return baseTemplates;
    }
}
