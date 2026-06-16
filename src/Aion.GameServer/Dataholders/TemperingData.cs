using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Enchants;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/TemperingData (xTz). @XmlRootElement(tempering_templates); nested Map&lt;string,Map&lt;int,List&lt;TemperingStat&gt;&gt;&gt;; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("tempering_templates")]
public class TemperingData
{
    [XmlElement("tempering_list")] public List<TemperingList> temperingList;

    private Dictionary<string, Dictionary<int, List<TemperingStat>>> templates = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (TemperingList tempering in temperingList)
        {
            Dictionary<int, List<TemperingStat>> map = new();
            templates[tempering.GetItemGroup()] = map;
            foreach (TemperingTemplateData data in tempering.GetTemperingDatas())
                map[data.GetLevel()] = data.GetTemperingStats();
        }
        temperingList = null;
    }

    public int Size()
    {
        return templates.Count;
    }

    public Dictionary<int, List<TemperingStat>> GetTemplates(ItemTemplate itemTemplate)
    {
        if (itemTemplate.GetTemperingName() != null)
            return templates.TryGetValue(itemTemplate.GetTemperingName(), out var v) ? v : null;
        else
            return templates.TryGetValue(itemTemplate.GetItemGroup().ToString(), out var v) ? v : null;
    }
}
