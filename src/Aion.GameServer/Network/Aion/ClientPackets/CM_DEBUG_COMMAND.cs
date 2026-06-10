using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>
/// Java parity: network/aion/clientpackets/CM_DEBUG_COMMAND. Sent for GM-panel commands prefixed by //// (builder control) or ///// (builder command),
/// and //// in macros when the console is enabled. Logs to the ADMINAUDIT_LOG channel via the base GM-command handler.
/// </summary>
public class CM_DEBUG_COMMAND : AbstractGmCommandPacket
{
    public CM_DEBUG_COMMAND(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void RunImpl()
    {
        NullLoggerFactory.Instance.CreateLogger("ADMINAUDIT_LOG").LogInformation(GetConnection().GetActivePlayer() + " sent debug command ////" + command);
    }
}
