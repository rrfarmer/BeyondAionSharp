using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Enchants;

/// <summary>Java parity: model/enchants/EnchantTemplateData.</summary>
[XmlType("enchant_data")]
public class EnchantTemplateData
{
    [XmlElement("enchant_stat")] protected List<EnchantStat> enchantStats;
    [XmlAttribute("level")] private int level;

    public List<EnchantStat> GetEnchantStats()
    {
        return enchantStats;
    }

    public int GetLevel()
    {
        return level;
    }
}
