using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Item;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SPLIT_ITEM (kosyak). Splits an item stack into a destination slot/storage. ItemSplitService red-tolerated.</summary>
public class CM_SPLIT_ITEM : AionClientPacket
{
    int sourceItemObjId;
    byte sourceStorageType;
    long itemAmount;
    int destinationItemObjId;
    byte destinationStorageType;
    short slotNum;

    public CM_SPLIT_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        sourceItemObjId = ReadD();
        itemAmount = ReadQ();
        sourceStorageType = ReadC();
        destinationItemObjId = ReadD();
        destinationStorageType = ReadC();
        slotNum = ReadH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        ItemSplitService.SplitItem(player, sourceItemObjId, destinationItemObjId, itemAmount, slotNum, sourceStorageType, destinationStorageType);
    }
}
