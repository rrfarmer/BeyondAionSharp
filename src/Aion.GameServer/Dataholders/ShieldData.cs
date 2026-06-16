using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Shield;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ShieldData (Wakizashi). @XmlRootElement(shields).</summary>
[XmlRoot("shields")]
public class ShieldData
{
    [XmlElement("shield")] public List<ShieldTemplate> shieldTemplates;

    public int Size()
    {
        if (shieldTemplates == null)
        {
            shieldTemplates = new List<ShieldTemplate>();
            return 0;
        }
        return shieldTemplates.Count;
    }

    public List<ShieldTemplate> GetShieldTemplates()
    {
        if (shieldTemplates == null)
        {
            return new List<ShieldTemplate>();
        }
        return shieldTemplates;
    }

    public void AddAll(ICollection<ShieldTemplate> templates)
    {
        if (shieldTemplates == null)
        {
            shieldTemplates = new List<ShieldTemplate>();
        }
        shieldTemplates.AddRange(templates);
    }
}
