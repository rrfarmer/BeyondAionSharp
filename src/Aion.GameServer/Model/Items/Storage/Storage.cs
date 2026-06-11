using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Items.Storage;

/// <summary>
/// Java parity: model/items/storage/Storage (abstract base implementing IStorage). The public no-actor
/// IStorage variants are abstract here (subclasses supply the actor); the actor-overloads are internal helpers.
/// </summary>
public abstract class Storage : IStorage
{
    private static readonly ILogger log = NullLogger.Instance;
    private ItemStorage itemStorage;
    private Item kinahItem;
    private StorageType storageType;
    private ConcurrentQueue<Item> deletedItems;
    /// <summary>Can be of 2 types: UPDATED and UPDATE_REQUIRED</summary>
    private IPersistable.PersistentState persistentState = IPersistable.PersistentState.UPDATED;

    public Storage(StorageType storageType) : this(storageType, true)
    {
    }

    public Storage(StorageType storageType, bool withDeletedItems)
    {
        itemStorage = new ItemStorage(storageType);
        this.storageType = storageType;
        if (withDeletedItems)
            this.deletedItems = new ConcurrentQueue<Item>();
    }

    public virtual long GetKinah()
    {
        return kinahItem == null ? 0 : kinahItem.GetItemCount();
    }

    public virtual Item GetKinahItem()
    {
        return kinahItem;
    }

    public virtual StorageType GetStorageType()
    {
        return storageType;
    }

    internal void IncreaseKinah(long amount, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        IncreaseKinah(amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.INC_KINAH_COLLECT, actor);
    }

