using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Ai.Manager;

/// <summary>
/// Java parity: ai/manager/AttackManager (ATracer). Drives NPC attack scheduling/intention + target-too-far chase/giveup logic.
/// AttackIntention SCREAMING (SIMPLE_ATTACK/SKILL_ATTACK/FINISH_ATTACK); AISubState.NONE/TARGET_LOST -> None/TargetLost; AiEventType.
/// TARGET_CHANGED/TARGET_GIVEUP -> TargetChanged/TargetGiveup; AggroTarget.MOST_HATED SCREAMING; instanceof -> is. schedule(Runnable,2000)
/// -> async ThreadPool idiom. SimpleAttackManager/SkillAttackManager red-tolerated (port next ticks).
/// </summary>
public class AttackManager
{
    public static void StartAttacking(NpcAI npcAI)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "AttackManager: startAttacking");
        }
        npcAI.GetOwner().GetGameStats().SetFightStartingTime();
        VisibleObject target = npcAI.GetOwner().GetTarget();
        if (target is Creature creature) // may be null at this point (-> triggering then TARGET_GIVEUP)
            EmoteManager.EmoteStartAttacking(npcAI.GetOwner(), creature);
        ScheduleNextAttack(npcAI);
    }

    public static void ScheduleNextAttack(NpcAI npcAI)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "AttackManager: scheduleNextAttack");
        }
        // don't start attack while in casting substate
        AISubState subState = npcAI.GetSubState();
        if (subState == AISubState.NONE)
        {
            ChooseAttack(npcAI, npcAI.GetOwner().GetGameStats().GetNextAttackInterval());
        }
        else
        {
            if (npcAI.IsLogging())
            {
                AILogger.Info(npcAI, "Will not choose attack in substate" + subState);
            }
        }
    }

    protected static void ChooseAttack(NpcAI npcAI, int delay)
    {
        AttackIntention attackIntention = npcAI.ChooseAttackIntention();
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "AttackManager: chooseAttack " + attackIntention + " delay " + delay);
        }
        if (!npcAI.CanThink())
        {
            return;
        }
        switch (attackIntention)
        {
            case AttackIntention.SIMPLE_ATTACK:
                SimpleAttackManager.PerformAttack(npcAI, delay);
                break;
            case AttackIntention.SKILL_ATTACK:
                SkillAttackManager.PerformAttack(npcAI, delay);
                break;
            case AttackIntention.FINISH_ATTACK:
                npcAI.Think();
                break;
        }
    }

    public static void TargetTooFar(NpcAI npcAI)
    {
        Npc npc = npcAI.GetOwner();
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "AttackManager: attackTimeDelta " + npc.GetGameStats().GetLastAttackTimeDelta());
        }

        // switch target if there is more hated creature
        if (npc.GetGameStats().GetLastChangeTargetTimeDelta() > 5)
        {
            Creature mostHated = npc.GetAggroList().GetTarget(AggroTarget.MOST_HATED);
            if (mostHated != null && !npc.IsTargeting(mostHated.GetObjectId()))
            {
                if (npcAI.IsLogging())
                {
                    AILogger.Info(npcAI, "AttackManager: switching target during chase");
                }
                npcAI.OnCreatureEvent(AiEventType.TargetChanged, mostHated);
                return;
            }
        }
        if (!npc.CanSee(npc.GetTarget()))
        {
            if (npcAI.SetSubStateIfNot(AISubState.TARGET_LOST))
            {
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    if (npcAI.IsInSubState(AISubState.TARGET_LOST) && npc.IsSpawned() && !npc.IsDead())
                        npcAI.OnGeneralEvent(AiEventType.TargetGiveup);
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(2000));
            }
            return;
        }
        if (CheckGiveupDistance(npcAI))
        {
            npcAI.OnGeneralEvent(AiEventType.TargetGiveup);
            return;
        }
        if (npcAI.IsMoveSupported())
        {
            npc.GetMoveController().MoveToTargetObject();
            return;
        }
        npcAI.OnGeneralEvent(AiEventType.TargetGiveup);
    }

    private static bool CheckGiveupDistance(NpcAI npcAI)
    {
        Npc npc = npcAI.GetOwner();
        // if target run away too far
        VisibleObject target = npc.GetTarget();
        if (target != null)
        {
            if (npcAI.IsLogging())
                AILogger.Info(npcAI, "AttackManager: distanceToTarget " + PositionUtil.GetDistance(npc, target, false));
            int maxChaseDistance = npc.IsBoss() ? 50 : npc.GetPosition().GetWorldMapInstance().GetTemplate().GetAiInfo().GetChaseTarget();
            if (!PositionUtil.IsInRange(npc, target, maxChaseDistance))
                return true;
        }
        double distanceToHome = npc.GetDistanceToSpawnLocation();
        // if npc is far away from home
        int chaseHome = npc.IsBoss() ? 150 : npc.GetPosition().GetWorldMapInstance().GetTemplate().GetAiInfo().GetChaseHome();
        if (distanceToHome > chaseHome)
        {
            return true;
        }
        // start thinking about home after 100 meters and no attack for 10 seconds (only for default monsters)
        if (chaseHome <= 200)
        { // TODO: Check Client and use chase_user_by_trace value
            if ((npc.GetGameStats().GetLastAttackTimeDelta() > 20 && npc.GetGameStats().GetLastAttackedTimeDelta() > 20)
                || (distanceToHome > chaseHome / 2 && npc.GetGameStats().GetLastAttackedTimeDelta() > 10))
            {
                return true;
            }
        }
        return false;
    }
}
