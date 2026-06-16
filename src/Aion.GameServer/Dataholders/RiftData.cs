using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Rift;
using Aion.GameServer.Model.Templates.Rift;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/RiftData. @XmlRootElement(rift_locations); LinkedHashMap→Dictionary (insertion order); afterUnmarshal→AfterUnmarshal(object). NOTE: riftTemplates list is NOT nulled (matches Java).</summary>
[XmlRoot("rift_locations")]
public class RiftData
{
    [XmlElement("rift_location")] public List<RiftTemplate> riftTemplates;

    [XmlIgnore] private readonly Dictionary<int, RiftLocation> rift = new();

    public void AfterUnmarshal(object parent)
    {
        if (riftTemplates == null)
            return;
        foreach (RiftTemplate template in riftTemplates)
        {
            rift[template.GetId()] = new RiftLocation(template);
        }
    }

    public int Size()
    {
        return rift.Count;
    }

    public Dictionary<int, RiftLocation> GetRiftLocations()
    {
        return rift;
    }
}
