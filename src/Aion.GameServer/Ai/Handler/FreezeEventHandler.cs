using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/FreezeEventHandler (Rolandas, Neon). Freeze/unfreeze AI sub-state toggling. Wildcard AbstractAI&lt;? extends
/// Creature&gt; erased to AbstractAI&lt;Creature&gt;; AISubState.FREEZE/NONE -> AiSubState.Freeze/None.
/// </summary>
public class FreezeEventHandler
{
    public static void OnUnfreeze(AbstractAI<Creature> ai)
    {
        if (ai.IsInSubState(AiSubState.Freeze))
        {
            ai.SetSubStateIfNot(AiSubState.None);
            ai.Think();
        }
    }

    public static void OnFreeze(AbstractAI<Creature> ai)
    {
        ai.SetSubStateIfNot(AiSubState.Freeze);
        ai.Think();
    }
}
