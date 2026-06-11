using Aion.GameServer.Ai;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/MoveEventHandler (ATracer). Movement validate/arrive dispatch -> controller OnMove + TargetEventHandler.
/// TargetEventHandler red-tolerated (not yet ported).
/// </summary>
public class MoveEventHandler
{
    public static void OnMoveValidate(NpcAI npcAI)
    {
        if (npcAI.GetOwner().CanPerformMove())
        {
            npcAI.GetOwner().GetController().OnMove();
            TargetEventHandler.OnTargetTooFar(npcAI);
        }
    }

    public static void OnMoveArrived(NpcAI npcAI)
    {
        npcAI.GetOwner().GetController().OnMove();
        TargetEventHandler.OnTargetReached(npcAI);
    }
}
