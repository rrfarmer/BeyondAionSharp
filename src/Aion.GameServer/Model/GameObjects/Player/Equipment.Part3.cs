using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Items;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity: model/gameobjects/player/Equipment — partial #3 (Java ~538-697): power-shard use,
/// equipped item count change, switchHands, weapon-equipped checks, main/off-hand getters, Persistable impl.
/// </summary>
public partial class Equipment
{
    public void UsePowerShard(Item powerShardItem, int count)
    {
        DecreaseEquippedItemCount(powerShardItem.GetObjectId(), count);

        if (powerShardItem.GetItemCount() <= 0) // Search for next same power shards stack
        {
            List<Item> powerShardStacks = owner.GetInventory().GetItemsByItemId(powerShardItem.GetItemTemplate().GetTemplateId());
            if (powerShardStacks.Count != 0)
            {
                EquipItem(powerShardStacks[0].GetObjectId(), powerShardItem.GetEquipmentSlot());
            }
            else
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_WEAPON_BOOST_MODE_BURN_OUT());
                owner.UnsetState(CreatureState.Powershard);
            }
        }
    }

    /// <summary>increase item count and return left count</summary>
    public long IncreaseEquippedItemCount(Item item, long count)
    {
        // Only Shards can be increased
        if (item.GetItemTemplate().GetItemGroup() != Aion.GameServer.Model.Templates.Item.Enums.ItemGroup.POWER_SHARDS)
            return count;

        long leftCount = item.IncreaseItemCount(count);
        Aion.GameServer.Services.Item.ItemPacketService.UpdateItemAfterInfoChange(owner, item, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.STATS_CHANGE);
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
        return leftCount;
    }

    public void DecreaseEquippedItemCount(int itemObjId, int count)
    {
        Item equippedItem = GetEquippedItemByObjId(itemObjId);

        if (equippedItem.GetItemCount() >= count)
            equippedItem.DecreaseItemCount(count);
        else
            equippedItem.DecreaseItemCount(equippedItem.GetItemCount());

        if (equippedItem.GetItemCount() == 0)
        {
            Aion.GameServer.Dao.InventoryDAO.Store(equippedItem, owner); // must store (delete) before unequip
            Unequip(equippedItem);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmDeleteItem(equippedItem.GetObjectId()));
        }
        else
        {
            Aion.GameServer.Services.Item.ItemPacketService.UpdateItemAfterInfoChange(owner, equippedItem, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.STATS_CHANGE);
        }
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmUpdatePlayerAppearance(owner.GetObjectId(), owner.GetEquipment().GetEquippedForAppearance()), true);
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    /// <summary>Switch OFF and MAIN hands.</summary>
    public void SwitchHands()
    {
        Item mainHandItem = GetEquip(ItemSlot.MAIN_HAND.GetSlotIdMask());
        Item subHandItem = GetEquip(ItemSlot.SUB_HAND.GetSlotIdMask());
        Item mainOffHandItem = GetEquip(ItemSlot.MAIN_OFF_HAND.GetSlotIdMask());
        Item subOffHandItem = GetEquip(ItemSlot.SUB_OFF_HAND.GetSlotIdMask());

        List<Item> equippedWeapon = new List<Item>();

        if (mainHandItem != null)
            equippedWeapon.Add(mainHandItem);
        if (subHandItem != null && subHandItem != mainHandItem)
            equippedWeapon.Add(subHandItem);
        if (mainOffHandItem != null)
            equippedWeapon.Add(mainOffHandItem);
        if (subOffHandItem != null && subOffHandItem != mainOffHandItem)
            equippedWeapon.Add(subOffHandItem);

        foreach (Item item in equippedWeapon)
        {
            Unequip(item);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(owner, new Aion.GameServer.Network.Aion.ServerPackets.SmInventoryUpdateItem(owner, item, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType.EQUIP_UNEQUIP));
            if (owner.GetGameStats() != null)
            {
                if ((item.GetEquipmentSlot() & ItemSlot.MAIN_HAND.GetSlotIdMask()) != 0
                    || (item.GetEquipmentSlot() & ItemSlot.SUB_HAND.GetSlotIdMask()) != 0)
                {
                    NotifyItemUnequip(item);
                }
            }
        }

        foreach (Item item in equippedWeapon)
        {
            long oldSlots = item.GetEquipmentSlot();
            if ((oldSlots & ItemSlot.RIGHT_HAND.GetSlotIdMask()) != 0)
                oldSlots ^= ItemSlot.RIGHT_HAND.GetSlotIdMask();
            if ((oldSlots & ItemSlot.LEFT_HAND.GetSlotIdMask()) != 0)
                oldSlots ^= ItemSlot.LEFT_HAND.GetSlotIdMask();
            item.SetEquipmentSlot(oldSlots);
        }

        foreach (Item item in equippedWeapon)
        {
            if (item.GetItemTemplate().IsTwoHandWeapon())
            {
                ItemSlot[] slots = ItemSlotExtensions.GetSlotsFor(item.GetEquipmentSlot());
                foreach (ItemSlot slot in slots)
                    equipment[slot.GetSlotIdMask()] = item;
            }
            else
            {
                equipment[item.GetEquipmentSlot()] = item;
            }
            item.SetEquipped(true);
            Aion.GameServer.Services.Item.ItemPacketService.UpdateItemAfterEquip(owner, item);
        }

        if (owner.GetGameStats() != null)
        {
            foreach (Item item in equippedWeapon)
            {
                if ((item.GetEquipmentSlot() & ItemSlot.MAIN_HAND.GetSlotIdMask()) != 0
                    || (item.GetEquipmentSlot() & ItemSlot.SUB_HAND.GetSlotIdMask()) != 0)
                {
                    NotifyItemEquipped(item);
                }
            }
        }

        owner.GetLifeStats().UpdateCurrentStats();
        owner.GetGameStats().UpdateStatsAndSpeedVisually();
        SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
    }

    public bool IsWeaponEquipped(Aion.GameServer.Model.Templates.Item.Enums.ItemSubType subType)
    {
        Item weapon = GetMainHandWeapon();
        if (weapon != null && weapon.GetItemTemplate().GetItemSubType() == subType)
            return true;
        weapon = GetOffHandWeapon();
        if (weapon != null && weapon.GetItemTemplate().GetItemSubType() == subType)
            return true;
        return false;
    }

    public bool IsDualWeaponEquipped()
    {
        foreach (ItemSlot offhandSlot in new[] { ItemSlot.SUB_HAND, ItemSlot.MAIN_HAND })
        {
            Item weapon = GetEquip(offhandSlot.GetSlotIdMask());
            if (weapon == null || !weapon.GetItemTemplate().IsOneHandWeapon())
                return false;
        }
        return true;
    }

    /// <summary>Only used for new Player creation. Although invalid, but fits its purpose.</summary>
    public bool IsSlotEquipped(long slot)
    {
        return GetEquip(slot) != null;
    }

    public Item GetMainHandWeapon()
    {
        return GetEquip(ItemSlot.MAIN_HAND.GetSlotIdMask());
    }

    public Item GetOffHandWeapon()
    {
        Item result = GetEquip(ItemSlot.SUB_HAND.GetSlotIdMask());
        if (GetMainHandWeapon() == result)
            return null;
        return result;
    }

    public IPersistable.PersistentState GetPersistentState()
    {
        return persistentState;
    }

    public void SetPersistentState(IPersistable.PersistentState persistentState)
    {
        this.persistentState = persistentState;
    }
}
