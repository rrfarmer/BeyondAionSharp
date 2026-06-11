using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Idfactory;
using static Aion.GameServer.Services.Items.ItemPacketService;
using ItemUpdateType = Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType;

namespace Aion.GameServer.Services.Items;

/// <summary>Java parity: services/item/ItemSplitService (ATracer). splitItem (split part of a stack into another slot/storage incl. kinah move, restriction checks, new-item creation or merge), mergeStacks, moveKinah (cube&lt;->account-warehouse with checksum), updateKinahCount. static-import ItemPacketService->using static; nested ItemUpdateType alias; String.format->string.Format; switch-on-StorageType->switch w/ block scopes. IStorage/SM_/LegionService red-tolerated.</summary>
public class ItemSplitService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(ItemSplitService));

    /// <summary>
    /// Move part of stack into different slot
    /// </summary>
    public static void SplitItem(Player player, int itemObjId, int destinationObjId, long splitAmount, short slotNum, byte sourceStorageType,
        byte destinationStorageType)
    {
        if (splitAmount <= 0)
        {
            return;
        }
        if (player.IsTrading())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INVENTORY_SPLIT_DURING_TRADE());
            return;
        }

        IStorage sourceStorage = player.GetStorage(sourceStorageType);
        IStorage destStorage = player.GetStorage(destinationStorageType);
        if (sourceStorage == null || destStorage == null)
        {
            log.LogWarning(string.Format("storage null playerName sourceStorage destStorage {0} {1} {2}", player.GetName(), sourceStorageType,
                destinationStorageType));
            return;
        }
        Item sourceItem = sourceStorage.GetItemByObjId(itemObjId);
        Item targetItem = destStorage.GetItemByObjId(destinationObjId);

        if (sourceItem == null)
        {
            sourceItem = sourceStorage.GetKinahItem();
            if (sourceItem == null || sourceItem.GetObjectId() != itemObjId)
            {
                log.LogWarning(string.Format("CHECKPOINT: attempt to split null item {0} {1} {2}", itemObjId, splitAmount, slotNum));
                return;
            }
        }

        if (sourceStorageType != destinationStorageType
            && (ItemRestrictionService.IsItemRestrictedTo(player, sourceItem, destStorage.GetStorageType()) || ItemRestrictionService.IsItemRestrictedFrom(
                player, sourceItem, sourceStorage.GetStorageType())))
        {
            SendStorageUpdatePacket(player, sourceStorage.GetStorageType(), sourceItem);
            return;
        }

        // To move kinah from inventory to warehouse and vice versa client using split item packet
        if (sourceItem.GetItemTemplate().IsKinah())
        {
            MoveKinah(player, sourceStorage, splitAmount);
            return;
        }

        if (targetItem == null)
        {
            if (destStorage.IsFull())
            {
                PacketSendUtility.SendPacket(player, destStorage.GetStorageIsFullMessage());
                return;
            }
            long oldItemCount = sourceItem.GetItemCount() - splitAmount;
            if (sourceItem.GetItemCount() < splitAmount || oldItemCount == 0)
            {
                return;
            }
            if (sourceStorageType != destinationStorageType)
            {
                LegionService.GetInstance().AddWHItemHistory(player, sourceItem.GetItemId(), splitAmount, sourceStorage, destStorage);
            }
            Item newItem = ItemFactory.NewItem(sourceItem.GetItemTemplate().GetTemplateId(), splitAmount);
            if (sourceStorageType == destinationStorageType)
                newItem.SetEquipmentSlot(slotNum);
            sourceStorage.DecreaseItemCount(sourceItem, splitAmount, sourceStorageType == destinationStorageType ? ItemUpdateType.DEC_ITEM_SPLIT
                : ItemUpdateType.DEC_ITEM_SPLIT_MOVE);
            PacketSendUtility.SendPacket(player, SM_CUBE_UPDATE.CubeSize(sourceStorage.GetStorageType(), player));
            if (destStorage.Add(newItem) == null)
            {
                // if item was not added - we can release its id
                IDFactory.GetInstance().ReleaseId(newItem.GetObjectId());
            }
        }
        else if (targetItem.GetItemId() == sourceItem.GetItemId())
        {
            if (sourceStorageType != destinationStorageType)
            {
                LegionService.GetInstance().AddWHItemHistory(player, sourceItem.GetItemId(), splitAmount, sourceStorage, destStorage);
            }
            MergeStacks(sourceStorage, destStorage, sourceItem, targetItem, splitAmount);
        }
    }

    /// <summary>
    /// Merge 2 stacks with simple validation
    /// </summary>
    public static void MergeStacks(IStorage sourceStorage, IStorage destStorage, Item sourceItem, Item targetItem, long count)
    {
        if (sourceItem.GetItemCount() >= count)
        {
            long freeCount = targetItem.GetFreeCount();
            count = count > freeCount ? freeCount : count;
            long leftCount = destStorage.IncreaseItemCount(targetItem, count,
                sourceStorage.GetStorageType() == destStorage.GetStorageType() ? ItemUpdateType.INC_ITEM_MERGE : ItemUpdateType.INC_ITEM_COLLECT);
            sourceStorage.DecreaseItemCount(sourceItem, count - leftCount,
                sourceStorage.GetStorageType() == destStorage.GetStorageType() ? ItemUpdateType.DEC_ITEM_SPLIT : ItemUpdateType.DEC_ITEM_SPLIT_MOVE);
        }
    }

    private static void MoveKinah(Player player, IStorage source, long splitAmount)
    {
        if (source.GetKinah() < splitAmount)
            return;
        switch (source.GetStorageType())
        {
            case StorageType.CUBE:
                {
                    IStorage destination = player.GetStorage(StorageType.ACCOUNT_WAREHOUSE.GetId());
                    long chksum = (source.GetKinah() - splitAmount) + (destination.GetKinah() + splitAmount);

                    if (chksum != source.GetKinah() + destination.GetKinah())
                        return;

                    UpdateKinahCount(source, splitAmount, destination);
                    break;
                }

            case StorageType.ACCOUNT_WAREHOUSE:
                {
                    IStorage destination = player.GetStorage(StorageType.CUBE.GetId());
                    long chksum = (source.GetKinah() - splitAmount) + (destination.GetKinah() + splitAmount);

                    if (chksum != source.GetKinah() + destination.GetKinah())
                        return;

                    UpdateKinahCount(source, splitAmount, destination);
                    break;
                }
        }
    }

    private static void UpdateKinahCount(IStorage source, long splitAmount, IStorage destination)
    {
        source.DecreaseKinah(splitAmount, ItemUpdateType.DEC_ITEM_SPLIT);
        destination.IncreaseKinah(splitAmount, ItemUpdateType.INC_KINAH_MERGE);
    }
}
