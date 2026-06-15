using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/quests/GarnonQ20060AI (@author Cheatkiller).</summary>
[AIName("Q20060")]
public class GarnonQ20060AI : NpcAI
{
    private ScheduledTask? task;

    public GarnonQ20060AI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        if (task != null && !task.IsDone())
            task.Cancel(true);
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Despawn();
    }

    private void Despawn()
    {
        task = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            Spawn(800020, 442.279f, 464.349f, 341.520f, (sbyte)20);
            GetOwner().GetController().Delete();
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 60000L * 3);
    }
}
