using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Geo;

namespace Aion.GameServer.Ai.Manager;

/// <summary>
/// Java parity: ai/manager/SimpleAttackManager (ATracer). Performs/schedules a basic NPC melee attack tick with target validation.
/// Nested Runnable SimpleCheckedAttackAction -> nested class with Run() via async ThreadPool idiom; currentTimeMillis ->
/// UtcNow.ToUnixTimeMilliseconds; instanceof+pattern -> is Creature; AiEventType PascalCase; AIState.FIGHT; AggroTarget.MOST_HATED.
/// GeoService red-tolerated.
/// </summary>
public class SimpleAttackManager
{
    public static void PerformAttack(NpcAI npcAI, int delay)
    {
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "performAttack");
        }
        if (npcAI.GetOwner().GetGameStats().IsNextAttackScheduled())
        {
            if (npcAI.IsLogging())
            {
                AILogger.Info(npcAI, "Attack already scheduled");
            }
            ScheduleCheckedAttackAction(npcAI, delay);
            return;
        }

        npcAI.GetOwner().GetGameStats().SetNextAttackTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + delay);
        if (delay > 0)
        {
            ThreadPoolManager.GetInstance().Schedule(ct => { AttackAction(npcAI); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(delay));
        }
        else
        {
            AttackAction(npcAI);
        }
    }

    private static void ScheduleCheckedAttackAction(NpcAI npcAI, int delay)
    {
        if (delay < 2000)
        {
            delay = 2000;
        }
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "Scheduling checked attack " + delay);
        }
        ThreadPoolManager.GetInstance().Schedule(ct => { new SimpleCheckedAttackAction(npcAI).Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(delay));
    }

    public static bool IsTargetInAttackRange(Npc npc)
    {
        VisibleObject target = npc.GetTarget();
        if (!(target is Creature creature))
            return false;
        return PositionUtil.IsInAttackRange(npc, creature, npc.GetGameStats().GetAttackRange().GetCurrent() / 1000f);
    }

    protected static void AttackAction(NpcAI npcAI)
    {
        if (!npcAI.IsInState(AIState.FIGHT))
        {
            return;
        }
        if (npcAI.IsLogging())
        {
            AILogger.Info(npcAI, "attackAction");
        }
        Npc npc = npcAI.GetOwner();
        Creature mostHated = npc.GetAggroList().GetTarget(AggroTarget.MOST_HATED);
        if (mostHated != null && !mostHated.Equals(npc.GetTarget()))
        {
            npcAI.OnCreatureEvent(AiEventType.TargetChanged, mostHated);
        }
        else if (!(npc.GetTarget() is Creature target) || target.IsDead())
        {
            npcAI.OnGeneralEvent(AiEventType.TargetGiveup);
        }
        else if (!npc.CanSee(target))
        {
            npc.GetController().AbortCast();
            npcAI.OnGeneralEvent(AiEventType.TargetToofar);
        }
        else if (!IsTargetInAttackRange(npc))
        {
            npcAI.OnGeneralEvent(AiEventType.TargetToofar);
        }
        else if (!GeoService.GetInstance().CanSee(npc, target))
        { // delete geo check when we've implemented a pathfinding system
            npc.GetController().CancelCurrentSkill(null);
            if (((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - npc.GetMoveController().GetLastMoveUpdate()) > 15000)
                && npc.GetGameStats().GetLastAttackedTimeDelta() > 15)
            {
                npcAI.OnGeneralEvent(AiEventType.TargetGiveup);
            }
            else
            {
                npcAI.OnGeneralEvent(AiEventType.AttackComplete);
            }
        }
        else
        {
            if (npc.IsSpawned() && !npc.IsDead() && !npc.GetLifeStats().IsAboutToDie() && npc.CanAttack())
            {
                npc.GetPosition().SetH(PositionUtil.GetHeadingTowards(npc, target));
                npc.GetController().AttackTarget(target, 0, true);
            }
            npcAI.OnGeneralEvent(AiEventType.AttackComplete);
        }
    }

    private sealed class SimpleCheckedAttackAction
    {
        private NpcAI npcAI;

        public SimpleCheckedAttackAction(NpcAI npcAI)
        {
            this.npcAI = npcAI;
        }

        public void Run()
        {
            if (!npcAI.GetOwner().GetGameStats().IsNextAttackScheduled())
            {
                AttackAction(npcAI);
            }
            else
            {
                if (npcAI.IsLogging())
                {
                    AILogger.Info(npcAI, "Scheduled checked attacked confirmed");
                }
            }
            npcAI = null;
        }
    }
}
