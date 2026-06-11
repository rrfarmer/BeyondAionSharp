using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Items;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_REPLACE_ITEM (kosyachok). Swaps two items between storages. ItemMoveService red-tolerated.</summary>
public class CM_REPLACE_ITEM : AionClientPacket
{
    private byte sourceStorageType;
    private int sourceItemObjId;
    private byte replaceStorageType;
    private int replaceItemObjId;

    public CM_REPLACE_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        sourceStorageType = ReadC();
        sourceItemObjId = ReadD();
        replaceStorageType = ReadC();
        replaceItemObjId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        ItemMoveService.SwitchItemsInStorages(player, sourceStorageType, sourceItemObjId, replaceStorageType, replaceItemObjId);
    }
}
