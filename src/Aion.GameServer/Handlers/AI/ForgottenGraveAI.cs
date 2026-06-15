using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/ForgottenGraveAI (Tibald).</summary>
[AIName("forgottengrave")]
public class ForgottenGraveAI : GeneralNpcAI
{
    private readonly AtomicBoolean isSpawned = new AtomicBoolean();

    public ForgottenGraveAI(Npc owner)
        : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        if (isSpawned.CompareAndSet(false, true))
        {
            ScheduleAddDespawn((Npc)RndSpawnInRange(283906, 1, 2));
            ScheduleAddDespawn((Npc)RndSpawnInRange(283906, 1, 2));
        }
    }

    private void ScheduleAddDespawn(Npc npc)
    {
        npc.GetController().AddTask(TaskId.DESPAWN,
            ThreadPoolManager.GetInstance().Schedule(_ => { npc.GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, TimeSpan.FromMinutes(5)));
    }
}
