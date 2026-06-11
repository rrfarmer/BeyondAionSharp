using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using ItemDeleteType = Aion.GameServer.Services.Items.ItemPacketService.ItemDeleteType;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_DELETE_ITEM (Avol). Discards an inventory item unless it is unbreakable. Storage/ItemDeleteType red-tolerated.</summary>
public class CM_DELETE_ITEM : AionClientPacket
{
    public int itemObjectId;

    public CM_DELETE_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        itemObjectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        Storage inventory = player.GetInventory();
        Item item = inventory.GetItemByObjId(itemObjectId);

        if (item != null)
        {
            if (!item.GetItemTemplate().IsBreakable())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_UNBREAKABLE_ITEM(item.GetL10n()));
            }
            else
            {
                inventory.Delete(item, ItemDeleteType.DISCARD);
            }
        }
    }
}
