using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SHOW_FRIENDLIST (Ben). Sent when the client requests the friend list. SM_FRIEND_LIST red-tolerated.</summary>
public class CM_SHOW_FRIENDLIST : AionClientPacket
{
    public CM_SHOW_FRIENDLIST(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
    }

    protected override void RunImpl()
    {
        SendPacket(new SM_FRIEND_LIST());
    }
}
