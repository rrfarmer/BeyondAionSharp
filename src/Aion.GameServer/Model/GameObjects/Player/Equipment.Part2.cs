using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity: model/gameobjects/player/Equipment — partial #2 (Java ~327-535): equipped-item queries,
/// stigma/appearance filters, item-set part count, load handlers, shield/weapon/power-shard getters.
/// </summary>
public partial class Equipment
{
    public List<Item> GetEquippedItemsByItemId(int value)
    {
        List<Item> equippedItemsById = new List<Item>();
        lock (equipment)
        {
            foreach (Item item in equipment.Values)
            {
                if (item.GetItemTemplate().GetTemplateId() == value)
                    equippedItemsById.Add(item);
            }
        }
        return equippedItemsById;
    }

    public List<Item> GetEquippedItems()
    {
        lock (equipment)
        {
            return equipment.Values.Distinct().ToList();
        }
    }

    public ISet<int> GetEquippedItemIds()
    {
        lock (equipment)
        {
            return equipment.Values.Select(i => i.GetItemId()).ToHashSet();
        }
    }

    public List<Item> GetEquippedItemsWithoutStigma()
    {
        List<Item> equippedItems = new List<Item>();
        HashSet<Item> twoHanded = new HashSet<Item>();
        lock (equipment)
        {
            foreach (Item item in equipment.Values)
            {
                if (!ItemSlotExtensions.IsStigma(item.GetEquipmentSlot()))
                {
                    if (item.GetItemTemplate().IsTwoHandWeapon() && !twoHanded.Add(item))
                        continue;
                    equippedItems.Add(item);
                }
            }
        }
        return equippedItems;
    }

    public List<Item> GetEquippedForAppearance()
    {
        List<Item> equippedItems = new List<Item>();
        lock (equipment)
        {
            foreach (Item item in equipment.Values)
            {
                if (ItemSlotExtensions.IsVisible(item.GetEquipmentSlot()) && !(item.GetItemTemplate().IsTwoHandWeapon() && equippedItems.Contains(item)))
                    equippedItems.Add(item);
            }
        }
        return equippedItems;
    }

    public List<Item> GetEquippedItemsAllStigma()
    {
        List<Item> equippedItems = new List<Item>();
        lock (equipment)
        {
            foreach (Item item in equipment.Values)
            {
                if (ItemSlotExtensions.IsStigma(item.GetEquipmentSlot()))
                {
                    equippedItems.Add(item);
                }
            }
        }
        return equippedItems;
    }

    public List<Item> GetEquippedItemsRegularStigma()
    {
        List<Item> equippedItems = new List<Item>();
        lock (equipment)
        {
            foreach (Item item in equipment.Values)
            {
                if (ItemSlotExtensions.IsRegularStigma(item.GetEquipmentSlot()))
                    equippedItems.Add(item);
            }
        }
        return equippedItems;
    }

    public List<Item> GetEquippedItemsAdvancedStigma()
    {
        List<Item> equippedItems = new List<Item>();
        lock (equipment)
        {
            foreach (Item item in equipment.Values)
            {
                if (ItemSlotExtensions.IsAdvancedStigma(item.GetEquipmentSlot()))
                {
                    equippedItems.Add(item);
                }
            }
        }
        return equippedItems;
    }

    /// <summary>Number of parts equipped belonging to requested itemset.</summary>
    public int ItemSetPartsEquipped(int itemSetTemplateId)
    {
        int number = 0;
        List<int> counted = new List<int>(); // no double counting for accessory and weapons

        lock (equipment)
        {
            foreach (Item item in equipment.Values)
            {
                if ((item.GetEquipmentSlot() & ItemSlot.MAIN_OFF_HAND.GetSlotIdMask()) != 0
                    || (item.GetEquipmentSlot() & ItemSlot.SUB_OFF_HAND.GetSlotIdMask()) != 0)
                {
                    continue;
                }
                Aion.GameServer.Model.Templates.Itemset.ItemSetTemplate setTemplate = item.GetItemTemplate().GetItemSet();
                if (setTemplate != null && setTemplate.GetId() == itemSetTemplateId && !counted.Contains(item.GetItemId()))
                {
                    counted.Add(item.GetItemId());
                    ++number;
                }
            }
        }
        return number;
    }

