using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/HM_TiamatWeakenedDragonAI (Estrayl).</summary>
[AIName("hm_tiamat_weakened_dragon")]
public class HM_TiamatWeakenedDragonAI : TiamatWeakenedDragonAI
{
    private List<int> gravityTornadoDistanceLeft = new List<int>();
    private List<int> gravityTornadoDistanceRight = new List<int>();

    public HM_TiamatWeakenedDragonAI(Npc owner)
        : base(owner)
    {
        hpPhases = new HpPhases(50, 25, 20, 15, 5);
    }

    public override void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 50:
                ScheduleDivisiveCreations(60000);
                break;
            case 25:
                ScheduleInfinitePain();
                SpawnGravityCrusher();
                break;
            case 20:
            case 15:
            case 5:
                SpawnGravityCrusher();
                break;
        }
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20922: // Ultimate Atrocity
                SpawnAtrocity(25);
                break;
            case 20924: // Ultimate Atrocity
                SpawnAtrocity(0);
                break;
            case 20926: // Ultimate Atrocity
                SpawnAtrocity(95);
                break;
        }
    }

    private void SpawnAtrocity(int heading)
    {
        Spawn(283238, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)heading);
    }

    protected override int CalculateAtrocitySkillId()
    {
        int[] accumulatedAnglesAndPlayers = new int[2];

        GetKnownList().ForEachPlayer(p =>
        {
            if (PositionUtil.IsInRange(GetOwner(), p, 46))
            {
                accumulatedAnglesAndPlayers[0] += (int)Math.Floor(PositionUtil.CalculateAngleFrom(GetOwner(), p) + 0.5f);
                accumulatedAnglesAndPlayers[1]++;
            }
        });
        if (accumulatedAnglesAndPlayers[1] == 0)
            return base.CalculateAtrocitySkillId();

        int medianAngle = accumulatedAnglesAndPlayers[0] / accumulatedAnglesAndPlayers[1];
        if (medianAngle <= 30 || medianAngle >= 330)
            return 20924;
        else if (medianAngle < 150)
            return 20922;
        else
            return 20926;
    }

    protected override void ScheduleSinkingSand()
    {
        spawnTasks.Add(ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { SpawnSinkingSand(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(60000), TimeSpan.FromMilliseconds(50000)));
    }

    private void SpawnSinkingSand()
    {
        int density = (int)Math.Min((100 - GetLifeStats().GetHpPercentage()) * 1.5f, 100);
        int delay = 250;
        for (int i = 0; i < density; i++)
            spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(_ => { SpawnSinkingSandOnRndPos(); return ValueTask.CompletedTask; }, (long)(delay += 250)));
    }

    private void SpawnSinkingSandOnRndPos()
    {
        float distance = Rnd.Get(60, 420) * 0.1f;
        double radian = (Rnd.Get(-900, 900) * 0.1f) * Math.PI / 180.0;
        float x = (float)(Math.Cos(radian) * distance);
        float y = (float)(Math.Sin(radian) * distance);
        Spawn(283135, GetPosition().GetX() + x, GetPosition().GetY() + y, 417.4f, (sbyte)0);
    }

    protected override void ScheduleDivisiveCreations(int delay)
    {
        spawnTasks.Add(ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (hasAggro.Get() && !IsDead() && delay > 30000)
            {
                Spawn(856042, 464.24f, 462.26f, 417.4f, (sbyte)18);
                Spawn(856042, 542.79f, 465.03f, 417.4f, (sbyte)43);
                Spawn(856042, 541.79f, 563.71f, 417.4f, (sbyte)74);
                Spawn(856042, 465.79f, 565.43f, 417.4f, (sbyte)100);
                ScheduleDivisiveCreations(delay - 10000);
            }
            return ValueTask.CompletedTask;
        }, (long)delay));
    }

    protected override void SpawnGravityCrusher()
    {
        if (gravityTornadoDistanceLeft.Count == 0 || gravityTornadoDistanceRight.Count == 0)
            return;

        int distance = gravityTornadoDistanceRight[Rnd.NextInt(gravityTornadoDistanceRight.Count)];
        gravityTornadoDistanceRight.Remove(distance);
        SpawnGravityTornadoByDistAndAngle(distance, 330);

        distance = gravityTornadoDistanceLeft[Rnd.NextInt(gravityTornadoDistanceLeft.Count)];
        gravityTornadoDistanceLeft.Remove(distance);
        SpawnGravityTornadoByDistAndAngle(distance, 30);
    }

    private void SpawnGravityTornadoByDistAndAngle(int distance, int angle)
    {
        double radian = angle * Math.PI / 180.0;
        float x = (float)(Math.Cos(radian) * distance);
        float y = (float)(Math.Sin(radian) * distance);
        Spawn(856046, GetPosition().GetX() + x, GetPosition().GetY() + y, 417.4f, (sbyte)0);
    }

    protected override void SpawnInfinitePain()
    {
        Spawn(856048, 508.32f, 515.18f, 417.4f, (sbyte)0);
    }

    protected override void DespawnAdds()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        DeleteNpcs(instance.GetNpcs(856045));
        DeleteNpcs(instance.GetNpcs(856041));
        DeleteNpcs(instance.GetNpcs(856042));
        DeleteNpcs(instance.GetNpcs(856046));
    }

    private void AddGravityTornadoSpots()
    {
        gravityTornadoDistanceLeft.Clear();
        gravityTornadoDistanceRight.Clear();
        gravityTornadoDistanceLeft.AddRange(new[] { 7, 14, 21, 28, 35, 42 });
        gravityTornadoDistanceRight.AddRange(new[] { 7, 14, 21, 28, 35, 42 });
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        AddGravityTornadoSpots();
    }

    protected override void HandleBackHome()
    {
        AddGravityTornadoSpots();
        base.HandleBackHome();
    }
}
