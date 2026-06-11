using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items.Storage;

namespace Aion.GameServer.Services.Items;

/// <summary>
/// Java parity: services/item/ItemPacketService (ATracer).
/// ItemUpdateType/ItemAddType keep distinct identity (duplicate masks like MAIL/QUEST_WORK_ITEM2=0x36) as plain
/// enums with GetMask()/IsSendable() extensions; ItemDeleteType is a class-enum (static FromX factories).
/// </summary>
public class ItemPacketService
{
    public enum ItemUpdateType
    {
        EQUIP_UNEQUIP, // internal usage only
        CHARGE, // internal usage only
        POLISH_CHARGE, // internal usage only
        STATS_CHANGE, // soul healer pay, manastone socketing, armor/weapons/arrows
        INC_ITEM_MERGE,
        INC_KINAH_MERGE,
        DEC_ITEM_SPLIT,
        DEC_ITEM_SPLIT_MOVE, // move to other storage with split
        PUT, // from other storage
        DEC_ITEM_USE,
        DEC_STIGMA_USE,
        INC_ITEM_COLLECT,
        INC_KINAH_COLLECT,
        INC_ITEM_BUY, // buying from npc
        DEC_KINAH_BUY,
        INC_KINAH_SELL,
        INC_PLAYER_EXCHANGE_GET_BACK,
        PUT_TO_EXCHANGE,
        INC_KINAH_QUEST,
        DEC_KINAH_LEARN, // craft skill learn
        DEC_KINAH_FLY, // teleport or fly
        INC_CASH_ITEM, // event items, for exchange
        INC_ITEM_REPURCHASE,
        DEC_KINAH_CUBE, // expand cube
        DEC_PET_FOOD,
        INC_PASSPORT_ADD,
    }

    public enum ItemAddType
    {
        SERVER_GENERATED, // Banquest Table, Extract Enchanment stones, Remodel, Armfusion
        PARTIAL_WITH_SLOT, // partial content of slot
        ALL_SLOT, // All warehouses/pets
        ITEM_COLLECT, // Drop, Gather
        BUY,
        PLAYER_EXCHANGE_GET,
        PLAYER_EXCHANGE_GET_BACK,
        PLAYER_STORE,
        CRAFTED_ITEM,
        BROKER_BUY,
        BROKER_RETURN,
        QUEST_REWARD_ITEM,
        QUEST_WORK_ITEM,
        QUEST_WORK_ITEM2,
        MAIL,
        SURVEY,
        DECOMPOSABLE, // also Guestbloom
        REPURCHASE,
    }

    /// <summary>Java parity: ItemUpdateType.getKinahUpdateTypeFromAddType (Java declares it on the enum; C# enums can't carry statics).</summary>
    public static ItemUpdateType GetKinahUpdateTypeFromAddType(ItemAddType itemAddType, bool isIncrease)
    {
        if (!isIncrease)
            return ItemUpdateType.DEC_KINAH_BUY;
        return itemAddType switch
        {
            ItemAddType.BUY => ItemUpdateType.INC_KINAH_SELL,
            ItemAddType.ITEM_COLLECT => ItemUpdateType.INC_KINAH_COLLECT,
            ItemAddType.QUEST_WORK_ITEM => ItemUpdateType.INC_KINAH_QUEST,
            _ => ItemUpdateType.INC_KINAH_MERGE,
        };
    }

    /// <summary>Java parity: nested enum ItemDeleteType — class-enum (per-instance mask + static FromX factories).</summary>
    public sealed class ItemDeleteType
    {
        public static readonly ItemDeleteType DEFAULT = new ItemDeleteType(0);
        public static readonly ItemDeleteType SPLIT = new ItemDeleteType(0x04);
        public static readonly ItemDeleteType MOVE = new ItemDeleteType(0x14);
        public static readonly ItemDeleteType DISCARD = new ItemDeleteType(0x15);
        public static readonly ItemDeleteType USE = new ItemDeleteType(0x17);
        public static readonly ItemDeleteType SELL = new ItemDeleteType(0x1F);
        public static readonly ItemDeleteType QUEST_COMPLETE = new ItemDeleteType(0x31);
        public static readonly ItemDeleteType QUEST_START = new ItemDeleteType(0x34);
        public static readonly ItemDeleteType DECOMPOSE = new ItemDeleteType(0x66);
        public static readonly ItemDeleteType REGISTER = new ItemDeleteType(0x78);
        public static readonly ItemDeleteType PUT_TO_EXCHANGE = new ItemDeleteType(0x26);

        private readonly int mask;

        private ItemDeleteType(int mask)
        {
            this.mask = mask;
        }

        public int GetMask()
        {
            return mask;
        }

