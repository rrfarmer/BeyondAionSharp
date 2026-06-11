using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Ai.Manager;

/// <summary>Java parity: ai/manager/FollowManager (ATracer). Resumes movement toward the target when it gets too far.</summary>
public class FollowManager
{
    public static void TargetTooFar(NpcAI npcAI)
    {
        Npc npc = npcAI.GetOwner();
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "Follow manager - targetTooFar");
        }
        if (npcAI.IsMoveSupported())
        {
            npc.GetMoveController().MoveToTargetObject();
        }
    }
}
