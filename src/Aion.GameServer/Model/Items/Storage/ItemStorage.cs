using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Items.Storage;

/// <summary>Java parity: model/items/storage/ItemStorage.</summary>
public class ItemStorage
{
    public const long FIRST_AVAILABLE_SLOT = 65535L;

    private readonly ConcurrentDictionary<int, Item> items = new ConcurrentDictionary<int, Item>();
    private readonly StorageType storageType;
    private int limit;

    public ItemStorage(StorageType storageType)
    {
        this.storageType = storageType;
        this.limit = storageType.GetLimit();
    }

    public List<Item> GetItems()
    {
        return new List<Item>(items.Values);
    }

    public int GetLimit()
    {
        return limit;
    }

    public void SetLimit(int limit)
    {
        this.limit = limit;
    }

    public int GetRowLength()
    {
        return storageType.GetLength();
    }

    public Item GetFirstItemById(int itemId)
    {
        foreach (Item item in items.Values)
        {
            if (item.GetItemTemplate().GetTemplateId() == itemId)
            {
                return item;
            }
        }
        return null;
    }

    public List<Item> GetItemsById(int itemId)
    {
        List<Item> temp = new List<Item>();
        foreach (Item item in items.Values)
        {
            if (item.GetItemTemplate().GetTemplateId() == itemId)
            {
                temp.Add(item);
            }
        }
        return temp;
    }

    public Item GetItemByObjId(int itemObjId)
    {
        return items.TryGetValue(itemObjId, out Item item) ? item : null;
    }

    public long GetSlotIdByItemId(int itemId)
    {
        foreach (Item item in items.Values)
        {
            if (item.GetItemTemplate().GetTemplateId() == itemId)
            {
                return item.GetEquipmentSlot();
            }
        }
        return -1;
    }

    public Item GetItemBySlotId(short slotId)
    {
        foreach (Item item in GetCubeItems())
        {
            if (item.GetEquipmentSlot() == slotId)
            {
                return item;
            }
        }
        return null;
    }

    public Item GetSpecialItemBySlotId(short slotId)
    {
        foreach (Item item in GetSpecialCubeItems())
        {
            if (item.GetEquipmentSlot() == slotId)
            {
                return item;
            }
        }
        return null;
    }

    public long GetSlotIdByObjId(int objId)
    {
        Item item = GetItemByObjId(objId);
        if (item != null)
            return item.GetEquipmentSlot();
        else
            return -1;
    }

    public bool PutItem(Item item)
    {
        return items.TryAdd(item.GetObjectId(), item);
    }

    public Item RemoveItem(int objId)
    {
        return items.TryRemove(objId, out Item item) ? item : null;
    }

    public bool IsFull()
    {
        return GetCubeItems().Count >= limit;
    }

    public bool IsFullSpecialCube()
    {
        return GetSpecialCubeItems().Count >= storageType.GetSpecialLimit();
    }

    public List<Item> GetSpecialCubeItems()
    {
        return items.Values.Where(i => i.GetItemTemplate().GetExtraInventoryId() > 0).ToList();
    }

    public List<Item> GetCubeItems()
    {
        return items.Values.Where(i => i.GetItemTemplate().GetExtraInventoryId() < 1).ToList();
    }

    public int GetFreeSlots()
    {
        return limit - GetCubeItems().Count;
    }

    public int GetSpecialCubeFreeSlots()
    {
        return storageType.GetSpecialLimit() - GetSpecialCubeItems().Count;
    }

    public int Size()
    {
        return items.Count;
    }
}
