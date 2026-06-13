using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity: model/gameobjects/player/Equipment implements Persistable. Ported as partials (802L).
/// Part 1: fields, ctor, equipItem + equip/unEquip core and helpers.
/// </summary>
public partial class Equipment : IPersistable
{
    private static readonly ILogger log = NullLogger.Instance;

    // Java parity: synchronizedSortedMap(TreeMap) — slot mask -> item; explicit locks guard compound ops.
    private readonly SortedDictionary<long, Item> equipment = new SortedDictionary<long, Item>();
    private readonly Player owner;
    private IPersistable.PersistentState persistentState = IPersistable.PersistentState.UPDATED;

    public Equipment(Player player)
    {
        this.owner = player;
    }

    private Item GetEquip(long slotMask)
    {
        return equipment.TryGetValue(slotMask, out Item item) ? item : null;
    }

    /// <summary>item or null in case of failure</summary>
    public Item EquipItem(int itemUniqueId, long slot)
    {
        Item item = owner.GetInventory().GetItemByObjId(itemUniqueId);
        if (item == null || item.IsEquipped())
            return null;

        Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate = item.GetItemTemplate();
        if (itemTemplate.IsTwoHandWeapon()) // client only sends main+sub slot when equipping via right click / double click
            slot = ItemSlot.MAIN_OR_SUB.GetSlotIdMask();
        else if (itemTemplate.IsOneHandWeapon() && !Aion.GameServer.SkillEngine.Effects.WeaponDualEffect.HasDualWieldEffect(owner))
            slot = ItemSlot.MAIN_HAND.GetSlotIdMask();

        if (!itemTemplate.IsClassSpecific(owner.GetPlayerClass()))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CANNOT_USE_ITEM_INVALID_CLASS());
            return null;
        }
        // don't allow to wear items of not allowed level
        int requiredLevel = itemTemplate.GetRequiredLevel(owner.GetPlayerClass());
        if (requiredLevel == -1 || requiredLevel > owner.GetLevel())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CANNOT_USE_ITEM_TOO_LOW_LEVEL_MUST_BE_THIS_LEVEL(item.GetL10n(), requiredLevel));
            return null;
        }

        sbyte levelRestrict = (sbyte)itemTemplate.GetMaxLevelRestrict(owner.GetPlayerClass());
        if (levelRestrict != 0 && owner.GetLevel() > levelRestrict)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CANNOT_USE_ITEM_TOO_HIGH_LEVEL(levelRestrict, itemTemplate.GetL10n()));
            return null;
        }

        if (itemTemplate.GetRace() != Aion.GameServer.Model.Race.PC_ALL && itemTemplate.GetRace() != owner.GetRace())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CANNOT_USE_ITEM_INVALID_RACE());
            return null;
        }

        Aion.GameServer.Model.Templates.Items.ItemUseLimits limits = itemTemplate.GetUseLimits();
        if (limits.GetGenderPermitted() != null && limits.GetGenderPermitted() != owner.GetGender())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CANNOT_USE_ITEM_INVALID_GENDER());
            return null;
        }

        if (!VerifyRankLimits(item))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CANNOT_USE_ITEM_INVALID_RANK(Aion.GameServer.Utils.Stats.AbyssRankEnumExtensions.GetRankL10n(owner.GetRace(), limits.GetMinRank())));
            return null;
        }

        if (!CheckInventorySlots(slot))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_UI_INVENTORY_FULL());
            return null;
        }

        if (!CheckAvailableEquipSkills(item))
            return null;

        ItemSlot[] targetSlots = ItemSlotExtensions.GetSlotsFor(slot);
        if (targetSlots.Length == 0)
        {
            log.LogWarning("Unknown target slot " + slot + " for " + item);
            return null;
        }

        if (targetSlots.Length == 2 && !itemTemplate.IsTwoHandWeapon() || targetSlots.Length > 2)
        {
            Aion.GameServer.Utils.Audit.AuditLogger.Log(owner, "tried to equip " + item + " in slots: " + "[" + string.Join(", ", (object[])targetSlots) + "]");
            return null;
        }

        if ((ItemSlot.MAIN_OFF_OR_SUB_OFF.GetSlotIdMask() & slot) != 0) // offhand slots cannot be directly populated on client side
        {
            Aion.GameServer.Utils.Audit.AuditLogger.Log(owner, "tried to equip " + item + " directly in offhand slot");
            return null;
        }

        long validSlotMask = itemTemplate.GetItemSlot();
        if (validSlotMask == 0) // e.g. arrows, which cannot be equipped anymore
            return null;
        if ((validSlotMask & slot) != slot) // invalid slot provided for the item
        {
            Aion.GameServer.Utils.Audit.AuditLogger.Log(owner, "tried to equip " + item + " in invalid slot(s): " + "[" + string.Join(", ", (object[])targetSlots) + "]");
            return null;
        }

        if (!Aion.GameServer.Services.StigmaService.NotifyEquipAction(owner, item, slot))
            return null;

        if (itemTemplate.IsSoulBound() && !item.IsSoulBound())
        {
            SoulBindItem(owner, item, slot);
            return null;
        }
        return Equip(slot, item);
    }

    private bool CheckInventorySlots(long itemSlotToEquip)
    {
        if (owner.GetInventory().IsFull() && ItemSlotExtensions.IsTwoHandedWeapon(itemSlotToEquip)) // weapon slot(s)
        {
            foreach (ItemSlot slot in ItemSlotExtensions.GetSlotsFor(itemSlotToEquip))
            {
                Item equippedWeaponOrShield = GetEquip(slot.GetSlotIdMask());
                if (equippedWeaponOrShield == null || equippedWeaponOrShield.GetItemTemplate().IsTwoHandWeapon())
                    return true;
            }
            return false; // two weapons would need to be unequipped, but there is no free slot
        }
        return true;
    }

    private bool CheckDualWieldRestriction(Item item, long slot)
    {
        if (item.GetItemTemplate().IsOneHandWeapon() && (slot & ItemSlot.LEFT_HAND.GetSlotIdMask()) == slot && !Aion.GameServer.SkillEngine.Effects.WeaponDualEffect.HasDualWieldEffect(owner))
            return false;
        return true;
    }

    private Item Equip(long itemSlotToEquip, Item item)
    {
        if (!item.IsIdentified())
        {
            log.LogWarning(item + " can't be equipped because it's not identified yet");
            return null;
        }

        ItemSlot[] targetSlots = ItemSlotExtensions.GetSlotsFor(itemSlotToEquip);

        lock (this)
        {
            // do unequip of necessary items
            UnEquip(GetUnequipSlots(itemSlotToEquip));
            owner.GetInventory().Remove(item);
            // equip target item
            foreach (ItemSlot slot in targetSlots)
                equipment[slot.GetSlotIdMask()] = item;
            item.SetEquipped(true);
            item.SetEquipmentSlot(itemSlotToEquip);
            Aion.GameServer.Services.Items.ItemPacketService.UpdateItemAfterEquip(owner, item);

            // update stats
            NotifyItemEquipped(item);
            owner.GetLifeStats().UpdateCurrentStats();
            owner.GetGameStats().UpdateStatsAndSpeedVisually();
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnEquipItem(new Aion.GameServer.QuestEngine.Model.QuestEnv(null, owner, 0), item.GetItemId());

            if (item.GetItemTemplate().IsStigma())
                Aion.GameServer.Services.StigmaService.AddLinkedStigmaSkills(owner);

            return item;
        }
    }

    private long GetUnequipSlots(long itemSlotToEquip)
    {
        if (itemSlotToEquip == ItemSlot.MAIN_HAND.GetSlotIdMask() || itemSlotToEquip == ItemSlot.SUB_HAND.GetSlotIdMask())
        {
            Item equippedItem = GetEquip(itemSlotToEquip);
            if (equippedItem != null && equippedItem.GetItemTemplate().IsTwoHandWeapon())
                return ItemSlot.MAIN_OR_SUB.GetSlotIdMask(); // two-handed occupies two slots, so we need to unequip both
        }
        return itemSlotToEquip;
    }

    private void NotifyItemEquipped(Item item)
    {
        Aion.GameServer.Model.Stats.Listeners.ItemEquipmentListener.OnItemEquipment(item, owner);
        owner.GetObserveController().NotifyItemEquip(item, owner);
        TryUpdateSummonStats();
    }

    private void NotifyItemUnequip(Item item)
    {
        Aion.GameServer.Model.Stats.Listeners.ItemEquipmentListener.OnItemUnequipment(item, owner);
        owner.GetObserveController().NotifyItemUnEquip(item, owner);
        TryUpdateSummonStats();
    }

    private void TryUpdateSummonStats()
    {
        Summon summon = owner.GetSummon();
        if (summon != null)
        {
            summon.GetGameStats().UpdateStatsAndSpeedVisually();
        }
    }

    /// <summary>Called when CM_EQUIP_ITEM packet arrives with action 1. Returns item or null in case of failure.</summary>
    public Item UnEquipItem(int itemObjId, bool checkFullInventory)
    {
        // if inventory is full unequip action is disabled
        if (checkFullInventory && owner.GetInventory().IsFull())
            return null;

        lock (this)
        {
            Item itemToUnequip = GetEquippedItemByObjId(itemObjId);
            if (itemToUnequip == null || !itemToUnequip.IsEquipped())
                return null;

            // Looks very odd - but its retail like
            if (itemToUnequip.GetEquipmentSlot() == ItemSlot.MAIN_HAND.GetSlotIdMask())
            {
                Item ohWeapon = GetEquip(ItemSlot.SUB_HAND.GetSlotIdMask());
                if (ohWeapon != null && ohWeapon.GetItemTemplate().IsWeapon())
                {
                    if (owner.GetInventory().GetFreeSlots() < 2)
                    {
                        return null;
                    }
                    UnEquip(ItemSlot.SUB_HAND.GetSlotIdMask());
                }
            }

            // if unequip power shard
            if (itemToUnequip.GetItemTemplate().GetItemGroup() == Aion.GameServer.Model.Templates.Items.Enums.ItemGroup.POWER_SHARDS)
            {
                owner.UnsetState(CreatureState.POWERSHARD);
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmEmotion(owner, Aion.GameServer.Model.EmotionType.POWERSHARD_OFF, 0, 0));
            }

            if (itemToUnequip.GetItemTemplate().IsStigma())
                Aion.GameServer.Services.StigmaService.RemoveStigmaSkills(owner, itemToUnequip.GetItemTemplate().GetStigma(), itemToUnequip.GetEnchantLevel(), true);

            UnEquip(itemToUnequip.GetEquipmentSlot());

            return itemToUnequip;
        }
    }

    public Item UnEquipItem(int itemObjId)
    {
        return UnEquipItem(itemObjId, true);
    }

    /// <param name="slot">Must be composite for dual weapons</param>
    private void UnEquip(long slot)
    {
        bool updateStats = false;
        ItemSlot[] allSlots = ItemSlotExtensions.GetSlotsFor(slot);
        foreach (ItemSlot itemSlot in allSlots)
        {
            if (!equipment.Remove(itemSlot.GetSlotIdMask(), out Item item))
                item = null;
            if (item == null || !item.IsEquipped()) // check isEquipped to avoid duplicate notifyUnequip, since two handed weapons occupy two slots
                continue;
            updateStats = true;
            item.SetEquipped(false);
            item.SetEquipmentSlot(0);
            owner.GetInventory().Put(item);
            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            NotifyItemUnequip(item);
        }
        if (updateStats)
        {
            owner.GetLifeStats().UpdateCurrentStats();
            owner.GetGameStats().UpdateStatsAndSpeedVisually();
        }
    }

    private void Unequip(Item item)
    {
        if (item.GetItemTemplate().IsTwoHandWeapon())
        {
            foreach (ItemSlot slot in ItemSlotExtensions.GetSlotsFor(item.GetEquipmentSlot()))
                equipment.Remove(slot.GetSlotIdMask());
        }
        else
        {
            equipment.Remove(item.GetEquipmentSlot());
        }
        item.SetEquipped(false);
    }

    private bool CheckAvailableEquipSkills(Item item)
    {
        int[] requiredSkills = item.GetItemTemplate().GetRequiredSkills();
        if (requiredSkills.Length == 0) // if no skills required - validate as true
            return true;

        foreach (int skill in requiredSkills)
        {
            if (owner.GetSkillList().IsSkillPresent(skill))
                return true;
        }

        return false; // FIXME leather skill allows you to wear leather. You don't need cloth skill too!
    }

    public Item GetEquippedItemByObjId(int itemObjId)
    {
        lock (equipment)
        {
            foreach (Item item in equipment.Values)
            {
                if (item.GetObjectId() == itemObjId)
                    return item;
            }
        }
        return null;
    }
}
