using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Isbariya the Resolute (216182, 216263). Retail pattern <c>IDCT_Boss_ArchPriest</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/beshmundirTemple/IsbariyaTheResoluteAI (@author Luzien). Retail-sourced
/// corrections below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>Every number in his phase ladder was off.</b> Retail's bands are <b>70</b>, <b>49</b> and
/// <b>29</b>; this class used 75, 50 and 25. The mapping is not in doubt — each band sends its own
/// system message, and this class already sent the matching one on each rung.
/// </para>
/// <para>
/// The waves were wrong with them. Retail sends <b>three</b> Taros on the middle band and <b>two</b>
/// shields on the deepest; this class sent five and one. And the rungs re-arm at <b>20</b>, <b>18</b>
/// and <b>8</b> seconds, where this class used 25, 10 and 20 — so the deepest phase, which retail makes
/// the fastest, was its slowest.
/// </para>
/// <para>
/// <b>Not translated.</b> The casts on every rung, which name skill indices; retail's 30% variant on
/// the top band; and its <c>on_enter_idle_state</c>, which places two basic summons at fixed points when
/// he drops combat.
/// </para>
/// </remarks>
[AIName("isbariya")]
public class IsbariyaTheResoluteAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary>Retail's three bands: 70-100, 50-70, 30-49 and below 29.</summary>
    private readonly HpPhases hpPhases = new HpPhases(70, 49, 29);

    /// <summary>Retail's <c>total_set_to_spawn</c> on each wave, and its re-arm on each rung.</summary>
    private const int TarosCount = 3;
    private const int ShieldCount = 2;
    private const int SkeletonRungMillis = 20000;
    private const int TarosRungMillis = 18000;
    private const int ShieldRungMillis = 8000;
    private readonly AtomicBoolean isStart = new AtomicBoolean();
    private readonly List<Point3D> soulLocations = new List<Point3D>();
    private ScheduledTask basicSkillTask;
    private ScheduledTask spawnTask;

    public IsbariyaTheResoluteAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isStart.CompareAndSet(false, true))
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 342051, 1000);
            GetPosition().GetWorldMapInstance().SetDoorState(535, false);
            StartBasicSkillTask();
        }
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 70:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_Boss_ArchPriest_3phase());
                LaunchSpecial();
                break;
            case 49:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_Boss_ArchPriest_2phase());
                break;
        }
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        soulLocations.Add(new Point3D(1580.5f, 1572.8f, 304.64f));
        soulLocations.Add(new Point3D(1582.1f, 1571.2f, 304.64f));
        soulLocations.Add(new Point3D(1583.3f, 1569.9f, 304.64f));
        soulLocations.Add(new Point3D(1585.3f, 1568.1f, 304.64f));
        soulLocations.Add(new Point3D(1586.4f, 1567.1f, 304.64f));
        soulLocations.Add(new Point3D(1588.3f, 1566.2f, 304.64f));
    }

    protected override void HandleDied()
    {
        CancelTasks(spawnTask, basicSkillTask);
        base.HandleDied();
        PacketSendUtility.BroadcastMessage(GetOwner(), 342055);
        GetPosition().GetWorldMapInstance().SetDoorState(535, true);
    }

    protected override void HandleDespawned()
    {
        CancelTasks(spawnTask, basicSkillTask);
        base.HandleDespawned();
    }

    protected override void HandleBackHome()
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 342056);
        CancelTasks(spawnTask, basicSkillTask);
        base.HandleBackHome();
        isStart.Set(false);
        GetPosition().GetWorldMapInstance().SetDoorState(535, true);
        hpPhases.Reset();
    }

    private void StartBasicSkillTask()
    {
        basicSkillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
                CancelTasks(basicSkillTask);
            else
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 18912 + Rnd.NextInt(2), 55, GetOwner()).UseNoAnimationSkill();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(24000));
    }

    private void LaunchSpecial()
    {
        if (IsDead() || hpPhases.GetCurrentPhase() == 0 || GetOwner().GetPosition().GetWorldMapInstance() == null)
        {
            CancelTasks(basicSkillTask);
            return;
        }
        int delay = 10000;

        switch (hpPhases.GetCurrentPhase())
        {
            case 1:
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 18959, 50, GetRandomTarget()).UseNoAnimationSkill();
                SpawnSouls();
                delay = SkeletonRungMillis;
                break;
            case 2:
                RndSpawn(281660, TarosCount);
                delay = TarosRungMillis;
                break;
            case 3:
                RndSpawn(281659, ShieldCount);
                AIActions.UseSkill(this, 18993);
                delay = ShieldRungMillis;
                break;
        }
        ScheduleSpecial(delay);
    }

    /// <summary>Retail <c>IDCT_Boss_ArchPriest</c> gives the skeletons thirty seconds.</summary>
    private const int SkeletonLife = 30;

    private void RndSpawn(int npcId, int count)
    {
        for (int i = 0; i < count; i++)
            RndSpawnInRange(npcId, 5);
    }

    private void SpawnSouls()
    {
        List<Point3D> points = new List<Point3D>(soulLocations);
        int count = Rnd.Get(3, 6);
        for (int i = 0; i < count; i++)
        {
            if (points.Count != 0)
            {
                int idx = Rnd.NextInt(points.Count);
                Point3D spawn = points[idx];
                points.RemoveAt(idx);
                SpawnFor(281645, spawn.GetX(), spawn.GetY(), spawn.GetZ(), (sbyte)18, SkeletonLife);
            }
        }
    }

    private Creature GetRandomTarget()
    {
        return GetAggroList().GetTarget(AggroTarget.RANDOM_EXCEPT_CURRENT_TARGET, 40);
    }

    private void ScheduleSpecial(int delay)
    {
        spawnTask = ThreadPoolManager.GetInstance().Schedule(_ => { LaunchSpecial(); return ValueTask.CompletedTask; }, (long)delay);
    }

    private void CancelTasks(params ScheduledTask[] tasks)
    {
        foreach (ScheduledTask task in tasks)
            if (task != null && !task.IsCancelled)
                task.Cancel(true);
    }
}
