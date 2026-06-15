using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/abyss/IllusionGateAI (@author Estrayl, since AION 4.8).
/// </summary>
[AIName("illusion_gate")]
public class IllusionGateAI : NpcAI
{
    private ScheduledTask spawnTask;

    public IllusionGateAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        spawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            for (int i = 0; i < 2; i++)
            {
                RndSpawnInRange(GetNpcId() + Rnd.Get(1, 3), 0, 1);
            }

            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(10000), TimeSpan.FromMilliseconds(30000));
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

    private void CancelTask()
    {
        if (spawnTask != null && !spawnTask.IsCancelled)
            spawnTask.Cancel(true);
    }
}
