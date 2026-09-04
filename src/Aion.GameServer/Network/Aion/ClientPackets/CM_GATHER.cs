using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.Audit;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_GATHER (ATracer). Starts (-1 cancel / 0,128 start) gathering from a Gatherable target. Gatherable/AuditLogger red-tolerated.</summary>
public class CM_GATHER : AionClientPacket
{
    private int actionId;

    public CM_GATHER(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        actionId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        switch (actionId)
        {
            case -1:
                CancelGathering(player);
                break;
            case 0:
            case 128:
                StartGathering(player); // 128 is sent when using /attack chat command
                break;
            default:
                NullLoggerFactory.Instance.CreateLogger(GetType().Name).LogWarning("Unhandled gathering action ID {ActionId} (sent by {Player} at {Position})", actionId, player, player.GetPosition());
                break;
        }
    }

    private void StartGathering(Player player)
    {
        if (player.GetTarget() is Gatherable gatherable)
            gatherable.GetController().StartGathering(player);
        else
            AuditLogger.Log(player, "tried to gather from " + player.GetTarget());
    }

    private void CancelGathering(Player player)
    {
        if (player.GetInteractionTask() is global::Aion.GameServer.SkillEngine.Task.GatheringTask gatheringTask)
            gatheringTask.Abort();
    }
}
