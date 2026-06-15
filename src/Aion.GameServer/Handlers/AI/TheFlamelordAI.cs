using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/raksang/TheFlamelordAI (@author xTz).
/// </summary>
[AIName("the_flamelord")]
public class TheFlamelordAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(90, 40, 30, 20, 10);
    private readonly AtomicBoolean isAggred = new AtomicBoolean(false);
    private ScheduledTask? phaseTask;

    public TheFlamelordAI(Npc owner)
        : base(owner)
    {
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 90:
                StartPhaseTask();
                break;
            case 40:
            case 30:
            case 20:
            case 10:
                StartPhaseEvent(phaseHpPercent);
                break;
        }
    }

    private void StartPhaseEvent(int percent)
    {
        CancelPhaseTask();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1401120);
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19980, 46, GetOwner()).UseNoAnimationSkill();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                switch (percent)
                {
                    case 40:
                        MoveExecutor(282451);
                        break;
                    case 30:
                        MoveExecutor(282451);
                        MoveExecutor(282452);
                        break;
                    case 20:
                        MoveExecutor(282451);
                        MoveExecutor(282452);
                        MoveExecutor(282453);
                        break;
                    case 10:
                        MoveExecutor(282451);
                        MoveExecutor(282452);
                        MoveExecutor(282453);
                        MoveExecutor(282454);
                        break;
                }
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19924, 44, GetOwner()).UseNoAnimationSkill();
                CancelPhaseTask();
                StartPhaseTask();
            }
            return ValueTask.CompletedTask;
        }, 5000L);
    }

    private void MoveExecutor(int executorId)
    {
        Npc npc = (Npc)Spawn(executorId, 802.845f, 964.903f, 792.102f, (sbyte)0);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                int targetId = 0;
                switch (executorId)
                {
                    case 282451:
                        targetId = 701062;
                        break;
                    case 282452:
                        targetId = 701063;
                        break;
                    case 282453:
                        targetId = 701064;
                        break;
                    case 282454:
                        targetId = 701065;
                        break;
                }
                Npc target = GetPosition().GetWorldMapInstance().GetNpc(targetId);
                if (target != null)
                {
                    npc.SetTarget(target);
                    npc.GetMoveController().MoveToTargetObject();
                }
            }
            return ValueTask.CompletedTask;
        }, 1500L);
    }

    private void StartPhaseTask()
    {
        phaseTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelPhaseTask();
            }
            else
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19925, 44, GetOwner()).UseNoAnimationSkill();
                PacketSendUtility.BroadcastMessage(GetOwner(), 1401119);
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(3000), TimeSpan.FromMilliseconds(30000));
    }

    private void CancelPhaseTask()
    {
        if (phaseTask != null && !phaseTask.IsDone())
        {
            phaseTask.Cancel(true);
        }
    }

    protected override void HandleDied()
    {
        CancelPhaseTask();
        GetPosition().GetWorldMapInstance().SetDoorState(118, true);
        PacketSendUtility.BroadcastMessage(GetOwner(), 1401121);
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelPhaseTask();
        base.HandleDespawned();
    }

    protected override void HandleAttack(Creature creature)
    {
        if (isAggred.CompareAndSet(false, true))
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1401118);
        }
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    protected override void HandleBackHome()
    {
        CancelPhaseTask();
        isAggred.Set(false);
        base.HandleBackHome();
        hpPhases.Reset();
    }
}
