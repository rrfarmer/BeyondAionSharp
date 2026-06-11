using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Ai.Handler;

/// <summary>Java parity: ai/handler/ActivateEventHandler (ATracer). Map-region activate/deactivate dispatch for an NpcAI.</summary>
public class ActivateEventHandler
{
    public static void OnActivate(NpcAI npcAI)
    {
        if (npcAI.IsInState(AiState.Idle))
        {
            npcAI.GetOwner().UpdateKnownlist();
            npcAI.Think();
        }
    }

    public static void OnDeactivate(NpcAI npcAI)
    {
        npcAI.Think();
        Npc npc = npcAI.GetOwner();
        npc.UpdateKnownlist();
        npc.GetController().LoseAggro(false);
        if (npcAI.Ask(AIQuestion.REMOVE_EFFECTS_ON_MAP_REGION_DEACTIVATE))
            npc.GetEffectController().RemoveAllEffects();
    }
}
