using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.LoginServer;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_RECONNECT_AUTH (-Nemesiss-). Client requests a fast reconnection to the LoginServer. LoginServer red-tolerated.</summary>
public class CM_RECONNECT_AUTH : AionClientPacket
{
    public CM_RECONNECT_AUTH(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
    }

    protected override void RunImpl()
    {
        AionConnection client = GetConnection();
        LoginServer.GetInstance().RequestAuthReconnection(client.GetAccount().GetId(), client);
    }
}
