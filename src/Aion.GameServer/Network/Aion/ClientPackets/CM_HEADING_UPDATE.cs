using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HEADING_UPDATE. Client sends this after a spin effect to update heading (already set before receipt); no response.</summary>
public class CM_HEADING_UPDATE : AionClientPacket
{
    // Client sends this packet after spin effect to update the heading (in that case we already set the heading before we receive this packet)
    // TODO: Find out when else this packet is sent and what or even if we have to answer
    public CM_HEADING_UPDATE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        ReadC(); // heading
    }

    protected override void RunImpl()
    {
    }
}
