using System.Collections.Generic;
using Aion.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using ItemAddType = Aion.GameServer.Services.Items.ItemPacketService.ItemAddType;
using ItemUpdateType = Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType;
using ItemUpdatePredicate = Aion.GameServer.Services.Items.ItemService.ItemUpdatePredicate;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SELECT_DECOMPOSABLE (xTz). Picks a selectable result from a decomposable item and grants it. DataManager.DECOMPOSABLE_ITEMS_DATA/ItemService red-tolerated.</summary>
public class CM_SELECT_DECOMPOSABLE : AionClientPacket
{
    private int objectId;
    private int unk;
    private int index;

    public CM_SELECT_DECOMPOSABLE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        objectId = ReadD();
        unk = ReadD();
        index = ReadUC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player != null)
        {

            Item item = player.GetInventory().GetItemByObjId(objectId);
            if (item != null)
            {
                List<ResultedItem> selectableItems = DataManager.DECOMPOSABLE_ITEMS_DATA.GetSelectableItems(item.GetItemId());
                if (selectableItems == null)
                {
                    return;
                }
                selectableItems.RemoveAll(i => !i.IsObtainableFor(player));
                if (index + 1 > selectableItems.Count)
                {
                    return;
                }
                PacketSendUtility.BroadcastPacketAndReceive(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), objectId, item.GetItemId()));
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_UNCOMPRESS_COMPRESSED_ITEM_SUCCEEDED(item.GetL10n()));
                player.GetInventory().DecreaseByObjectId(objectId, 1);
                PacketSendUtility.SendPacket(player, new SM_SECONDARY_SHOW_DECOMPOSABLE(objectId, new List<ResultedItem>())); // TODO
                ResultedItem selectedItem = selectableItems[index];
                int count = Rnd.Get(selectedItem.GetMinCount(), selectedItem.GetMaxCount());
                ItemService.AddItem(player, selectedItem.GetItemId(), count, true, new ItemUpdatePredicate(ItemAddType.DECOMPOSABLE, ItemUpdateType.INC_ITEM_COLLECT));
            }
        }
    }
}
