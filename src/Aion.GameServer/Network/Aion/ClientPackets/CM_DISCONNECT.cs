using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_DISCONNECT (Neon). Sent before the client closes the connection on AFK timeout; the client does not wait for a response.</summary>
public class CM_DISCONNECT : AionClientPacket
{
    public CM_DISCONNECT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        ReadC(); // 0 when client auto closes the connection for inactivity, maybe there are other flags in different cases
    }

    protected override void RunImpl()
    {
        // no need to do something here, since the character will leave world shortly after the connection is closed
    }
}
