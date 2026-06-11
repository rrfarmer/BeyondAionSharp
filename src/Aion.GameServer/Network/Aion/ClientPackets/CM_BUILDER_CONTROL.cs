using System.Collections.Generic;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>
/// Java parity: network/aion/clientpackets/CM_BUILDER_CONTROL (ginho1). Sent for GM Panel buttons when "Builder control (///)" is selected, and
/// /// -prefixed commands in the command tab / macros (console enabled). Behaviour inherited from AbstractGmCommandPacket.
/// </summary>
public class CM_BUILDER_CONTROL : AbstractGmCommandPacket
{
    public CM_BUILDER_CONTROL(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }
}
