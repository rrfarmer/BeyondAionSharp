using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_POSITION_SELF. Client reply to SM_POSITION_SELF; no-op.</summary>
public class CM_POSITION_SELF : AionClientPacket
{
    public CM_POSITION_SELF(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
    }

    protected override void RunImpl()
    {
    }
}
