using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Flyring;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/FlyRingData (M@xx). @XmlRootElement(fly_rings).</summary>
[XmlRoot("fly_rings")]
public class FlyRingData
{
    [XmlElement("fly_ring")] private List<FlyRingTemplate> flyRingTemplates;

    public int Size()
    {
        if (flyRingTemplates == null)
        {
            flyRingTemplates = new List<FlyRingTemplate>();
            return 0;
        }
        return flyRingTemplates.Count;
    }

    public List<FlyRingTemplate> GetFlyRingTemplates()
    {
        if (flyRingTemplates == null)
        {
            return new List<FlyRingTemplate>();
        }
        return flyRingTemplates;
    }

    public void AddAll(ICollection<FlyRingTemplate> templates)
    {
        if (flyRingTemplates == null)
        {
            flyRingTemplates = new List<FlyRingTemplate>();
        }
        flyRingTemplates.AddRange(templates);
    }
}
