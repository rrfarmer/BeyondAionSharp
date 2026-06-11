using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHECK_MAIL_UNK (ginho1). Unknown mail-related opcode (no-op).</summary>
public class CM_CHECK_MAIL_UNK : AionClientPacket
{
    public CM_CHECK_MAIL_UNK(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {

    }

    protected override void RunImpl()
    {
        // TODO???
    }
}