        public static ItemDeleteType FromUpdateType(ItemUpdateType updateType)
        {
            return updateType switch
            {
                ItemUpdateType.DEC_ITEM_SPLIT => SPLIT,
                ItemUpdateType.DEC_ITEM_USE => USE,
                ItemUpdateType.DEC_ITEM_SPLIT_MOVE => MOVE,
                _ => DEFAULT,
            };
        }

        public static ItemDeleteType FromQuestStatus(Aion.GameServer.QuestEngine.Model.QuestStatus questStatus)
        {
            return questStatus switch
            {
                Aion.GameServer.QuestEngine.Model.QuestStatus.START => QUEST_START,
                Aion.GameServer.QuestEngine.Model.QuestStatus.COMPLETE => QUEST_COMPLETE,
                _ => DEFAULT,
            };
        }
    }

    public static void UpdateItemAfterInfoChange(Aion.GameServer.Model.GameObjects.Players.Player player, Item item)
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmInventoryUpdateItem(player, item));
    }

    public static void UpdateItemAfterInfoChange(Aion.GameServer.Model.GameObjects.Players.Player player, Item item, ItemUpdateType updateType)
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmInventoryUpdateItem(player, item, updateType));
    }

    public static void UpdateItemAfterEquip(Aion.GameServer.Model.GameObjects.Players.Player player, Item item)
    {
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmInventoryUpdateItem(player, item, ItemUpdateType.EQUIP_UNEQUIP));
    }

    public static void SendItemPacket(Aion.GameServer.Model.GameObjects.Players.Player player, StorageType storageType, Item item, ItemUpdateType updateType)
    {
        if (item.GetItemCount() <= 0 && !item.GetItemTemplate().IsKinah())
        {
            SendItemDeletePacket(player, storageType, item, ItemDeleteType.FromUpdateType(updateType));
        }
        else
        {
            SendItemUpdatePacket(player, storageType, item, updateType);
        }
    }

    /// <summary>Item will be deleted from UI slot.</summary>
    public static void SendItemDeletePacket(Aion.GameServer.Model.GameObjects.Players.Player player, StorageType storageType, Item item, ItemDeleteType deleteType)
    {
        if (storageType == StorageType.CUBE)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmDeleteItem(item.GetObjectId(), deleteType));
        }
        else
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmDeleteWarehouseItem(storageType.GetId(), item.GetObjectId(), deleteType));
        }
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmCubeUpdate.CubeSize(storageType, player));
    }

    /// <summary>Item will be updated in UI slot (stacked items). Java fallthrough: LEGION_WAREHOUSE (non-kinah) → default.</summary>
    public static void SendItemUpdatePacket(Aion.GameServer.Model.GameObjects.Players.Player player, StorageType storageType, Item item, ItemUpdateType updateType)
    {
        if (storageType == StorageType.CUBE)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmInventoryUpdateItem(player, item, updateType));
        }
        else if (storageType == StorageType.LEGION_WAREHOUSE && item.GetItemTemplate().IsKinah())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmLegionEdit(0x04, player.GetLegion()));
        }
        else
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmWarehouseUpdateItem(player, item, storageType.GetId(), updateType));
        }
    }

    public static void SendStorageUpdatePacket(Aion.GameServer.Model.GameObjects.Players.Player player, StorageType storageType, Item item)
    {
        SendStorageUpdatePacket(player, storageType, item, ItemAddType.ITEM_COLLECT);
    }

    /// <summary>New item will be displayed in storage. Java fallthrough: LEGION_WAREHOUSE (non-kinah) → default.</summary>
    public static void SendStorageUpdatePacket(Aion.GameServer.Model.GameObjects.Players.Player player, StorageType storageType, Item item, ItemAddType addType)
    {
        Aion.GameServer.Services.Toypet.PetFeedUnusualStorageArtifactCapture.RegisterStorageUpdate(player, storageType, item, addType);
        if (storageType == StorageType.CUBE)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmInventoryAddItem(new List<Item> { item }, player, addType));
        }
        else if (storageType == StorageType.LEGION_WAREHOUSE && item.GetItemTemplate().IsKinah())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmLegionEdit(0x04, player.GetLegion()));
        }
        else
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmWarehouseAddItem(item, storageType.GetId(), player, addType));
        }
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmCubeUpdate.CubeSize(storageType, player));
    }

    public static void SendItemUnlockPacket(Aion.GameServer.Model.GameObjects.Players.Player player, Item item)
    {
        StorageType storageType = StorageType.GetStorageTypeById(item.GetItemLocation());
        if (storageType != null)
            SendStorageUpdatePacket(player, storageType, item, ItemAddType.ALL_SLOT);
    }
}

