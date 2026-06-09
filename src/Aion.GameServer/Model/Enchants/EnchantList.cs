using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Enchants;

/// <summary>Java parity: model/enchants/EnchantList.</summary>
[XmlType("enchant_list")]
public class EnchantList
{
    [XmlElement("enchant_data")] protected List<EnchantTemplateData> enchantDatas;
    [XmlAttribute("item_group")] private string itemGroup;

    public List<EnchantTemplateData> GetEnchantDatas()
    {
        return enchantDatas;
    }

    public string GetItemGroup()
    {
        return itemGroup;
    }
}
