using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/darkPoeta/MarabataAI (xTz).</summary>
[AIName("marabata")]
public class MarabataAI : AggressiveNpcAI
{
    private ScheduledTask boosterLifeCheckTask;
    private readonly AtomicBoolean isStarted = new AtomicBoolean();

    public MarabataAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isStarted.CompareAndSet(false, true))
        {
            boosterLifeCheckTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct =>
            {
                if (IsDead())
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                switch (GetNpcId())
                {
                    case 214849:
                        if (!IsBoosterAlive(700439))
                            Spawn(700439, 665.374f, 372.751f, 99.375f, (sbyte)90);
                        if (!IsBoosterAlive(700440))
                            Spawn(700440, 681.851f, 408.625f, 100.472f, (sbyte)13);
                        if (!IsBoosterAlive(700441))
                            Spawn(700441, 646.550f, 406.088f, 99.375f, (sbyte)49);
                        break;
                    case 214850:
                        if (!IsBoosterAlive(700442))
                            Spawn(700442, 636.118f, 325.537f, 99.375f, (sbyte)49);
                        if (!IsBoosterAlive(700443))
                            Spawn(700443, 676.257f, 319.650f, 99.375f, (sbyte)4);
                        if (!IsBoosterAlive(700444))
                            Spawn(700444, 655.851f, 292.711f, 99.375f, (sbyte)90);
                        break;
                    case 214851:
                        if (!IsBoosterAlive(700445))
                            Spawn(700445, 605.625f, 380.479f, 99.375f, (sbyte)14);
                        if (!IsBoosterAlive(700446))
                            Spawn(700446, 598.706f, 345.978f, 99.375f, (sbyte)98);
                        if (!IsBoosterAlive(700447))
                            Spawn(700447, 567.775f, 366.207f, 99.375f, (sbyte)59);
                        break;
                }
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, System.TimeSpan.FromMilliseconds(30000), System.TimeSpan.FromMilliseconds(30000));
        }
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        switch (GetNpcId())
        {
            case 214849:
                Spawn(700439, 665.374f, 372.751f, 99.375f, (sbyte)90);
                Spawn(700440, 681.851f, 408.625f, 100.472f, (sbyte)13);
                Spawn(700441, 646.550f, 406.088f, 99.375f, (sbyte)49);
                break;
            case 214850:
                Spawn(700442, 636.118f, 325.537f, 99.375f, (sbyte)49);
                Spawn(700443, 676.257f, 319.650f, 99.375f, (sbyte)4);
                Spawn(700444, 655.851f, 292.711f, 99.375f, (sbyte)90);
                break;
            case 214851:
                Spawn(700445, 605.625f, 380.479f, 99.375f, (sbyte)14);
                Spawn(700446, 598.706f, 345.978f, 99.375f, (sbyte)98);
                Spawn(700447, 567.775f, 366.207f, 99.375f, (sbyte)59);
                break;
        }
    }

    private bool IsBoosterAlive(int boosterId)
    {
        return GetPosition().GetWorldMapInstance().GetNpc(boosterId) != null;
    }

    private void CancelTask()
    {
        if (boosterLifeCheckTask != null && !boosterLifeCheckTask.IsCancelled)
            boosterLifeCheckTask.Cancel(true);
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTask();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTask();
    }
}
