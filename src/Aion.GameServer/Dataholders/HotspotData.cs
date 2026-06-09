using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Hotspot;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/HotspotData (ginho1). @XmlRootElement(hotspot_template).</summary>
[XmlRoot("hotspot_template")]
public class HotspotData
{
    [XmlElement("hotspot_location")] private List<HotspotTemplate> hotspotTemplates;

    public int Size()
    {
        if (hotspotTemplates == null)
        {
            hotspotTemplates = new List<HotspotTemplate>();
            return 0;
        }
        return hotspotTemplates.Count;
    }

    public List<HotspotTemplate> GetHotspotTemplates()
    {
        if (hotspotTemplates == null)
        {
            return new List<HotspotTemplate>();
        }
        return hotspotTemplates;
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
