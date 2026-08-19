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
/// King Consierd, Empyrean Crucible. Retail pattern <c>IDArena_S9_Named_2</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/empyreanCrucible/KingConsierdAI (@author Luzien). Retail-sourced
/// corrections below; see docs/retail-ai-fidelity.md. Found by <c>audit_hp_phases.py</c>.
/// <para>
/// <b>His condors were on the wrong clock, at the wrong threshold, with no floor.</b> Retail gives
/// them a battle timer of their own — <c>BTIMERI_INDEX_4</c>, re-armed at <b>30000</b>, guarded by
/// <c>is_hp_in_boundary larger_than=26 less_than=100</c> — which is first armed by the rung that fires
/// when he drops below <b>55</b>. So they start at fifty-five per cent, come every thirty seconds, and
/// <b>stop below twenty-six</b>.
/// </para>
/// <para>
/// This class hung them off its own twenty-five-second skill task with an <c>hp &lt;= 50</c> test
/// inside it, so they arrived <b>five per cent late, a fifth too often, and never stopped</b> — the
/// last quarter of the fight, which retail deliberately leaves clear, had condors in it throughout.
/// </para>
/// <para>
/// <b>And both landed on his exact point.</b> Retail's <c>spawn_range</c> is ten.
/// </para>
/// <para>
/// <b>Not translated.</b> Almost everything else: six battle timers of skill indices, the
/// <c>ATTACKERI_THIRD_HATING</c> and <c>ATTACKERI_HAS_LOWEST_HP</c> target switches that go with them,
/// and the three health bands (81-100, 56-80, 26-55) that pick which cast rotation he is running. The
/// 75 in <see cref="hpPhases"/> is this port's own and starts our skill task; retail's band edge there
/// is 80.
/// </para>
/// </remarks>
[AIName("king_consierd")]
public class KingConsierdAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(75, 55, 25);

    /// <summary>
    /// Retail's condor rung: <c>BTIMERI_INDEX_4</c> at <b>30000</b>, two birds, <c>spawn_range=10</c>,
    /// <c>live_time=600</c>, while his health is above twenty-six per cent.
    /// </summary>
    public const long CondorRepeatMillis = 30_000L;
    public const int CondorsPerWave = 2;
    public const float CondorSpread = 10f;
    public const int CondorFloorPercent = 26;

    /// <summary>The health at which the timer is first armed, from the rung that arms it.</summary>
    public const int CondorStartPercent = 55;

    private ScheduledTask? condorTask;
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

    /// <summary>Retail <c>IDArena_Summon_Condor_55_An</c> stands ten minutes.</summary>
    /// <remarks>
    /// Despawned here on death and on going home, which bounds them after the fight and not during it.
    /// </remarks>
    private const int CondorLife = 600;

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
            case CondorStartPercent:
                StartCondorTask();
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
            // The condors used to be spawned from here, on this task's twenty-five second period and
            // behind an hp <= 50 test. They are retail's own timer now; see StartCondorTask.
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                ThreadPoolManager.GetInstance().Schedule(_ => { SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 17952, 29, GetTarget()).UseNoAnimationSkill(); return ValueTask.CompletedTask; }, 2000L);
                return ValueTask.CompletedTask;
            }, 3500L);
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(25000));
    }

    /// <summary>
    /// Retail's <c>BTIMERI_INDEX_4</c>: two condors every thirty seconds until he is under twenty-six
    /// per cent, scattered ten metres about him.
    /// </summary>
    /// <remarks>
    /// The floor is checked when the timer fires rather than when it is armed, which is retail's shape:
    /// the guard is on the rung, so the timer keeps running and simply stops matching.
    /// </remarks>
    private void StartCondorTask()
    {
        condorTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ =>
            {
                if (IsDead() || GetLifeStats().GetHpPercentage() <= CondorFloorPercent)
                    return ValueTask.CompletedTask;

                for (int i = 0; i < CondorsPerWave; i++)
                    RndSpawnInRange(282378, 1, CondorSpread);

                return ValueTask.CompletedTask;
            },
            TimeSpan.FromMilliseconds(CondorRepeatMillis),
            TimeSpan.FromMilliseconds(CondorRepeatMillis));
    }

    private void CancelTasks()
    {
        if (condorTask != null && !condorTask.IsDone())
        {
            condorTask.Cancel(true);
        }

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
