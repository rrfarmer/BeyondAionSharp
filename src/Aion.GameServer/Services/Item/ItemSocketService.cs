using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Model.Templates.Item.Enums;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Trade;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using PersistentState = Aion.GameServer.Model.GameObjects.Persistable.PersistentState;

namespace Aion.GameServer.Services.Item;

/// <summary>Java parity: services/item/ItemSocketService (ATracer, Sykra). Manastone/fusion-stone socketing: addManaStone (next slot or specific slot, special/normal slot accounting), copyFusionStones, removeManastone (kinah cost), removeAllManastone, socketGodstone (move-cancel listener + 2s cast). Set->ISet/HashSet; stream.filter.findFirst.orElse->LINQ; Collections.singleton->new HashSet; anonymous StartMovingListener->nested GodstoneMoveListener (super.moved->base.Moved); anonymous Runnable->Schedule ct-lambda. ManaStone/templates/SM_/DAO red-tolerated.</summary>
public class ItemSocketService
{
    public static ManaStone AddManaStone(Item item, int manaStoneItemId, bool useFusionSlots)
    {
        if (item == null)
            return null;
        int maxSlots = item.GetSockets(useFusionSlots);
        ISet<ManaStone> manaStones = useFusionSlots ? item.GetFusionStones() : item.GetItemStones();
        if (manaStones.Count > maxSlots)
            return null;
        ItemGroup manaStoneCategory = DataManager.ITEM_DATA.GetItemTemplate(manaStoneItemId).GetItemGroup();
        int specialSlotCount = useFusionSlots ? item.GetFusionedItemTemplate().GetSpecialSlots() : item.GetItemTemplate().GetSpecialSlots();
        return InsertManaStoneIntoNextPossibleSlot(item, manaStoneItemId, maxSlots, manaStones, manaStoneCategory, specialSlotCount);
    }

    public static ManaStone AddManaStone(Item item, int manaStoneItemId, int slotId, bool useFusionSlots)
    {
        if (item == null)
            return null;
        ISet<ManaStone> manaStones = useFusionSlots ? item.GetFusionStones() : item.GetItemStones();
        if (manaStones.Count >= Item.MAX_BASIC_STONES)
            return null;
        return InsertManastoneIntoSlot(item, manaStones, manaStoneItemId, slotId);
    }

    private static ManaStone InsertManaStoneIntoNextPossibleSlot(Item item, int manaStoneItemId, int maxSlots, ISet<ManaStone> manaStones,
        ItemGroup manastoneCategory, int specialSlotCount)
    {
        if (manastoneCategory == ItemGroup.SPECIAL_MANASTONE && specialSlotCount == 0)
            return null;

        int specialSlotsOccupied = 0;
        int normalSlotsOccupied = 0;
        HashSet<int> allSlots = new HashSet<int>();
        foreach (ManaStone ms in manaStones)
        {
            ItemGroup category = DataManager.ITEM_DATA.GetItemTemplate(ms.GetItemId()).GetItemGroup();
            if (category == ItemGroup.SPECIAL_MANASTONE)
                specialSlotsOccupied++;
            else
                normalSlotsOccupied++;
            allSlots.Add(ms.GetSlot());
        }

        if ((manastoneCategory == ItemGroup.SPECIAL_MANASTONE && specialSlotsOccupied >= specialSlotCount)
            || (manastoneCategory == ItemGroup.MANASTONE && normalSlotsOccupied >= (maxSlots - specialSlotCount)))
            return null;

        int start = manastoneCategory == ItemGroup.SPECIAL_MANASTONE ? 0 : specialSlotCount;
        int end = manastoneCategory == ItemGroup.SPECIAL_MANASTONE ? specialSlotCount : maxSlots;
        int nextSlot = start;
        bool slotFound = false;
        for (; nextSlot < end; nextSlot++)
        {
            if (!allSlots.Contains(nextSlot))
            {
                slotFound = true;
                break;
            }
        }
        if (!slotFound)
            return null;
        return InsertManastoneIntoSlot(item, manaStones, manaStoneItemId, nextSlot);
    }

    private static ManaStone InsertManastoneIntoSlot(Item item, ISet<ManaStone> manaStones, int manastoneId, int slotId)
    {
        item.RemoveRemainingTuningCountIfPossible();
        ManaStone stone = new ManaStone(item.GetObjectId(), manastoneId, slotId, PersistentState.NEW);
        manaStones.Add(stone);
        return stone;
    }

    public static void CopyFusionStones(Item source, Item target)
    {
        if (source.HasManaStones())
        {
            foreach (ManaStone manaStone in source.GetItemStones())
                target.GetFusionStones().Add(new ManaStone(target.GetObjectId(), manaStone.GetItemId(), manaStone.GetSlot(), PersistentState.NEW));
            target.RemoveRemainingTuningCountIfPossible();
        }
    }

