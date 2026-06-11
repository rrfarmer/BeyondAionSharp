using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MAY_QUIT (xavier). Sent by client when the player may quit in 10 seconds; no-op.</summary>
public class CM_MAY_QUIT : AionClientPacket
{
    public CM_MAY_QUIT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        // empty
    }

    protected override void RunImpl()
    {
        // Nothing to do
    }
}
