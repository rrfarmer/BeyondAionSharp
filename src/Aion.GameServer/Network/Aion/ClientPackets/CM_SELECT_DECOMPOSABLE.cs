using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using ItemAddType = global::Aion.GameServer.Services.Items.ItemPacketService.ItemAddType;
using ItemUpdateType = global::Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType;
using ItemUpdatePredicate = global::Aion.GameServer.Services.Items.ItemService.ItemUpdatePredicate;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SELECT_DECOMPOSABLE (xTz).</summary>
public class CM_SELECT_DECOMPOSABLE : AionClientPacket
{
    private int objectId;
    private int index;

    public CM_SELECT_DECOMPOSABLE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        objectId = (int)ReadQ(); // 64 bit object id
        index = ReadUC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;
        Item item = player.GetInventory().GetItemByObjId(objectId);
        if (item == null)
            return;
        DecomposableItem decomposableItem = DataManager.DECOMPOSABLE_ITEMS_DATA.Get(item.GetItemId());
        // nothing ties the pick to an opened window, so it has to pass the same checks as using the item
        if (decomposableItem == null || !decomposableItem.IsSelectable() || !PlayerRestrictions.CanUseItem(player, item))
            return;
        DecomposableSet set = decomposableItem.GetSelectableSet(player);
        if (set == null || index >= set.GetItems().Count)
            return;
        DecomposedItem selectedItem = set.GetItems()[index];
        if (player.GetInventory().IsFull(DataManager.ITEM_DATA.GetItemTemplate(selectedItem.GetItemId()).GetExtraInventoryId()))
        {
            Refuse(player, item);
            return;
        }
        PacketSendUtility.BroadcastPacketAndReceive(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), objectId, item.GetItemId()));
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_USE_ITEM(item.GetL10n()));
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_UNCOMPRESS_COMPRESSED_ITEM_SUCCEEDED(item.GetL10n()));
        if (!player.GetInventory().DecreaseByObjectId(objectId, 1))
        {
            PacketSendUtility.SendPacket(player, new SM_SELECT_DECOMPOSABLE_RESULT(objectId, SM_SELECT_DECOMPOSABLE_RESULT.NOT_GRANTED));
            return;
        }
        PacketSendUtility.SendPacket(player, new SM_SELECT_DECOMPOSABLE_RESULT(objectId, SM_SELECT_DECOMPOSABLE_RESULT.GRANTED));
        ItemService.AddItem(player, selectedItem.GetItemId(), selectedItem.GetCount(), true,
            new ItemUpdatePredicate(ItemAddType.DECOMPOSABLE, ItemUpdateType.INC_ITEM_COLLECT));
        player.StartCooldown(item);
    }

    private void Refuse(Player player, Item item)
    {
        PacketSendUtility.BroadcastPacketAndReceive(player,
            new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), objectId, item.GetItemId(), 0, ItemUseAnimation.USE_FAIL));
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANT_USE_ITEM(item.GetL10n()));
        PacketSendUtility.SendPacket(player, new SM_SELECT_DECOMPOSABLE_RESULT(objectId, SM_SELECT_DECOMPOSABLE_RESULT.NOT_GRANTED));
    }
}
