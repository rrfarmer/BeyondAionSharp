using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Enchants;

/// <summary>Java parity: model/enchants/TemperingList.</summary>
[XmlType("tempering_list")]
public class TemperingList
{
    [XmlElement("tempering_data")] public List<TemperingTemplateData> temperingDatas;
    [XmlAttribute("item_group")] public string itemGroup;

    public string GetItemGroup()
    {
        return itemGroup;
    }

    public List<TemperingTemplateData> GetTemperingDatas()
    {
        return temperingDatas;
    }
}
