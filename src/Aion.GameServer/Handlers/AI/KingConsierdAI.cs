using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/empyreanCrucible/KingConsierdAI (@author Luzien).
/// </summary>
[AIName("king_consierd")]
public class KingConsierdAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(75, 25);
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? eventTask;
    private ScheduledTask? skillTask;

    public KingConsierdAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDespawned()
    {
        CancelTasks();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelTasks();
        DespawnNpcs(GetPosition().GetWorldMapInstance().GetNpcs(282378));
        base.HandleDied();
    }

    protected override void HandleBackHome()
    {
        CancelTasks();
        DespawnNpcs(GetPosition().GetWorldMapInstance().GetNpcs(282378));
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
        if (isHome.CompareAndSet(true, false))
        {
            StartBloodThirstTask();

            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19691, 1, GetTarget()).UseNoAnimationSkill();
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 17954, 29, GetTarget()).UseNoAnimationSkill();
                    return ValueTask.CompletedTask;
                }, 4000L);
                return ValueTask.CompletedTask;
            }, 2000L);
        }
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 75:
                StartSkillTask();
                break;
            case 25:
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19690, 1, GetTarget()).UseNoAnimationSkill();
                break;
        }
    }

    private void StartBloodThirstTask()
    {
        eventTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19624, 10, GetOwner()).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, 180 * 1000L); // 3min, need confirm
    }

    private void StartSkillTask()
    {
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelTasks();
                return ValueTask.CompletedTask;
            }
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 17951, 29, GetTarget()).UseNoAnimationSkill();
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (GetLifeStats().GetHpPercentage() <= 50)
                {
                    WorldPosition p = GetPosition();
                    Spawn(282378, p.GetX(), p.GetY(), p.GetZ(), unchecked((sbyte)p.GetHeading()));
                    Spawn(282378, p.GetX(), p.GetY(), p.GetZ(), unchecked((sbyte)p.GetHeading()));
                }
                ThreadPoolManager.GetInstance().Schedule(_ => { SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 17952, 29, GetTarget()).UseNoAnimationSkill(); return ValueTask.CompletedTask; }, 2000L);
                return ValueTask.CompletedTask;
            }, 3500L);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(25000));
    }

    private void CancelTasks()
    {
        if (eventTask != null && !eventTask.IsDone())
        {
            eventTask.Cancel(true);
        }
        if (skillTask != null && !skillTask.IsCancelled)
        {
            skillTask.Cancel(true);
        }
    }

    private void DespawnNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            npc.GetController().Delete();
        }
    }
}
