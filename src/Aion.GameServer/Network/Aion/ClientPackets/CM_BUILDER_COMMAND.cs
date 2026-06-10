using System.Collections.Generic;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>
/// Java parity: network/aion/clientpackets/CM_BUILDER_COMMAND (ginho1). Sent for GM Dialog buttons, GM Panel buttons when "Builder command (//)" is
/// selected, and // -prefixed commands in the command tab / macros (console enabled). Behaviour inherited from AbstractGmCommandPacket.
/// </summary>
public class CM_BUILDER_COMMAND : AbstractGmCommandPacket
{
    public CM_BUILDER_COMMAND(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }
}
