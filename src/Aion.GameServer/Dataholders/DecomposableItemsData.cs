using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/DecomposableItemsData (antness). @XmlRootElement(decomposable_items); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("decomposable_items")]
public class DecomposableItemsData
{
    [XmlElement("decomposable")] private List<DecomposableItemInfo> decomposableItemsTemplates;

    [XmlIgnore] private readonly Dictionary<int, List<ExtractedItemsCollection>> decomposableItemsInfo = new();
    [XmlIgnore] private readonly Dictionary<int, List<ResultedItem>> selectableDecomposables = new();

    public void AfterUnmarshal(object parent)
    {
        decomposableItemsInfo.Clear();
        foreach (DecomposableItemInfo template in decomposableItemsTemplates)
        {
            List<ExtractedItemsCollection> itemGroups = template.GetItemsCollections();
            if (itemGroups != null)
            {
                if (template.IsIsSelectable())
                {
                    selectableDecomposables[template.GetItemId()] = itemGroups[0].GetItems();
                }
                else
                {
                    decomposableItemsInfo[template.GetItemId()] = itemGroups;
                }
            }
        }
        decomposableItemsTemplates = null;
    }

    public int Size()
    {
        return decomposableItemsInfo.Count;
    }

    public List<ResultedItem> GetSelectableItems(int itemId)
    {
        List<ResultedItem> items = selectableDecomposables.TryGetValue(itemId, out var v) ? v : null;
        return items == null ? null : new List<ResultedItem>(items);
    }

    public List<ExtractedItemsCollection> GetInfoByItemId(int itemId)
    {
        return decomposableItemsInfo.TryGetValue(itemId, out var v) ? v : null;
    }
}
