using Aion.GameServer.Ai;

namespace Aion.GameServer.Ai.Handler;

/// <summary>Java parity: ai/handler/DiedEventHandler (ATracer). Handles NPC death: logs, shouts, sets DIED state, clears target.</summary>
public class DiedEventHandler
{
    public static void OnDie(NpcAI npcAI)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onDie");
        }

        ShoutEventHandler.OnDied(npcAI);

        npcAI.SetStateIfNot(AiState.Died);
        npcAI.SetSubStateIfNot(AiSubState.None);
        npcAI.GetOwner().SetTarget(null);
    }
}
