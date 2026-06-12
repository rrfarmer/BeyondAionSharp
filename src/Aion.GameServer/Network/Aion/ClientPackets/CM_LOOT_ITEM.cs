using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Drop;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LOOT_ITEM (alexa026, ATracer). Requests looting a specific drop-list item by index. DropService red-tolerated.</summary>
public class CM_LOOT_ITEM : AionClientPacket
{
    private int targetObjectId;
    private int index;

    public CM_LOOT_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjectId = ReadD();
        index = ReadUC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;
        DropService.GetInstance().RequestDropItem(player, targetObjectId, index);
    }
}
