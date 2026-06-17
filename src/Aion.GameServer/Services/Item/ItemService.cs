using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ItemAddType = Aion.GameServer.Services.Items.ItemPacketService.ItemAddType;
using ItemUpdateType = Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType;

namespace Aion.GameServer.Services.Items;

/// <summary>Java parity: services/item/ItemService (KID).</summary>
public class ItemService
{
    private static readonly ILogger log = NullLogger.Instance; // Java logger name: "ITEM_LOG"

    public static readonly ItemUpdatePredicate DEFAULT_UPDATE_PREDICATE = new ItemUpdatePredicate(ItemAddType.ITEM_COLLECT,
        ItemUpdateType.INC_ITEM_COLLECT);

    public static long AddItem(Aion.GameServer.Model.GameObjects.Players.Player player, int itemId, long count, bool allowInventoryOverflow)
    {
        return AddItem(player, itemId, count, null, allowInventoryOverflow, DEFAULT_UPDATE_PREDICATE);
    }

    public static long AddItem(Aion.GameServer.Model.GameObjects.Players.Player player, int itemId, long count)
    {
        return AddItem(player, itemId, count, null, false, DEFAULT_UPDATE_PREDICATE);
    }

    public static long AddItem(Aion.GameServer.Model.GameObjects.Players.Player player, int itemId, long count, bool allowInventoryOverflow, ItemUpdatePredicate predicate)
    {
        return AddItem(player, itemId, count, null, allowInventoryOverflow, predicate);
    }

    /// <summary>Add new item based on all sourceItem values.</summary>
    public static long AddItem(Aion.GameServer.Model.GameObjects.Players.Player player, Item sourceItem)
    {
        return AddItem(player, sourceItem.GetItemId(), sourceItem.GetItemCount(), sourceItem, true, DEFAULT_UPDATE_PREDICATE);
    }

    /// <summary>Add new item based on all sourceItem values, but with different count.</summary>
    public static long AddItem(Aion.GameServer.Model.GameObjects.Players.Player player, Item sourceItem, long count)
    {
        return AddItem(player, sourceItem.GetItemId(), count, sourceItem, false, DEFAULT_UPDATE_PREDICATE);
    }

    /// <summary>Add new item based on all sourceItem values, but with different count.</summary>
    public static long AddItem(Aion.GameServer.Model.GameObjects.Players.Player player, Item sourceItem, long count, bool allowInventoryOverflow, ItemUpdatePredicate predicate)
    {
        return AddItem(player, sourceItem.GetItemId(), count, sourceItem, allowInventoryOverflow, predicate);
    }

    /// <summary>Add new item based on sourceItem values.</summary>
    private static long AddItem(Aion.GameServer.Model.GameObjects.Players.Player player, int itemId, long count, Item sourceItem, bool allowInventoryOverflow, ItemUpdatePredicate predicate)
    {
        if (count <= 0)
            return 0;

        ItemTemplate itemTemplate = DataManager.ITEM_DATA.GetItemTemplate(itemId);
        if (itemTemplate == null)
            throw new System.NullReferenceException("No item with id " + itemId);
        if (predicate == null)
            throw new System.NullReferenceException("Predicate is not supplied");

        if (LoggingConfig.LOG_ITEM)
            log.LogInformation("Item: " + itemTemplate.GetTemplateId() + " [" + itemTemplate.GetName() + "] added to player " + player.GetName() + " (count: " + count
                + ") (type: " + predicate.GetAddType() + ")");

        Storage inventory = player.GetInventory();
        if (itemTemplate.IsKinah())
        {
            // quests do not add here
            inventory.IncreaseKinah(count);
            return 0;
        }

        if (itemTemplate.IsStackable())
            count = AddStackableItem(player, itemTemplate, count, allowInventoryOverflow, predicate);
        else
            count = AddNonStackableItem(player, itemTemplate, count, sourceItem, allowInventoryOverflow, predicate);

        if (count > 0 && inventory.IsFull(itemTemplate.GetExtraInventoryId()))
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_DICE_INVEN_ERROR());