    /// <summary>Should be called only when loading from DB for items isEquipped=1.</summary>
    public void OnLoadHandler(Item item)
    {
        if (!CheckAvailableEquipSkills(item))
        {
            PutItemBackToInventory(item);
            return;
        }
        if (!CheckDualWieldRestriction(item, item.GetEquipmentSlot()))
        {
            PutItemBackToInventory(item);
            return;
        }
        foreach (ItemSlot slot in ItemSlotExtensions.GetSlotsFor(item.GetEquipmentSlot())) // two slots (main+sub) for two-handed weapons
        {
            if (equipment.ContainsKey(slot.GetSlotIdMask()))
            {
                log.LogWarning("Duplicate equipped item in slot " + slot + " for " + owner);
                PutItemBackToInventory(item);
            }
            else
            {
                equipment[slot.GetSlotIdMask()] = item;
            }
        }
    }

    private void PutItemBackToInventory(Item item)
    {
        item.SetEquipped(false);
        item.SetEquipmentSlot(0);
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        owner.GetInventory().Put(item);
    }

    /// <summary>
    /// Should be called only when equipment object totally constructed on player loading. Applies every equipped item stats modificators.
    /// </summary>
    public void OnLoadApplyEquipmentStats()
    {
        Item twoHanded = null;
        lock (equipment)
        {
            foreach (Item item in equipment.Values)
            {
                if ((item.GetEquipmentSlot() & ItemSlot.MAIN_OFF_HAND.GetSlotIdMask()) == 0
                    && (item.GetEquipmentSlot() & ItemSlot.SUB_OFF_HAND.GetSlotIdMask()) == 0)
                {
                    if (item.GetItemTemplate().IsTwoHandWeapon())
                    {
                        if (twoHanded != null)
                            continue;
                        twoHanded = item;
                    }
                    Aion.GameServer.Model.Stats.Listeners.ItemEquipmentListener.OnItemEquipment(item, owner);
                }
            }
        }
        owner.GetLifeStats().SynchronizeWithMaxStats();
    }

    public bool IsShieldEquipped()
    {
        Item subHandItem = GetEquip(ItemSlot.SUB_HAND.GetSlotIdMask());
        if (subHandItem == null)
            return false;
        Aion.GameServer.Model.Templates.Items.Enums.ItemSubType shieldType = subHandItem.GetItemTemplate().GetItemSubType();
        return shieldType == Aion.GameServer.Model.Templates.Items.Enums.ItemSubType.SHIELD;
    }

    public Item GetEquippedShield()
    {
        Item subHandItem = GetEquip(ItemSlot.SUB_HAND.GetSlotIdMask());
        if (subHandItem == null)
            return null;
        Aion.GameServer.Model.Templates.Items.Enums.ItemSubType shieldType = subHandItem.GetItemTemplate().GetItemSubType();
        return (shieldType == Aion.GameServer.Model.Templates.Items.Enums.ItemSubType.SHIELD) ? subHandItem : null;
    }

    /// <summary>WeaponType of current weapon in main hand or null.</summary>
    public Aion.GameServer.Model.Templates.Items.Enums.ItemGroup? GetMainHandWeaponType()
    {
        Item mainHandItem = GetEquip(ItemSlot.MAIN_HAND.GetSlotIdMask());
        if (mainHandItem == null)
            return null;

        return mainHandItem.GetItemTemplate().GetItemGroup();
    }

    /// <summary>WeaponType of current weapon in off hand or null.</summary>
    public Aion.GameServer.Model.Templates.Items.Enums.ItemGroup? GetOffHandWeaponType()
    {
        Item offHandItem = GetEquip(ItemSlot.SUB_HAND.GetSlotIdMask());
        Item mainHandItem = GetEquip(ItemSlot.MAIN_HAND.GetSlotIdMask());
        if (mainHandItem == offHandItem)
            offHandItem = null;
        if (offHandItem != null && offHandItem.GetItemTemplate().IsWeapon())
            return offHandItem.GetItemTemplate().GetItemGroup();

        return null;
    }

    public bool IsPowerShardEquipped()
    {
        return GetMainHandPowerShard() != null || GetOffHandPowerShard() != null;
    }

    public Item GetMainHandPowerShard()
    {
        return GetEquip(ItemSlot.POWER_SHARD_RIGHT.GetSlotIdMask());
    }

    public Item GetOffHandPowerShard()
    {
        return GetEquip(ItemSlot.POWER_SHARD_LEFT.GetSlotIdMask());
    }
}
