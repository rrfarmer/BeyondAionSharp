using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.ConquerorAndProtectorSystem;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SHOW_MAP (Lyahim). Map action 0 triggers conqueror/protector intruder scan. ConquerorAndProtectorService red-tolerated.</summary>
public class CM_SHOW_MAP : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_SHOW_MAP));
    private byte action;

    public CM_SHOW_MAP(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = ReadC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        switch (action)
        {
            case 0:
                ConquerorAndProtectorService.GetInstance().IntruderScan(player);
                break;
            case 1:
                // TODO unk
                break;
            default:
                log.LogWarning(player + " sent unknown show map action type: " + action);
                break;
        }
    }
}
