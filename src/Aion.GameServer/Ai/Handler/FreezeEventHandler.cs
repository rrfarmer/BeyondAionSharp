using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/FreezeEventHandler (Rolandas, Neon). Freeze/unfreeze AI sub-state toggling. Wildcard AbstractAI&lt;? extends
/// Creature&gt; erased to AbstractAI&lt;Creature&gt;; AISubState.FREEZE/NONE -> AISubState.FREEZE/None.
/// </summary>
public class FreezeEventHandler
{
    public static void OnUnfreeze(AbstractAI<Creature> ai)
    {
        if (ai.IsInSubState(AISubState.FREEZE))
        {
            ai.SetSubStateIfNot(AISubState.NONE);
            ai.Think();
        }
    }

    public static void OnFreeze(AbstractAI<Creature> ai)
    {
        ai.SetSubStateIfNot(AISubState.FREEZE);
        ai.Think();
    }
}
