using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Items.Purification;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ItemPurificationData (Ranastic, Navyan). @XmlRootElement(item_purifications); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("item_purifications")]
public class ItemPurificationData
{
    [XmlElement("item_purification")] public List<ItemPurificationTemplate> itemPurificationTemplates;

    [XmlIgnore] private Dictionary<int, ItemPurificationTemplate> itemPurificationSets;
    [XmlIgnore] private Dictionary<int, Dictionary<int, PurificationResult>> possibleResultItems;

    public void AfterUnmarshal(object parent)
    {
        itemPurificationSets = new Dictionary<int, ItemPurificationTemplate>();
        possibleResultItems = new Dictionary<int, Dictionary<int, PurificationResult>>();

        foreach (ItemPurificationTemplate purificationTemplate in itemPurificationTemplates)
        {
            itemPurificationSets[purificationTemplate.GetBaseItemId()] = purificationTemplate;

            possibleResultItems[purificationTemplate.GetBaseItemId()] = new Dictionary<int, PurificationResult>();
            foreach (PurificationResult resultItem in purificationTemplate.GetPurificationResults())
                possibleResultItems[purificationTemplate.GetBaseItemId()][resultItem.GetResultItemId()] = resultItem;
        }
        itemPurificationTemplates = null;
    }

    public ItemPurificationTemplate GetItemPurificationTemplate(int itemSetId)
    {
        return itemPurificationSets.TryGetValue(itemSetId, out var v) ? v : null;
    }

    public Dictionary<int, PurificationResult> GetResultItemMap(int baseItemId)
    {
        return possibleResultItems.TryGetValue(baseItemId, out var v) ? v : null;
    }

    public int Size()
    {
        return itemPurificationSets.Count;
    }
}
