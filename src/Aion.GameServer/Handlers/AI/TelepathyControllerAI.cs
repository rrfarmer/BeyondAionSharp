using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/darkPoeta/TelepathyControllerAI (Ritsu, Estrayl).</summary>
[AIName("telepathy_controller")]
public class TelepathyControllerAI : AggressiveNpcAI
{
    private ScheduledTask spawnTask;
    private readonly AtomicBoolean isAggred = new AtomicBoolean();

    public TelepathyControllerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isAggred.CompareAndSet(false, true))
        {
            lock (isAggred)
            {
                if (isAggred.Get())
                {
                    spawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
                    {
                        if (!IsDead())
                        {
                            Npc spawn = (Npc)RndSpawnInRange(Rnd.NextInt(2) == 0 ? 281150 : 281334, 7, 10);
                            spawn.GetKnownList().ForEachPlayer(p => spawn.GetAggroList().AddHate(p, 10));
                        }
                        return ValueTask.CompletedTask;
                    }, System.TimeSpan.FromMilliseconds(60000), System.TimeSpan.FromMilliseconds(60000));
                }
            }
        }
    }

    private void CancelTask()
    {
        if (spawnTask != null && !spawnTask.IsCancelled)
            spawnTask.Cancel(true);
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        lock (isAggred)
        {
            CancelTask();
            isAggred.Set(false);
        }
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
