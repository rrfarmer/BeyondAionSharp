using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Itemset;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ItemSetData (ATracer). @XmlRootElement(item_sets); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("item_sets")]
public class ItemSetData
{
    [XmlElement("itemset")] public List<ItemSetTemplate> itemsetList;

    [XmlIgnore] private Dictionary<int, ItemSetTemplate> sets;
    [XmlIgnore] private Dictionary<int, ItemSetTemplate> setItems;

    public void AfterUnmarshal(object parent)
    {
        sets = new Dictionary<int, ItemSetTemplate>();
        setItems = new Dictionary<int, ItemSetTemplate>();

        foreach (ItemSetTemplate set in itemsetList)
        {
            // Java parity: JAXB invokes each ItemSetTemplate.afterUnmarshal (sets fullbonus item count).
            set.AfterUnmarshal(this);

            sets[set.GetId()] = set;

            // Add reference to the ItemSetTemplate from
            foreach (ItemPart part in set.GetItempart())
            {
                setItems[part.GetItemId()] = set;
            }
        }
        itemsetList = null;
    }

    public ItemSetTemplate GetItemSetTemplate(int itemSetId)
    {
        return sets.TryGetValue(itemSetId, out var v) ? v : null;
    }

    public ItemSetTemplate GetItemSetTemplateByItemId(int itemId)
    {
        return setItems.TryGetValue(itemId, out var v) ? v : null;
    }

    public int Size()
    {
        return sets.Count;
    }
}
