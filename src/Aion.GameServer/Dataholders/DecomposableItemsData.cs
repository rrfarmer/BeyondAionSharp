using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/DecomposableItemsData (antness). @XmlRootElement(decomposable_items); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("decomposable_items")]
public class DecomposableItemsData
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields).
    [XmlElement("decomposable")] public List<DecomposableItemInfo> decomposableItemsTemplates;

    [XmlIgnore] private readonly Dictionary<int, List<ExtractedItemsCollection>> decomposableItemsInfo = new();
    [XmlIgnore] private readonly Dictionary<int, List<ResultedItem>> selectableDecomposables = new();

    public void AfterUnmarshal(object parent)
    {
        AfterUnmarshal(DataManager.ITEM_DATA, parent);
    }

    // Boot-time overload: StaticData.LoadLeafHoldersFromFiles calls this with the in-progress ItemData because the
    // DataManager singleton bridge is not yet registered during the leaf-holder load (mirrors Java's StaticDataListener
    // handing afterUnmarshal the StaticData currently being unmarshalled).
    public void AfterUnmarshal(ItemData itemData, object parent)
    {
        // Children-first cascade: JAXB fires each ResultedItem/RandomItem.afterUnmarshal during unmarshal (validating
        // the reward item id against the in-progress ITEM_DATA + defaulting/validating min/max counts) BEFORE this
        // holder's afterUnmarshal runs. XmlSerializer does not auto-fire nested JAXB callbacks, so replicate the
        // cascade here before the parent indexing logic. ResultedItem throws on an invalid item id, exactly as Java.
        foreach (DecomposableItemInfo template in decomposableItemsTemplates)
        {
            if (template.itemsCollections == null)
                continue;
            foreach (ExtractedItemsCollection collection in template.itemsCollections)
            {
                if (collection.items != null)
                    foreach (ResultedItem item in collection.items)
                        item.AfterUnmarshal(itemData, parent);
                if (collection.randomItems != null)
                    foreach (RandomItem randomItem in collection.randomItems)
                        randomItem.AfterUnmarshal();
            }
        }

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
