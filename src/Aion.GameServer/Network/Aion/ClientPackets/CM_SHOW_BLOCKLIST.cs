using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SHOW_BLOCKLIST (Ben). Sent when the client requests the block list. SM_BLOCK_LIST red-tolerated.</summary>
public class CM_SHOW_BLOCKLIST : AionClientPacket
{
    public CM_SHOW_BLOCKLIST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {

    }

    protected override void RunImpl()
    {
        SendPacket(new SM_BLOCK_LIST());
    }
}
