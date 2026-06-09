using System.Collections;
using System.Collections.Generic;
using Aion.GameServer.Dataholders.Loadingutils.Adapters;
using Aion.GameServer.Model.Templates.Item;

namespace Aion.GameServer.Model.Items;

/// <summary>
/// Java parity: model/items/NpcEquippedGear (Luno).
/// Java had @XmlJavaTypeAdapter(NpcEquippedGearAdapter) — the template loader supplies the NpcEquipmentList.
/// </summary>
public class NpcEquippedGear : IEnumerable<KeyValuePair<ItemSlot, ItemTemplate>>
{
    private SortedDictionary<ItemSlot, ItemTemplate> items;
    private int mask;

    private NpcEquipmentList v;

    public NpcEquippedGear(NpcEquipmentList v)
    {
        this.v = v;
    }

    public int GetItemsMask()
    {
        if (items == null)
            Init();
        return mask;
    }

    public IEnumerator<KeyValuePair<ItemSlot, ItemTemplate>> GetEnumerator()
    {
        if (items == null)
            Init();
        return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Here NPC equipment mask is initialized. All NPC slot masks should be lower than 65536.</summary>
    public void Init()
    {
        lock (this)
        {
            if (items == null)
            {
                items = new SortedDictionary<ItemSlot, ItemTemplate>();
                foreach (ItemTemplate item in v.items)
                {
                    ItemSlot[] itemSlots = ItemSlotExtensions.GetSlotsFor(item.GetItemSlot());
                    foreach (ItemSlot itemSlot in itemSlots)
                    {
                        if (!items.ContainsKey(itemSlot))
                        {
                            items[itemSlot] = item;
                            mask |= (int)itemSlot.GetSlotIdMask();
                            break;
                        }
                    }
                }
            }
            v = null;
        }
    }

    public ItemTemplate GetItem(ItemSlot itemSlot)
    {
        return items != null && items.TryGetValue(itemSlot, out ItemTemplate item) ? item : null;
    }
}