    internal void IncreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        if (kinahItem == null)
        {
            Add(Aion.GameServer.Services.Item.ItemFactory.NewItem(Aion.GameServer.Model.Items.ItemId.KINAH, 0), actor);
        }
        if (amount > 0)
        {
            IncreaseItemCount(kinahItem, amount, updateType, actor);
        }
    }

    /// <summary>Decrease kinah by amount but check first that it's enough in storage. Returns true if decrease was successful.</summary>
    internal bool TryDecreaseKinah(long amount, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        if (GetKinah() >= amount)
        {
            DecreaseKinah(amount, actor);
            return true;
        }
        return false;
    }

    internal bool TryDecreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        if (GetKinah() >= amount)
        {
            DecreaseKinah(amount, updateType, actor);
            return true;
        }
        return false;
    }

    /// <summary>Just decrease kinah without any checks.</summary>
    internal void DecreaseKinah(long amount, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        DecreaseKinah(amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.DEC_KINAH_BUY, actor);
    }

    internal void DecreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        if (amount > 0)
        {
            DecreaseItemCount(kinahItem, amount, updateType, actor);
        }
    }

    internal long IncreaseItemCount(Item item, long count, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        return IncreaseItemCount(item, count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.DEC_ITEM_USE, actor);
    }

    /// <summary>Increase item count and return left count.</summary>
    internal long IncreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        long leftCount = item.IncreaseItemCount(count);
        if (actor != null)
            Aion.GameServer.Services.Item.ItemPacketService.SendItemPacket(actor, storageType, item, updateType);
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        return leftCount;
    }

    internal long DecreaseItemCount(Item item, long count, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        return DecreaseItemCount(item, count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.DEC_ITEM_USE, actor);
    }

    /// <summary>Decrease item count and return left count.</summary>
    internal long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        return DecreaseItemCount(item, count, updateType, null, actor);
    }

    internal long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.Questengine.Model.QuestStatus? questStatus, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        if (item == null)
            return 0;

        Aion.GameServer.Services.Item.ItemPacketService.ItemDeleteType deleteType = questStatus != null
            ? Aion.GameServer.Services.Item.ItemPacketService.ItemDeleteType.FromQuestStatus(questStatus.Value)
            : Aion.GameServer.Services.Item.ItemPacketService.ItemDeleteType.FromUpdateType(updateType);
        long leftCount = item.DecreaseItemCount(count);
        bool isKinah = item.GetItemTemplate().IsKinah();
        if (item.GetItemCount() <= 0 && !isKinah)
            Delete(item, deleteType, actor);
        else
            Aion.GameServer.Services.Item.ItemPacketService.SendItemPacket(actor, storageType, item, updateType);

        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        return leftCount;
    }

    /// <summary>
    /// Called only for new items added to inventory (loading from DB). Kinah is stored separately.
    /// </summary>
    public virtual void OnLoadHandler(Item item)
    {
        if (item.GetItemTemplate().IsKinah())
            kinahItem = item;
        else
            itemStorage.PutItem(item);
    }

    internal Item Add(Item item, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        return Add(item, Aion.GameServer.Services.Item.ItemService.DEFAULT_UPDATE_PREDICATE.GetAddType(), actor);
    }

    internal Item Add(Item item, Aion.GameServer.Services.Item.ItemPacketService.ItemAddType addType, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        if (item.GetItemTemplate().IsKinah())
        {
            this.kinahItem = item;
        }
        else if (!itemStorage.PutItem(item))
        {
            return null;
        }
        item.SetItemLocation(storageType.GetId());
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        if (actor != null)
        {
            Aion.GameServer.Services.Item.ItemPacketService.SendStorageUpdatePacket(actor, storageType, item, addType);
            if (storageType == StorageType.CUBE)
                Aion.GameServer.Questengine.QuestEngine.GetInstance().OnItemGet(actor, item.GetItemId());
        }
        return item;
    }

    /// <summary>used only for character transfers</summary>
    public Item Add_CharacterTransfer(Item item)
    {
        if (item.GetItemTemplate().IsKinah())
        {
            this.kinahItem = item;
        }
        else if (!itemStorage.PutItem(item))
        {
            return null;
        }
        item.SetItemLocation(storageType.GetId());
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        return item;
    }

    // a bit misleading name - but looks like it's used only for equipment
    internal Item Put(Item item, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        if (!itemStorage.PutItem(item))
        {
            return null;
        }
        item.SetItemLocation(storageType.GetId());
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        Aion.GameServer.Services.Item.ItemPacketService.SendItemUpdatePacket(actor, storageType, item, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.EQUIP_UNEQUIP);
        return item;
    }

    /// <summary>Remove item from storage without changing its state.</summary>
    public virtual Item Remove(Item item)
    {
        return itemStorage.RemoveItem(item.GetObjectId());
    }

    /// <summary>Delete item from storage and mark for DB update.</summary>
    internal Item Delete(Item item, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        return Delete(item, Aion.GameServer.Services.Item.ItemPacketService.ItemDeleteType.DEFAULT, actor);
    }

    /// <summary>Delete item from storage and mark for DB update.</summary>
    internal Item Delete(Item item, Aion.GameServer.Services.Item.ItemPacketService.ItemDeleteType deleteType, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        if (Remove(item) != null)
        {
            item.SetPersistentState(IPersistable.PersistentState.DELETED);
            deletedItems.Enqueue(item);
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            Aion.GameServer.Services.Item.ItemPacketService.SendItemDeletePacket(actor, StorageType.GetStorageTypeById(item.GetItemLocation()), item, deleteType);
            if (Aion.GameServer.Configs.Main.LoggingConfig.LOG_ITEM && !item.GetItemTemplate().IsKinah() && item.GetItemCount() > 0)
            {
                string name = (item.GetEnchantLevel() > 0 ? "+" + item.GetEnchantLevel() + " " : "") + item.GetItemName();
                log.LogInformation("Deleted " + item.GetItemId() + " " + name + " from " + actor + " (count: " + item.GetItemCount() + ") (deletion type: " + deleteType + ")");
            }
            Aion.GameServer.Questengine.QuestEngine.GetInstance().OnItemRemoved(actor, item.GetItemId());
            return item;
        }
        return null;
    }

    internal bool DecreaseByItemId(int itemId, long count, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        return DecreaseByItemId(itemId, count, null, actor);
    }

    internal bool DecreaseByItemId(int itemId, long count, Aion.GameServer.Questengine.Model.QuestStatus? questStatus, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        List<Item> items = itemStorage.GetItemsById(itemId);
        if (items.Count == 0)
            return false;

        foreach (Item item in items)
        {
            if (count == 0)
            {
                break;
            }
            count = DecreaseItemCount(item, count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.DEC_ITEM_USE, questStatus, actor);
        }

        return count == 0;
    }

    internal bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        return DecreaseByObjectId(itemObjId, count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.DEC_ITEM_USE, actor);
    }

    internal bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Questengine.Model.QuestStatus? questStatus, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        Item item = itemStorage.GetItemByObjId(itemObjId);
        if (item == null || item.GetItemCount() < count)
            return false;

        return DecreaseItemCount(item, count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.DEC_ITEM_USE, questStatus, actor) == 0;
    }

    internal bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        Item item = itemStorage.GetItemByObjId(itemObjId);
        if (item == null || item.GetItemCount() < count)
            return false;

        return DecreaseItemCount(item, count, updateType, actor) == 0;
    }

    public virtual Item GetFirstItemByItemId(int itemId)
    {
        return this.itemStorage.GetFirstItemById(itemId);
    }

    public virtual List<Item> GetItemsWithKinah()
    {
        List<Item> items = this.itemStorage.GetItems();
        if (this.kinahItem != null)
        {
            items.Add(this.kinahItem);
        }
        return items;
    }

    public virtual List<Item> GetItems()
    {
        return this.itemStorage.GetItems();
    }

    public virtual List<Item> GetItemsByItemId(int itemId)
    {
        return this.itemStorage.GetItemsById(itemId);
    }

    public virtual ConcurrentQueue<Item> GetDeletedItems()
    {
        return deletedItems;
    }

    public virtual Item GetItemByObjId(int itemObjId)
    {
        return this.itemStorage.GetItemByObjId(itemObjId);
    }

    public long GetItemCountByItemId(int itemId)
    {
        List<Item> temp = this.itemStorage.GetItemsById(itemId);
        if (temp.Count == 0)
            return 0;

        long cnt = 0;
        foreach (Item item in temp)
            cnt += item.GetItemCount();

        return cnt;
    }

    public virtual bool IsFull()
    {
        return this.itemStorage.IsFull();
    }

    public bool IsFullSpecialCube()
    {
        return this.itemStorage.IsFullSpecialCube();
    }

    public bool IsFull(int inventory)
    {
        if (inventory > 0)
        {
            return IsFullSpecialCube();
        }
        return IsFull();
    }

    public int GetFreeSlots(int inventory)
    {
        if (inventory > 0)
        {
            return GetSpecialCubeFreeSlots();
        }
        return GetFreeSlots();
    }

    public int GetSpecialCubeFreeSlots()
    {
        return this.itemStorage.GetSpecialCubeFreeSlots();
    }

    public virtual int GetFreeSlots()
    {
        return this.itemStorage.GetFreeSlots();
    }

    public virtual void SetLimit(int limit)
    {
        itemStorage.SetLimit(limit);
    }

    public virtual int GetLimit()
    {
        return this.itemStorage.GetLimit();
    }

    public int GetRowLength()
    {
        return this.itemStorage.GetRowLength();
    }

    // Java parity: final getPersistentState() — non-virtual so subclasses cannot override.
    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    // Java parity: final setPersistentState(PersistentState).
    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        this.persistentState = persistentState;
    }

    public virtual int Size()
    {
        return itemStorage.Size();
    }

    // ----- IStorage public (no-actor) members implemented by subclasses (Java: abstract via subclass) -----
    public abstract void SetOwner(Aion.GameServer.Model.GameObjects.Players.Player player);

    public abstract void IncreaseKinah(long amount);

    public abstract void IncreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType);

    public abstract bool TryDecreaseKinah(long amount);

    public abstract bool TryDecreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType);

    public abstract void DecreaseKinah(long amount);

    public abstract void DecreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType);

    public abstract long IncreaseItemCount(Item item, long count);

    public abstract long IncreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType);

    public abstract long DecreaseItemCount(Item item, long count);

    public abstract long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType);

    public abstract long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.Questengine.Model.QuestStatus questStatus);

    public abstract Item Add(Item item);

    public abstract Item Add(Item item, Aion.GameServer.Services.Item.ItemPacketService.ItemAddType addType);

    public abstract Item Put(Item item);

    public abstract Item Delete(Item item);

    public abstract Item Delete(Item item, Aion.GameServer.Services.Item.ItemPacketService.ItemDeleteType deleteType);

    public abstract bool DecreaseByItemId(int itemId, long count);

    public abstract bool DecreaseByItemId(int itemId, long count, Aion.GameServer.Questengine.Model.QuestStatus questStatus);

    public abstract bool DecreaseByObjectId(int itemObjId, long count);

    public abstract bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType);

    public abstract bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Questengine.Model.QuestStatus questStatus);
}
