using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/DecomposableItemsData (antness). @XmlRootElement(decomposable_items); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("decomposable_items")]
public class DecomposableItemsData
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields).
    [XmlElement("decomposable")] public List<DecomposableItem> decomposableItems;

    [XmlIgnore] private readonly Dictionary<int, DecomposableItem> decomposableItemById = new();
    [XmlIgnore] private int overrideCount;

    /// <summary>
    /// Java merges the files of the decomposable_items folder into one &lt;decomposable_items&gt; root before unmarshalling
    /// (<c>&lt;import file="decomposable_items" singleRootTag="true"/&gt;</c>); XmlSerializer reads one file at a time, so the
    /// loader appends each file's rows here and runs AfterUnmarshal once over the whole set, as JAXB would.
    /// </summary>
    public void MergePending(DecomposableItemsData other)
    {
        if (other.decomposableItems == null)
            return;
        decomposableItems ??= [];
        decomposableItems.AddRange(other.decomposableItems);
    }

    public void AfterUnmarshal(object parent)
    {
        AfterUnmarshal(DataManager.ITEM_DATA, parent);
    }

    // Boot-time overload: StaticData.LoadLeafHoldersFromFiles calls this with the in-progress ItemData because the
    // DataManager singleton bridge is not yet registered during the leaf-holder load (mirrors Java's StaticDataListener
    // handing afterUnmarshal the StaticData currently being unmarshalled).
    public void AfterUnmarshal(ItemData itemData, object parent)
    {
        // Children-first cascade: JAXB fires each DecomposedItem.afterUnmarshal during unmarshal (validating the reward
        // item id against the in-progress ITEM_DATA, and its count and chance) BEFORE this holder's afterUnmarshal, and
        // each DecomposableItem's after its own children. XmlSerializer does not auto-fire nested JAXB callbacks, so
        // replicate the cascade here in that order before the parent indexing logic.
        foreach (DecomposableItem template in decomposableItems)
        {
            if (template.sets != null)
                foreach (DecomposableSet set in template.sets)
                {
                    if (set.items != null)
                        foreach (DecomposedItem item in set.items)
                            item.AfterUnmarshal(itemData, parent);
                }
            template.AfterUnmarshal(parent);
        }

        decomposableItemById.Clear();
        Dictionary<int, DecomposableItem> overrides = new();
        foreach (DecomposableItem template in decomposableItems)
        {
            Dictionary<int, DecomposableItem> target = template.IsOverride() ? overrides : decomposableItemById;
            if (!target.TryAdd(template.GetItemId(), template))
                throw new ArgumentException("Duplicate decomposable item " + template.GetItemId());
        }
        // the files of the folder are merged in no particular order, so the custom entries replace the retail ones only after all are read
        foreach (KeyValuePair<int, DecomposableItem> entry in overrides)
            decomposableItemById[entry.Key] = entry.Value;
        foreach (int itemId in decomposableItemById.Where(e => e.Value.GetSets().Count == 0).Select(e => e.Key).ToList())
            decomposableItemById.Remove(itemId);
        overrideCount = overrides.Count;
        decomposableItems = null;
    }

    public int Size()
    {
        return decomposableItemById.Count;
    }

    public int OverrideCount()
    {
        return overrideCount;
    }

    public DecomposableItem Get(int itemId)
    {
        return decomposableItemById.TryGetValue(itemId, out DecomposableItem item) ? item : null;
    }
}
