using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/TargetEventHandler (ATracer). Target reached/too-far/giveup/change dispatch, switched on AIState. switch with
/// fallthrough -> shared case labels; AIState PascalCase (Fight/Returning/Following/Confuse/Fear/Walking/ForcedWalking); AiEventType.
/// BACK_HOME/NOT_AT_HOME -> BackHome/NotAtHome; AISubState.TARGET_LOST/NONE. AttackManager/FollowManager/WalkManager red-tolerated.
/// </summary>
public class TargetEventHandler
{
    public static void OnTargetReached(NpcAI npcAI)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onTargetReached");
        }

        AIState currentState = npcAI.GetState();
        switch (currentState)
        {
            case AIState.FIGHT:
                npcAI.GetOwner().GetMoveController().AbortMove();
                AttackManager.ScheduleNextAttack(npcAI);
                break;
            case AIState.RETURNING:
                npcAI.GetOwner().GetMoveController().AbortMove();
                if (npcAI.GetOwner().IsAtSpawnLocation())
                    npcAI.OnGeneralEvent(AiEventType.BackHome);
                else
                {
                    npcAI.SetStateIfNot(AIState.IDLE);
                    npcAI.OnGeneralEvent(AiEventType.NotAtHome);
                }
                break;
            case AIState.FOLLOWING:
            case AIState.CONFUSE:
            case AIState.FEAR:
                npcAI.GetOwner().GetMoveController().AbortMove();
                break;
            case AIState.WALKING:
                WalkManager.TargetReached(npcAI);
                CheckAggro(npcAI);
                break;
            case AIState.FORCED_WALKING:
                WalkManager.TargetReached(npcAI);
                break;
        }
    }

    public static void OnTargetTooFar(NpcAI npcAI)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onTargetTooFar");
        }
        switch (npcAI.GetState())
        {
            case AIState.FIGHT:
                AttackManager.TargetTooFar(npcAI);
                break;
            case AIState.FOLLOWING:
                FollowManager.TargetTooFar(npcAI);
                break;
            case AIState.CONFUSE:
            case AIState.FEAR:
                break;
            default:
                if (npcAI.IsLogging())
                {
                    AILogger.Info(npcAI, "default onTargetTooFar");
                }
                break;
        }
    }

    public static void OnTargetGiveup(NpcAI npcAI)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onTargetGiveup");
        }
        VisibleObject target = npcAI.GetOwner().GetTarget();
        if (target != null)
        {
            if (npcAI.GetSubState() == AISubState.TARGET_LOST)
                npcAI.SetSubStateIfNot(AISubState.NONE);
            npcAI.GetOwner().GetAggroList().StopHating(target);
        }
        if (npcAI.IsMoveSupported())
        {
            npcAI.GetOwner().GetMoveController().AbortMove();
        }
        if (!npcAI.IsDead())
            npcAI.Think();
    }

    public static void OnTargetChange(NpcAI npcAI, Creature creature)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "onTargetChange");
        }
        if (npcAI.IsInState(AIState.FIGHT))
        {
            npcAI.GetOwner().SetTarget(creature);
            AttackManager.ScheduleNextAttack(npcAI);
        }
    }

    private static void CheckAggro(NpcAI npcAI)
    {
        npcAI.GetOwner().GetKnownList().ForEachObject(obj =>
        {
            if (obj is Creature creature)
                CreatureEventHandler.CheckAggro(npcAI, creature);
        });
    }
}
