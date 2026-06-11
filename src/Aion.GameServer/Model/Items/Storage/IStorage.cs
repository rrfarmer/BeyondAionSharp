using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Items.Storage;

/// <summary>
/// Public interface for Storage. Java parity: model/items/storage/IStorage extends Persistable.
/// </summary>
public interface IStorage : IPersistable
{
    void SetOwner(Aion.GameServer.Model.GameObjects.Players.Player player);

    long GetKinah();

    /// <summary>kinah item or null if storage never had kinah</summary>
    Item GetKinahItem();

    StorageType GetStorageType();

    void IncreaseKinah(long amount);

    void IncreaseKinah(long amount, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType);

    bool TryDecreaseKinah(long amount);

    bool TryDecreaseKinah(long amount, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType);

    void DecreaseKinah(long amount);

    void DecreaseKinah(long amount, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType);

    long IncreaseItemCount(Item item, long count);

    long IncreaseItemCount(Item item, long count, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType);

    long DecreaseItemCount(Item item, long count);

    long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType);

    long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.QuestEngine.Model.QuestStatus questStatus);

    /// <summary>Add operation should be used for new items incoming into storage from outside.</summary>
    Item Add(Item item);

    Item Add(Item item, Aion.GameServer.Services.Items.ItemPacketService.ItemAddType addType);

    /// <summary>Put operation is used in some operations like unequip.</summary>
    Item Put(Item item);

    Item Remove(Item item);

    Item Delete(Item item);

    Item Delete(Item item, Aion.GameServer.Services.Items.ItemPacketService.ItemDeleteType deleteType);

    bool DecreaseByItemId(int itemId, long count);

    bool DecreaseByItemId(int itemId, long count, Aion.GameServer.QuestEngine.Model.QuestStatus questStatus);

    bool DecreaseByObjectId(int itemObjId, long count);

    bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType);

    bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.QuestEngine.Model.QuestStatus questStatus);

    Item GetFirstItemByItemId(int itemId);

    List<Item> GetItemsWithKinah();

    List<Item> GetItems();

    List<Item> GetItemsByItemId(int itemId);

    Item GetItemByObjId(int itemObjId);

    long GetItemCountByItemId(int itemId);

    bool IsFull();

    int GetFreeSlots();

    int GetLimit();

    int GetRowLength();

    int Size();

    System.Collections.Concurrent.ConcurrentQueue<Item> GetDeletedItems();

    void OnLoadHandler(Item item);

    // Java parity: default getStorageIsFullMessage() — switch over StorageType; class-enum so use if/else (reference equality on singletons).
    Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage GetStorageIsFullMessage()
    {
        StorageType st = GetStorageType();
        if (st == StorageType.CUBE)
            return Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_WAREHOUSE_FULL_INVENTORY();
        if (st == StorageType.REGULAR_WAREHOUSE || st == StorageType.ACCOUNT_WAREHOUSE || st == StorageType.LEGION_WAREHOUSE)
            return Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_WAREHOUSE_DEPOSIT_FULL_BASKET();
        if (st == StorageType.PET_BAG_6 || st == StorageType.PET_BAG_12 || st == StorageType.PET_BAG_18 || st == StorageType.PET_BAG_24
            || st == StorageType.CASH_PET_BAG_12 || st == StorageType.CASH_PET_BAG_18 || st == StorageType.CASH_PET_BAG_30 || st == StorageType.CASH_PET_BAG_24
            || st == StorageType.PET_BAG_30 || st == StorageType.CASH_PET_BAG_26 || st == StorageType.CASH_PET_BAG_32 || st == StorageType.CASH_PET_BAG_34)
            return Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_WAREHOUSE_TOO_MANY_ITEMS_TOYPET_WAREHOUSE();
        if (st == StorageType.HOUSE_STORAGE_01 || st == StorageType.HOUSE_STORAGE_02 || st == StorageType.HOUSE_STORAGE_03 || st == StorageType.HOUSE_STORAGE_04
            || st == StorageType.HOUSE_STORAGE_05 || st == StorageType.HOUSE_STORAGE_06 || st == StorageType.HOUSE_STORAGE_07 || st == StorageType.HOUSE_STORAGE_08
            || st == StorageType.HOUSE_STORAGE_09 || st == StorageType.HOUSE_STORAGE_10 || st == StorageType.HOUSE_STORAGE_11 || st == StorageType.HOUSE_STORAGE_12
            || st == StorageType.HOUSE_STORAGE_13 || st == StorageType.HOUSE_STORAGE_14 || st == StorageType.HOUSE_STORAGE_15 || st == StorageType.HOUSE_STORAGE_16
            || st == StorageType.HOUSE_STORAGE_17 || st == StorageType.HOUSE_STORAGE_18 || st == StorageType.HOUSE_STORAGE_19 || st == StorageType.HOUSE_STORAGE_20)
            return Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_HOUSING_WAREHOUSE_TOO_MANY_ITEMS_WAREHOUSE();
        if (st == StorageType.BROKER)
            return Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_VENDOR_FULL_ITEM();
        // MAILBOX
        return Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MAIL_SEND_FULL_BASKET();
    }
}