    public static void RemoveManastone(Player player, int itemObjId, int slotNum, bool isFusionSocket)
    {
        Storage inventory = player.GetInventory();
        Item item = inventory.GetItemByObjId(itemObjId);
        if (item == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REMOVE_ITEM_OPTION_NO_TARGET_ITEM());
            return;
        }

        bool hasManaStones = isFusionSocket ? item.HasFusionStones() : item.HasManaStones();
        if (!hasManaStones)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REMOVE_ITEM_OPTION_NO_OPTION_TO_REMOVE(item.GetL10n()));
            return;
        }

        ISet<ManaStone> itemStones = isFusionSocket ? item.GetFusionStones() : item.GetItemStones();
        ManaStone manaStoneToRemove = itemStones.Where(ms => ms.GetSlot() == slotNum).FirstOrDefault();
        if (manaStoneToRemove == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REMOVE_ITEM_OPTION_INVALID_OPTION_SLOT_NUMBER(item.GetL10n()));
            return;
        }

        long price = PricesService.GetPriceForService(650, player.GetRace());
        if (player.GetInventory().TryDecreaseKinah(price))
        {
            manaStoneToRemove.SetPersistentState(PersistentState.DELETED);
            if (isFusionSocket)
            {
                ItemStoneListDAO.StoreFusionStone(new HashSet<ManaStone> { manaStoneToRemove });
            }
            else
            {
                ItemStoneListDAO.StoreManaStones(new HashSet<ManaStone> { manaStoneToRemove });
            }
            itemStones.Remove(manaStoneToRemove);

            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REMOVE_ITEM_OPTION_SUCCEED(item.GetL10n()));
            ItemPacketService.UpdateItemAfterInfoChange(player, item);
        }
        else
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REMOVE_ITEM_OPTION_NOT_ENOUGH_GOLD(item.GetL10n()));
        }
    }

    public static void RemoveAllManastone(Player player, Item item)
    {
        if (item == null || !item.HasManaStones())
            return;

        ISet<ManaStone> itemStones = item.GetItemStones();
        foreach (ManaStone ms in itemStones)
            ms.SetPersistentState(PersistentState.DELETED);
        ItemStoneListDAO.StoreManaStones(itemStones);
        itemStones.Clear();

        ItemPacketService.UpdateItemAfterInfoChange(player, item);
    }

    public static void SocketGodstone(Player player, Item weapon, int stoneId)
    {
        if (weapon == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_NO_TARGET_ITEM());
            return;
        }

        if (!weapon.CanSocketGodstone())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_NOT_PROC_GIVABLE_ITEM(weapon.GetL10n()));
            AuditLogger.Log(player, "tried to insert godstone in not compatible item " + weapon.GetItemId());
            return;
        }

        StartMovingListener move = new GodstoneMoveListener(player, weapon);

        player.GetObserveController().Attach(move);

        Item godstone = player.GetInventory().GetItemByObjId(stoneId);
        if (godstone == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_NO_PROC_GIVE_ITEM());
            return;
        }

        ItemTemplate itemTemplate = godstone.GetItemTemplate();
        if (itemTemplate.GetGodstoneInfo() == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_NO_PROC_GIVE_ITEM());
            return;
        }

        PacketSendUtility.BroadcastPacketAndReceive(player,
            new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), stoneId, itemTemplate.GetTemplateId(), 2000, 0, 0));

        player.GetController().AddTask(TaskId.ITEM_USE, ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(move);

            PacketSendUtility.BroadcastPacketAndReceive(player,
                new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), stoneId, itemTemplate.GetTemplateId(), 0, 1, 0));

            if (!player.GetInventory().DecreaseByObjectId(stoneId, 1))
                return ValueTask.CompletedTask;

            weapon.AddGodStone(itemTemplate.GetTemplateId());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_ENCHANTED_TARGET_ITEM(weapon.GetL10n()));

            ItemPacketService.UpdateItemAfterInfoChange(player, weapon);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(2000)));
    }

    private sealed class GodstoneMoveListener : StartMovingListener
    {
        private readonly Player player;
        private readonly Item weapon;

        public GodstoneMoveListener(Player player, Item weapon)
        {
            this.player = player;
            this.weapon = weapon;
        }

        public override void Moved()
        {
            base.Moved();
            player.GetObserveController().RemoveObserver(this);
            player.GetController().CancelUseItem();
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_GIVE_PROC_CANCEL(weapon.GetL10n()));
        }
    }
}
