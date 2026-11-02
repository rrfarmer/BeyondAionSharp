using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Hotspot;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/HotspotData (ginho1). @XmlRootElement(hotspot_template).</summary>
[XmlRoot("hotspot_template")]
public class HotspotData
{
    // Public so XmlSerializer can populate it (JAXB used a private field via @XmlAccessorType(FIELD)).
    [XmlElement("hotspot_location")] public List<HotspotTemplate> hotspotTemplates;

    public int Size()
    {
        return hotspotTemplates.Count;
    }

    public HotspotTemplate GetHotspotTemplateById(int id)
    {
        foreach (HotspotTemplate t in hotspotTemplates)
        {
            if (t.GetId() == id)
                return t;
        }
        return null;
    }
}
