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

/// <summary>Java parity: ai/instance/beshmundirTemple/IsbariyaTheResoluteAI (@author Luzien).</summary>
[AIName("isbariya")]
public class IsbariyaTheResoluteAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(75, 50, 25);
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
            case 75:
                PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDCatacombs_Boss_ArchPriest_3phase());
                LaunchSpecial();
                break;
            case 50:
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
                delay = 25000;
                break;
            case 2:
                RndSpawn(281660, 5);
                break;
            case 3:
                RndSpawn(281659, 1);
                AIActions.UseSkill(this, 18993);
                delay = 20000;
                break;
        }
        ScheduleSpecial(delay);
    }

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
                Spawn(281645, spawn.GetX(), spawn.GetY(), spawn.GetZ(), (sbyte)18);
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
