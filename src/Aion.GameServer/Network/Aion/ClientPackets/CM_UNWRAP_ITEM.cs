using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using PersistentState = Aion.GameServer.Model.GameObjects.IPersistable.PersistentState;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_UNWRAP_ITEM (xTz). Unwraps a packed item (negates pack count, flags update). SM_UNWRAP_ITEM/SM_INVENTORY_UPDATE_ITEM red-tolerated.</summary>
public class CM_UNWRAP_ITEM : AionClientPacket
{
    private int objectId;

    public CM_UNWRAP_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        objectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
        {
            return;
        }
        Item item = player.GetInventory().GetItemByObjId(objectId);
        if (item != null)
        {
            if (item.GetPackCount() > 0)
            {
                SendPacket(new SM_UNWRAP_ITEM(objectId, item.GetPackCount()));
                item.SetPackCount(item.GetPackCount() * -1);
                item.SetPersistentState(PersistentState.UPDATE_REQUIRED);
                PacketSendUtility.SendPacket(player, new SM_INVENTORY_UPDATE_ITEM(player, item));
            }
        }
    }
}
