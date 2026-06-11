using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Ai.Handler;

/// <summary>
/// Java parity: ai/handler/TargetEventHandler (ATracer). Target reached/too-far/giveup/change dispatch, switched on AiState. switch with
/// fallthrough -> shared case labels; AiState PascalCase (Fight/Returning/Following/Confuse/Fear/Walking/ForcedWalking); AiEventType.
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

        AiState currentState = npcAI.GetState();
        switch (currentState)
        {
            case AiState.Fight:
                npcAI.GetOwner().GetMoveController().AbortMove();
                AttackManager.ScheduleNextAttack(npcAI);
                break;
            case AiState.Returning:
                npcAI.GetOwner().GetMoveController().AbortMove();
                if (npcAI.GetOwner().IsAtSpawnLocation())
                    npcAI.OnGeneralEvent(AiEventType.BackHome);
                else
                {
                    npcAI.SetStateIfNot(AiState.Idle);
                    npcAI.OnGeneralEvent(AiEventType.NotAtHome);
                }
                break;
            case AiState.Following:
            case AiState.Confuse:
            case AiState.Fear:
                npcAI.GetOwner().GetMoveController().AbortMove();
                break;
            case AiState.Walking:
                WalkManager.TargetReached(npcAI);
                CheckAggro(npcAI);
                break;
            case AiState.ForcedWalking:
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
            case AiState.Fight:
                AttackManager.TargetTooFar(npcAI);
                break;
            case AiState.Following:
                FollowManager.TargetTooFar(npcAI);
                break;
            case AiState.Confuse:
            case AiState.Fear:
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
            if (npcAI.GetSubState() == AiSubState.TargetLost)
                npcAI.SetSubStateIfNot(AiSubState.None);
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
        if (npcAI.IsInState(AiState.Fight))
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
