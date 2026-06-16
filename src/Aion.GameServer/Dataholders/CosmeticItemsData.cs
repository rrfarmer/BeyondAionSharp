using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Cosmeticitems;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/CosmeticItemsData. @XmlRootElement(cosmetic_items); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("cosmetic_items")]
public class CosmeticItemsData
{
    [XmlElement("cosmetic_item", typeof(CosmeticItemTemplate))]
    public List<CosmeticItemTemplate> templates;

    private readonly Dictionary<string, CosmeticItemTemplate> cosmeticItemTemplates = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (CosmeticItemTemplate template in templates)
        {
            cosmeticItemTemplates[template.GetCosmeticName()] = template;
        }
        templates = null;
    }

    public int Size()
    {
        return cosmeticItemTemplates.Count;
    }

    public CosmeticItemTemplate GetCosmeticItemsTemplate(string str)
    {
        return cosmeticItemTemplates.TryGetValue(str, out var v) ? v : null;
    }
}
