using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/DecomposeAction (oslo(a00441234)).</summary>
[XmlType("DecomposeAction")]
public class DecomposeAction : AbstractItemAction
{
    public override bool CanAct(Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (player.IsDead() || !player.IsSpawned())
            return false;
        DecomposableItem decomposableItem = DataManager.DECOMPOSABLE_ITEMS_DATA.Get(parentItem.GetItemId());
        if (decomposableItem == null)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_IT_CAN_NOT_BE_DECOMPOSED(parentItem.GetL10n()));
            return false;
        }
        if (player.GetInventory().IsFull() || player.GetInventory().IsFullSpecialCube() && ContainsSpecialCubeItems(decomposableItem, player))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WAREHOUSE_FULL_INVENTORY());
            return false;
        }
        return true;
    }

    public override void Act(Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        DecomposableItem decomposableItem = DataManager.DECOMPOSABLE_ITEMS_DATA.Get(parentItem.GetItemId());
        if (decomposableItem.IsSelectable())
        {
            DecomposableSet set = decomposableItem.GetSelectableSet(player);
            if (set == null)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANT_USE_ITEM(parentItem.GetL10n()));
            else
                PacketSendUtility.SendPacket(player, new SM_SHOW_DECOMPOSABLE(parentItem.GetObjectId(), set.GetItems()));
            return;
        }
        int castingDelay = player.GetItemUseDelay(parentItem.GetItemTemplate().GetCastingDelay());
        if (castingDelay <= 0)
        {
            FinishUse(player, parentItem, targetItem, decomposableItem);
            return;
        }
        PacketSendUtility.BroadcastPacket(player,
            new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), castingDelay, ItemUseAnimation.USE_START), true);

        ItemUseObserver observer = new DecomposeUseObserver(player, parentItem);

        player.GetObserveController().AddObserver(observer);
        player.GetController().AddTask(TaskId.ITEM_USE, ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            player.GetObserveController().RemoveObserver(observer);
            FinishUse(player, parentItem, targetItem, decomposableItem);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(castingDelay)));
    }

    private bool PostValidate(Player player, Item parentItem, Item targetItem)
    {
        if (!CanAct(player, parentItem, targetItem))
        {
            return false;
        }
        if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_DECOMPOSE_ITEM_NO_TARGET_ITEM());
            return false;
        }
        return true;
    }

    private void FinishUse(Player player, Item parentItem, Item targetItem, DecomposableItem decomposableItem)
    {
        Dictionary<int, long> rewards = MergeRewards(decomposableItem.Decompose(player));
        bool obtainedRewards = rewards.Count > 0 && !OwnsLoreReward(player, rewards);
        bool validAction = obtainedRewards && PostValidate(player, parentItem, targetItem);
        if (validAction)
        {
            player.StartCooldown(parentItem);
            foreach (KeyValuePair<int, long> reward in rewards)
                ItemService.AddItem(player, reward.Key, reward.Value, true,
                    new ItemService.ItemUpdatePredicate(ItemPacketService.ItemAddType.DECOMPOSABLE, ItemPacketService.ItemUpdateType.INC_ITEM_COLLECT));
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_UNCOMPRESS_COMPRESSED_ITEM_SUCCEEDED(parentItem.GetL10n()));
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_USE_ITEM(parentItem.GetL10n()));
        }
        else if (!obtainedRewards) // nothing was rolled, or the reward was voided by the lore check that already reported itself
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANT_USE_ITEM(parentItem.GetL10n()));
        }
        PacketSendUtility.BroadcastPacket(player,
            new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0,
                validAction ? ItemUseAnimation.USE_SUCCESS : ItemUseAnimation.USE_FAIL), true);
    }

    /// <summary>Branches that hit the same item stack their counts instead of granting it twice.</summary>
    private Dictionary<int, long> MergeRewards(List<DecomposedItem> rewards)
    {
        // Java LinkedHashMap: insertion ordered, which a C# Dictionary also is for an add-only map.
        Dictionary<int, long> merged = new();
        foreach (DecomposedItem reward in rewards)
            merged[reward.GetItemId()] = merged.TryGetValue(reward.GetItemId(), out long count) ? count + reward.GetCount() : reward.GetCount();
        return merged;
    }

    /// <summary>A limited possession item the player already owns voids the whole decompose, the parent item is not consumed.</summary>
    private bool OwnsLoreReward(Player player, Dictionary<int, long> rewards)
    {
        foreach (int itemId in rewards.Keys)
        {
            ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(itemId);
            if (template.HasLimitOne() && (player.GetInventory().GetFirstItemByItemId(itemId) != null
                || player.GetStorage(Aion.GameServer.Model.Items.Storage.StorageType.REGULAR_WAREHOUSE.GetId()).GetFirstItemByItemId(itemId) != null))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CAN_NOT_DISASSEMBLE_LORE_ITEM(template.GetL10n()!));
                return true;
            }
        }
        return false;
    }

    private bool ContainsSpecialCubeItems(DecomposableItem decomposableItem, Player player)
    {
        foreach (DecomposableSet set in decomposableItem.GetSets())
        {
            if (!set.IsApplicableTo(player))
                continue;
            foreach (DecomposedItem item in set.GetItems())
            {
                if (DataManager.ITEM_DATA.GetItemTemplate(item.GetItemId()).GetExtraInventoryId() > 0)
                    return true;
            }
        }
        return false;
    }

    private sealed class DecomposeUseObserver : ItemUseObserver
    {
        private readonly Player player;
        private readonly Item parentItem;

        public DecomposeUseObserver(Player player, Item parentItem)
            : base(player)
        {
            this.player = player;
            this.parentItem = parentItem;
        }

        protected override void OnAbort()
        {
            player.GetController().CancelTask(TaskId.ITEM_USE);
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_UNCOMPRESS_COMPRESSED_ITEM_CANCELED(parentItem.GetL10n()));
            PacketSendUtility.BroadcastPacket(player,
                new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId(), 0,
                    ItemUseAnimation.USE_CANCEL), true);
        }
    }
}
