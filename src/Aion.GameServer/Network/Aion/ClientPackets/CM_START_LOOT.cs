using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Drop;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_START_LOOT (alexa026, Metos, ATracer). Opens (0) or closes (1) a corpse drop list. DropService red-tolerated.</summary>
public class CM_START_LOOT : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_START_LOOT));
    /// <summary>Target object id that client wants to TALK WITH or 0 if wants to unselect</summary>
    private int targetObjectId;
    private byte action;

    public CM_START_LOOT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjectId = ReadD(); // empty
        action = ReadC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        switch (action)
        {
            case 0: // open
                DropService.GetInstance().RequestDropList(player, targetObjectId);
                break;
            case 1: // close
                DropService.GetInstance().CloseDropList(player, targetObjectId);
                break;
            default:
                log.LogWarning(player + " sent unknown loot action type " + action);
                break;
        }
    }
}
