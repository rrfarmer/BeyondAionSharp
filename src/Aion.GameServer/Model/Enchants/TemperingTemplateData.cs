using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Enchants;

/// <summary>Java parity: model/enchants/TemperingTemplateData.</summary>
[XmlType("tempering_data")]
public class TemperingTemplateData
{
    [XmlElement("tempering_stat")] public List<TemperingStat> temperingStats;
    [XmlAttribute("level")] public int level;

    public int GetLevel()
    {
        return level;
    }

    public List<TemperingStat> GetTemperingStats()
    {
        return temperingStats;
    }
}
