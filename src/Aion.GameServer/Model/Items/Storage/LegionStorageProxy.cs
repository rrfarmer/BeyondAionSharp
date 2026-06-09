using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Items.Storage;

/// <summary>Java parity: model/items/storage/LegionStorageProxy extends Storage.</summary>
public class LegionStorageProxy : Storage
{
    private readonly Aion.GameServer.Model.GameObjects.Player.Player actor;
    private readonly Storage storage;

    public LegionStorageProxy(Aion.GameServer.Model.Team.Legion.LegionWarehouse storage, Aion.GameServer.Model.GameObjects.Player.Player actor)
        : base(storage.GetStorageType(), false)
    {
        this.actor = actor;
        this.storage = storage;
    }

    public override void IncreaseKinah(long amount)
    {
        storage.IncreaseKinah(amount, actor);
    }

    public override void IncreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        storage.IncreaseKinah(amount, updateType, actor);
    }

    public override bool TryDecreaseKinah(long amount)
    {
        return storage.TryDecreaseKinah(amount, actor);
    }

    public override bool TryDecreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        return storage.TryDecreaseKinah(amount, updateType, actor);
    }

    public override void DecreaseKinah(long amount)
    {
        storage.DecreaseKinah(amount, actor);
    }

    public override void DecreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        storage.DecreaseKinah(amount, updateType, actor);
    }

    public override long IncreaseItemCount(Item item, long count)
    {
        return storage.IncreaseItemCount(item, count, actor);
    }

    public override long IncreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        return storage.IncreaseItemCount(item, count, updateType, actor);
    }

    public override long DecreaseItemCount(Item item, long count)
    {
        return storage.DecreaseItemCount(item, count, actor);
    }

    public override long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        return storage.DecreaseItemCount(item, count, updateType, actor);
    }

    public override long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.Questengine.Model.QuestStatus questStatus)
    {
        throw new NotSupportedException("Quests should not update LWH!");
    }

    public override Item Add(Item item)
    {
        return storage.Add(item, actor);
    }

    public override Item Add(Item item, Aion.GameServer.Services.Item.ItemPacketService.ItemAddType addType)
    {
        return storage.Add(item, addType, actor);
    }

    public override Item Put(Item item)
    {
        return storage.Put(item, actor);
    }

    public override Item Delete(Item item)
    {
        return storage.Delete(item, actor);
    }

    public override Item Delete(Item item, Aion.GameServer.Services.Item.ItemPacketService.ItemDeleteType deleteType)
    {
        return storage.Delete(item, deleteType, actor);
    }

    public override bool DecreaseByItemId(int itemId, long count)
    {
        return storage.DecreaseByItemId(itemId, count, actor);
    }

    public override bool DecreaseByItemId(int itemId, long count, Aion.GameServer.Questengine.Model.QuestStatus questStatus)
    {
        throw new NotSupportedException("Quests should not update LWH!");
    }

    public override bool DecreaseByObjectId(int itemObjId, long count)
    {
        return storage.DecreaseByObjectId(itemObjId, count, actor);
    }

    public override bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        return storage.DecreaseByObjectId(itemObjId, count, updateType, actor);
    }

    public override bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Questengine.Model.QuestStatus questStatus)
    {
        throw new NotSupportedException("Quests should not update LWH!");
    }

    public override long GetKinah()
    {
        return storage.GetKinah();
    }

    public override Item GetKinahItem()
    {
        return storage.GetKinahItem();
    }

    public override StorageType GetStorageType()
    {
        return storage.GetStorageType();
    }

    public override void OnLoadHandler(Item item)
    {
        storage.OnLoadHandler(item);
    }

    public override Item Remove(Item item)
    {
        return storage.Remove(item);
    }

    public override Item GetFirstItemByItemId(int itemId)
    {
        return storage.GetFirstItemByItemId(itemId);
    }

    public override List<Item> GetItemsWithKinah()
    {
        return storage.GetItemsWithKinah();
    }

    public override List<Item> GetItems()
    {
        return storage.GetItems();
    }

    public override List<Item> GetItemsByItemId(int itemId)
    {
        return storage.GetItemsByItemId(itemId);
    }

    public override ConcurrentQueue<Item> GetDeletedItems()
    {
        return storage.GetDeletedItems();
    }

    public override Item GetItemByObjId(int itemObjId)
    {
        return storage.GetItemByObjId(itemObjId);
    }

    public override bool IsFull()
    {
        return storage.IsFull();
    }

    public override int GetFreeSlots()
    {
        return storage.GetFreeSlots();
    }

    public override void SetLimit(int limit)
    {
        storage.SetLimit(limit);
    }

    public override int GetLimit()
    {
        return storage.GetLimit();
    }

    public override int Size()
    {
        return storage.Size();
    }

    public override void SetOwner(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        throw new NotSupportedException("LWH doesnt have owner");
    }
}
