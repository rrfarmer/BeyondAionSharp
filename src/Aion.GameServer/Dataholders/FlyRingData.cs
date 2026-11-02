using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Flyring;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/FlyRingData (M@xx). @XmlRootElement(fly_rings).</summary>
[XmlRoot("fly_rings")]
public class FlyRingData
{
    [XmlElement("fly_ring")] public List<FlyRingTemplate> flyRingTemplates;

    public int Size()
    {
        return GetFlyRingTemplates().Count;
    }

    public List<FlyRingTemplate> GetFlyRingTemplates()
    {
        return flyRingTemplates == null ? new List<FlyRingTemplate>() : flyRingTemplates;
    }
}
