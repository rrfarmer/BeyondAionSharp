using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_REJECT_REVIVE (Neon). Sent when a player declines another player's revive; no-op.</summary>
public class CM_REJECT_REVIVE : AionClientPacket
{
    public CM_REJECT_REVIVE(int opcode, ISet<State> validStates)
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