/// <summary>Java parity: ItemUpdateType per-constant fields (mask, sendable).</summary>
public static class ItemUpdateTypeExtensions
{
    public static int GetMask(this ItemPacketService.ItemUpdateType t) => t switch
    {
        ItemPacketService.ItemUpdateType.EQUIP_UNEQUIP => -1,
        ItemPacketService.ItemUpdateType.CHARGE => -2,
        ItemPacketService.ItemUpdateType.POLISH_CHARGE => -3,
        ItemPacketService.ItemUpdateType.STATS_CHANGE => 0,
        ItemPacketService.ItemUpdateType.INC_ITEM_MERGE => 0x01,
        ItemPacketService.ItemUpdateType.INC_KINAH_MERGE => 0x05,
        ItemPacketService.ItemUpdateType.DEC_ITEM_SPLIT => 0x06,
        ItemPacketService.ItemUpdateType.DEC_ITEM_SPLIT_MOVE => 0x0A,
        ItemPacketService.ItemUpdateType.PUT => 0x13,
        ItemPacketService.ItemUpdateType.DEC_ITEM_USE => 0x16,
        ItemPacketService.ItemUpdateType.DEC_STIGMA_USE => 0x17,
        ItemPacketService.ItemUpdateType.INC_ITEM_COLLECT => 0x19,
        ItemPacketService.ItemUpdateType.INC_KINAH_COLLECT => 0x1A,
        ItemPacketService.ItemUpdateType.INC_ITEM_BUY => 0x1C,
        ItemPacketService.ItemUpdateType.DEC_KINAH_BUY => 0x1D,
        ItemPacketService.ItemUpdateType.INC_KINAH_SELL => 0x20,
        ItemPacketService.ItemUpdateType.INC_PLAYER_EXCHANGE_GET_BACK => 0x23,
        ItemPacketService.ItemUpdateType.PUT_TO_EXCHANGE => 0x25,
        ItemPacketService.ItemUpdateType.INC_KINAH_QUEST => 0x32,
        ItemPacketService.ItemUpdateType.DEC_KINAH_LEARN => 0x49,
        ItemPacketService.ItemUpdateType.DEC_KINAH_FLY => 0x4B,
        ItemPacketService.ItemUpdateType.INC_CASH_ITEM => 0x50,
        ItemPacketService.ItemUpdateType.INC_ITEM_REPURCHASE => 0x51,
        ItemPacketService.ItemUpdateType.DEC_KINAH_CUBE => 0x5A,
        ItemPacketService.ItemUpdateType.DEC_PET_FOOD => 0x5E,
        ItemPacketService.ItemUpdateType.INC_PASSPORT_ADD => 0x8A,
        _ => 0,
    };

    public static bool IsSendable(this ItemPacketService.ItemUpdateType t) =>
        t != ItemPacketService.ItemUpdateType.EQUIP_UNEQUIP
        && t != ItemPacketService.ItemUpdateType.CHARGE
        && t != ItemPacketService.ItemUpdateType.POLISH_CHARGE;
}

/// <summary>Java parity: ItemAddType per-constant mask.</summary>
public static class ItemAddTypeExtensions
{
    public static int GetMask(this ItemPacketService.ItemAddType t) => t switch
    {
        ItemPacketService.ItemAddType.SERVER_GENERATED => 0x00,
        ItemPacketService.ItemAddType.PARTIAL_WITH_SLOT => 0x07,
        ItemPacketService.ItemAddType.ALL_SLOT => 0x13,
        ItemPacketService.ItemAddType.ITEM_COLLECT => 0x19,
        ItemPacketService.ItemAddType.BUY => 0x1C,
        ItemPacketService.ItemAddType.PLAYER_EXCHANGE_GET => 0x21,
        ItemPacketService.ItemAddType.PLAYER_EXCHANGE_GET_BACK => 0x23,
        ItemPacketService.ItemAddType.PLAYER_STORE => 0x2B,
        ItemPacketService.ItemAddType.CRAFTED_ITEM => 0x2D,
        ItemPacketService.ItemAddType.BROKER_BUY => 0x2E,
        ItemPacketService.ItemAddType.BROKER_RETURN => 0x2F,
        ItemPacketService.ItemAddType.QUEST_REWARD_ITEM => 0x30,
        ItemPacketService.ItemAddType.QUEST_WORK_ITEM => 0x35,
        ItemPacketService.ItemAddType.QUEST_WORK_ITEM2 => 0x36,
        ItemPacketService.ItemAddType.MAIL => 0x36,
        ItemPacketService.ItemAddType.SURVEY => 0x40,
        ItemPacketService.ItemAddType.DECOMPOSABLE => 0x50,
        ItemPacketService.ItemAddType.REPURCHASE => 0x51,
        _ => 0,
    };
}
