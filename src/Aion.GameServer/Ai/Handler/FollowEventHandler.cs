using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/FollowEventHandler (ATracer). Follow/creatureMoved/stopFollow dispatch. AIState.FOLLOWING/IDLE -> Following/
/// Idle; AiEventType.TARGET_TOOFAR -> AiEventType.TargetToofar; isInRange(AbstractAI&lt;? extends Creature&gt;) -> generic method
/// IsInRange&lt;T&gt;(AbstractAI&lt;T&gt;) where T : Creature (NpcAI=AbstractAI&lt;Npc&gt; can't satisfy an invariant AbstractAI&lt;Creature&gt; param).
/// EmoteManager/AIActions red-tolerated where unported.
/// </summary>
public class FollowEventHandler
{
    public static void Follow(NpcAI npcAI, Creature creature)
    {
        if (npcAI.SetStateIfNot(AIState.FOLLOWING))
        {
            npcAI.GetOwner().SetTarget(creature);
            EmoteManager.EmoteStartFollowing(npcAI.GetOwner());
        }
    }

    public static void CreatureMoved(NpcAI npcAI, Creature creature)
    {
        if (npcAI.IsInState(AIState.FOLLOWING))
        {
            if (npcAI.GetOwner().IsTargeting(creature.GetObjectId()) && !creature.IsDead())
            {
                CheckFollowTarget(npcAI, creature);
            }
        }
    }

    public static void CheckFollowTarget(NpcAI npcAI, Creature creature)
    {
        if (!IsInRange(npcAI, creature))
        {
            npcAI.OnGeneralEvent(AiEventType.TargetToofar);
        }
    }

    public static bool IsInRange<T>(AbstractAI<T> ai, VisibleObject obj) where T : Creature
    {
        if (obj == null)
        {
            return false;
        }
        return PositionUtil.IsInRange(ai.GetOwner(), obj, 2, false);
    }

    public static void StopFollow(NpcAI npcAI, Creature creature)
    {
        if (npcAI.SetStateIfNot(AIState.IDLE))
        {
            npcAI.GetOwner().SetTarget(null);
            npcAI.GetOwner().GetMoveController().AbortMove();
            AIActions.ScheduleRespawn(npcAI);
            AIActions.DeleteOwner(npcAI);
        }
    }
}
