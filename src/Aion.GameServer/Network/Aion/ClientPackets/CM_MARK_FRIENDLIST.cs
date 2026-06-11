using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MARK_FRIENDLIST (xTz, Rolandas). Replies with SM_MARK_FRIENDLIST. SM_MARK_FRIENDLIST red-tolerated.</summary>
public class CM_MARK_FRIENDLIST : AionClientPacket
{
    public CM_MARK_FRIENDLIST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        // nothing to read
    }

    protected override void RunImpl()
    {
        SendPacket(new SM_MARK_FRIENDLIST());
    }
}
