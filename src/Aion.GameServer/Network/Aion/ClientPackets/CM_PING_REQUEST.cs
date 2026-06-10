using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_PING_REQUEST (dragoon112). Sent on /ping; replies SM_PING_RESPONSE. SM_PING_RESPONSE red-tolerated.</summary>
public class CM_PING_REQUEST : AionClientPacket
{
    public CM_PING_REQUEST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        // empty
    }

    protected override void RunImpl()
    {
        SendPacket(new SM_PING_RESPONSE());
    }
}
