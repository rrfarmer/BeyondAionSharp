using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Items;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MOVE_ITEM (alexa026, kosyachok). Moves an item between storages (inventory / warehouses). ItemMoveService red-tolerated.</summary>
public class CM_MOVE_ITEM : AionClientPacket
{
    private int itemObjId;
    private byte source;
    private byte destination;
    private short slot;

    public CM_MOVE_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        itemObjId = ReadD();
        source = ReadC(); // FROM (0 - player inventory, 1 - regular warehouse, 2 - account warehouse, 3 - legion warehouse)
        destination = ReadC(); // TO
        slot = ReadH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        ItemMoveService.MoveItem(player, itemObjId, source, destination, slot);
    }
}
