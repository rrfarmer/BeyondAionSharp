using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Enchants;
using Aion.GameServer.Model.Templates.Item;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/EnchantData (xTz). @XmlRootElement(enchant_templates); nested Map&lt;string,Map&lt;int,List&lt;EnchantStat&gt;&gt;&gt;; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("enchant_templates")]
public class EnchantData
{
    [XmlElement("enchant_list")] private List<EnchantList> enchantList;

    private Dictionary<string, Dictionary<int, List<EnchantStat>>> templates = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (EnchantList enchant in enchantList)
        {
            Dictionary<int, List<EnchantStat>> map = new();
            templates[enchant.GetItemGroup()] = map;
            foreach (EnchantTemplateData data in enchant.GetEnchantDatas())
                map[data.GetLevel()] = data.GetEnchantStats();
        }
        enchantList = null;
    }

    public int Size()
    {
        return templates.Count;
    }

    public Dictionary<int, List<EnchantStat>> GetTemplates(ItemTemplate itemTemplate)
    {
        if (itemTemplate.GetEnchantName() != null)
            return templates.TryGetValue(itemTemplate.GetEnchantName(), out var v) ? v : null;
        else
            return templates.TryGetValue(itemTemplate.GetItemGroup().ToString(), out var v) ? v : null;
    }
}
