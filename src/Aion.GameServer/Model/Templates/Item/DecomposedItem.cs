using System;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.Templates.Items;

/// <summary>
/// Java parity: model/templates/item/DecomposedItem.
/// One reward alternative of a <see cref="DecomposableSet"/>, weighted against its siblings.
/// </summary>
[XmlType("DecomposedItem")]
public class DecomposedItem
{
    // XmlSerializer binds public members only (Java @XmlAccessorType(FIELD) on private fields).
    [XmlAttribute("id")] public int itemId;
    [XmlAttribute("count")] public int count = 1;
    [XmlAttribute("chance")] public float chance = 100;

    // Java parity: afterUnmarshal(Unmarshaller, Object parent). StaticDataListener (Unmarshaller-keyed) has no C# analog;
    // falls back to DataManager.ITEM_DATA, exactly as the sibling reward classes did.
    public void AfterUnmarshal(object parent)
    {
        AfterUnmarshal(DataManager.ITEM_DATA, parent);
    }

    // Boot-time overload: during LoadLeafHoldersFromFiles the DataManager singleton bridge is not yet registered,
    // so the cascading parent (DecomposableItemsData.AfterUnmarshal) passes the in-progress ItemData explicitly.
    public void AfterUnmarshal(ItemData itemData, object parent)
    {
        if (itemData.GetItemTemplate(itemId) == null)
            throw new ArgumentException("Decomposable reward item ID is invalid: " + itemId);
        if (count <= 0)
            throw new ArgumentException("Decomposable reward item [" + itemId + "] count (" + count + ") must be greater than 0");
        if (chance <= 0 || chance > 100)
            throw new ArgumentException("Decomposable reward item [" + itemId + "] chance (" + chance + ") must be within (0, 100]");
    }

    public int GetItemId()
    {
        return itemId;
    }

    public int GetCount()
    {
        return count;
    }

    public float GetChance()
    {
        return chance;
    }
}
