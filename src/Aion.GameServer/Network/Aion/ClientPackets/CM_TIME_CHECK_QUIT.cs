using System.Collections.Generic;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_TIME_CHECK_QUIT (Rolandas). Time-check variant sent on quit; behaviour inherited from CM_TIME_CHECK.</summary>
public class CM_TIME_CHECK_QUIT : CM_TIME_CHECK
{
    public CM_TIME_CHECK_QUIT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }
}