        return count;
    }

    /// <summary>Add non-stackable item to inventory.</summary>
    private static long AddNonStackableItem(Aion.GameServer.Model.GameObjects.Players.Player player, ItemTemplate itemTemplate, long count, Item sourceItem, bool allowInventoryOverflow,
        ItemUpdatePredicate predicate)
    {
        Storage inventory = player.GetInventory();
        while ((allowInventoryOverflow || !inventory.IsFull(itemTemplate.GetExtraInventoryId())) && count > 0)
        {
            Item newItem = ItemFactory.NewItem(itemTemplate.GetTemplateId());

            Aion.GameServer.Taskmanager.Tasks.ExpireTimerTask.GetInstance().RegisterExpirable(newItem, player);
            if (sourceItem != null)
            {
                CopyItemInfo(sourceItem, newItem);
            }
            predicate.ChangeItem(newItem);
            inventory.Add(newItem, predicate.GetAddType());
            count--;
        }
        return count;
    }

    /// <summary>Copy some item values like item stones and enchant level, without any fusion item attributes.</summary>
    public static void CopyItemInfo(Item sourceItem, Item newItem)
    {
        newItem.SetOptionalSockets(sourceItem.GetOptionalSockets());
        newItem.SetItemCreator(sourceItem.GetItemCreator());
        if (sourceItem.HasManaStones())
        {
            foreach (ManaStone manaStone in sourceItem.GetItemStones())
                ItemSocketService.AddManaStone(newItem, manaStone.GetItemId(), false);
        }
        if (sourceItem.GetGodStone() != null)
            newItem.AddGodStone(sourceItem.GetGodStone().GetItemId(), sourceItem.GetGodStone().GetActivatedCount());
        newItem.SetEnchantLevel(sourceItem.GetEnchantLevel());
        newItem.SetAmplified(sourceItem.IsAmplified());
        newItem.SetBuffSkill(sourceItem.GetBuffSkill());
        newItem.SetTempering(sourceItem.GetTempering());
        newItem.SetSoulBound(sourceItem.IsSoulBound());
        newItem.SetTuneCount(sourceItem.GetTuneCount());
        newItem.SetBonusStats(sourceItem.GetBonusStatsId(), true);
        newItem.SetIdianStone(sourceItem.GetIdianStone());
        newItem.SetItemColor(sourceItem.GetItemColor());
        newItem.SetEnchantBonus(sourceItem.GetEnchantBonus());
        newItem.SetItemSkinTemplate(sourceItem.GetItemSkinTemplate());
    }

    /// <summary>Add stackable item to inventory.</summary>
    private static long AddStackableItem(Aion.GameServer.Model.GameObjects.Players.Player player, ItemTemplate itemTemplate, long count, bool allowInventoryOverflow,
        ItemUpdatePredicate predicate)
    {
        ICollection<Item> items;
        // dirty & hacky check for arrows and shards...
        if (itemTemplate.GetItemGroup() == ItemGroup.POWER_SHARDS)
        {
            Aion.GameServer.Model.GameObjects.Players.Equipment equipment = player.GetEquipment();
            items = equipment.GetEquippedItemsByItemId(itemTemplate.GetTemplateId());
            foreach (Item item in items)
            {
                if (count == 0)
                {
                    break;
                }
                count = equipment.IncreaseEquippedItemCount(item, count);
            }
        }

        Storage inventory = player.GetInventory();
        items = inventory.GetItemsByItemId(itemTemplate.GetTemplateId());
        foreach (Item item in items)
        {
            if (count == 0)
            {
                break;
            }
            count = inventory.IncreaseItemCount(item, count, predicate.GetUpdateType(item, true));
        }

        while (count > 0 && (allowInventoryOverflow || !inventory.IsFull(itemTemplate.GetExtraInventoryId())))
        {
            Item newItem = ItemFactory.NewItem(itemTemplate.GetTemplateId(), count);
            count -= newItem.GetItemCount();
            inventory.Add(newItem, predicate.GetAddType());
        }
        return count;
    }

    public class ItemUpdatePredicate
    {
        private readonly ItemUpdateType itemUpdateType;
        private readonly ItemAddType itemAddType;

        public ItemUpdatePredicate(ItemAddType itemAddType, ItemUpdateType itemUpdateType)
        {
            this.itemUpdateType = itemUpdateType;
            this.itemAddType = itemAddType;
        }

        public ItemUpdatePredicate()
            : this(ItemAddType.ITEM_COLLECT, ItemUpdateType.INC_ITEM_COLLECT)
        {
        }

        public ItemUpdateType GetUpdateType(Item item, bool isIncrease)
        {
            if (item.GetItemTemplate().IsKinah())
                return ItemPacketService.GetKinahUpdateTypeFromAddType(itemAddType, isIncrease);
            return itemUpdateType;
        }

        public ItemAddType GetAddType()
        {
            return itemAddType;
        }

        public virtual bool ChangeItem(Item item)
        {
            return true;
        }
    }
}
