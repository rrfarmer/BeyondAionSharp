using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using static Aion.GameServer.Services.Items.ItemPacketService;
using ItemDeleteType = Aion.GameServer.Services.Items.ItemPacketService.ItemDeleteType;
using PersistentState = Aion.GameServer.Model.GameObjects.Persistable.PersistentState;

namespace Aion.GameServer.Services.Items;

/// <summary>Java parity: services/item/ItemMoveService (ATracer). moveItem (same-storage reslot or cross-storage move w/ restriction/trade/shutdown checks, stackable merge, full-storage handling, WH history), moveInSameStorage, switchItemsInStorages (swap two items between storages). static-import ItemPacketService.*->using static; nested ItemDeleteType alias; GameServer.isShuttingDownSoon red-tolerated. IStorage/SM_/LegionService red-tolerated.</summary>
public class ItemMoveService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ItemMoveService));

    public static void MoveItem(Player player, int itemObjId, byte sourceStorageType, byte destinationStorageType, short slot)
    {
        IStorage sourceStorage = player.GetStorage(sourceStorageType);
        if (sourceStorage == null)
        {
            log.LogError(player + " tried to move itemObjId " + itemObjId + " from unknown sourceStorageType: " + sourceStorageType);
            return;
        }
        Item item = sourceStorage.GetItemByObjId(itemObjId);
        if (item == null)
            return;

        IStorage targetStorage = player.GetStorage(destinationStorageType);
        if (targetStorage == null)
        {
            log.LogError(player + " tried to move itemObjId " + itemObjId + " to unknown destinationStorageType: " + destinationStorageType);
            return;
        }

        if (sourceStorageType == destinationStorageType)
        {
            if (item.GetEquipmentSlot() != slot)
                MoveInSameStorage(sourceStorage, item, slot);
            return;
        }
        if (ItemRestrictionService.IsItemRestrictedTo(player, item, targetStorage.GetStorageType())
            || ItemRestrictionService.IsItemRestrictedFrom(player, item, sourceStorage.GetStorageType())
            || player.IsTrading()
            || GameServer.IsShuttingDownSoon())
        {
            SendItemUnlockPacket(player, item);
            if (GameServer.IsShuttingDownSoon())
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DISABLE("Shutdown Progress"));
            return;
        }

        if (sourceStorageType == StorageType.LEGION_WAREHOUSE.GetId() || destinationStorageType == StorageType.LEGION_WAREHOUSE.GetId())
        {
            LegionService.GetInstance().AddWHItemHistory(player, item.GetItemId(), item.GetItemCount(), sourceStorage, targetStorage);
        }
        if (slot == -1)
        {
            if (item.GetItemTemplate().IsStackable())
            {
                foreach (Item targetStack in targetStorage.GetItemsByItemId(item.GetItemId()))
                {
                    ItemSplitService.MergeStacks(sourceStorage, targetStorage, item, targetStack, item.GetItemCount());
                    if (item.GetItemCount() == 0)
                    {
                        return;
                    }
                }
            }
        }
        if (targetStorage.IsFull())
        {
            PacketSendUtility.SendPacket(player, targetStorage.GetStorageIsFullMessage());
            SendItemUnlockPacket(player, item);
            return;
        }
        sourceStorage.Remove(item);
        SendItemDeletePacket(player, sourceStorage.GetStorageType(), item, ItemDeleteType.MOVE);
        item.SetEquipmentSlot(slot);
        targetStorage.Add(item);
    }

    private static void MoveInSameStorage(IStorage storage, Item item, short slot)
    {
        storage.SetPersistentState(PersistentState.UPDATE_REQUIRED);
        item.SetEquipmentSlot(slot);
        item.SetPersistentState(PersistentState.UPDATE_REQUIRED);
    }

    public static void SwitchItemsInStorages(Player player, byte sourceStorageType, int sourceItemObjId, byte replaceStorageType, int replaceItemObjId)
    {
        IStorage sourceStorage = player.GetStorage(sourceStorageType);
        IStorage replaceStorage = player.GetStorage(replaceStorageType);

        Item sourceItem = sourceStorage.GetItemByObjId(sourceItemObjId);
        if (sourceItem == null)
            return;

        Item replaceItem = replaceStorage.GetItemByObjId(replaceItemObjId);
        if (replaceItem == null)
            return;

        // restrictions checks
        if (ItemRestrictionService.IsItemRestrictedFrom(player, sourceItem, sourceStorage.GetStorageType())
            || ItemRestrictionService.IsItemRestrictedFrom(player, replaceItem, replaceStorage.GetStorageType())
            || ItemRestrictionService.IsItemRestrictedTo(player, sourceItem, replaceStorage.GetStorageType())
            || ItemRestrictionService.IsItemRestrictedTo(player, replaceItem, sourceStorage.GetStorageType())
            || player.IsTrading()
            || GameServer.IsShuttingDownSoon())
        {
            SendItemUnlockPacket(player, sourceItem);
            SendItemUnlockPacket(player, replaceItem);
            if (GameServer.IsShuttingDownSoon())
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DISABLE("Shutdown Progress"));
            return;
        }

        long sourceSlot = sourceItem.GetEquipmentSlot();
        long replaceSlot = replaceItem.GetEquipmentSlot();

        sourceItem.SetEquipmentSlot(replaceSlot);
        replaceItem.SetEquipmentSlot(sourceSlot);

        sourceStorage.Remove(sourceItem);
        replaceStorage.Remove(replaceItem);

        // correct UI update order is 1)delete items 2) add items
        SendItemDeletePacket(player, sourceStorage.GetStorageType(), sourceItem, ItemDeleteType.MOVE);
        SendItemDeletePacket(player, replaceStorage.GetStorageType(), replaceItem, ItemDeleteType.MOVE);
        sourceStorage.Add(replaceItem);
        replaceStorage.Add(sourceItem);
    }
}
