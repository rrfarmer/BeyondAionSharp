using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PRIVATE_STORE_NAME (Simple). Opens a private store with the given name. PrivateStoreService red-tolerated.</summary>
public class CM_PRIVATE_STORE_NAME : AionClientPacket
{
    private string name;

    public CM_PRIVATE_STORE_NAME(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        name = ReadS();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        PrivateStoreService.OpenPrivateStore(activePlayer, name);
    }
}
