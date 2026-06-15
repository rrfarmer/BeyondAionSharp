using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/nightmareCircus/CircusBoxesAI (@author Ritsu).</summary>
[AIName("circusboxes")]
public class CircusBoxesAI : NpcAI
{
    private ScheduledTask spawnJesterTask;

    public CircusBoxesAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SpawnJester();
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelspawnJesterTask();
        DespawnNpcs(233462, 233463);
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1501144);
        CancelspawnJesterTask();
    }

    private void CancelspawnJesterTask()
    {
        if (spawnJesterTask != null && !spawnJesterTask.IsDone())
        {
            spawnJesterTask.Cancel(true);
        }
    }

    private void SpawnJester()
    {
        spawnJesterTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                GetOwner().GetController().Delete();
                int count = Rnd.Get(3, 5);
                for (int i = 0; i < count; i++)
                {
                    switch (GetOwner().GetNpcId())
                    {
                        case 831348:
                            RndSpawnInRange(233462, 3, 8);
                            break;
                        case 831349:
                            RndSpawnInRange(233463, 3, 8);
                            break;
                        default:
                            return ValueTask.CompletedTask;
                    }
                }
            }

            return ValueTask.CompletedTask;
        }, 33000L);
    }

    private void DespawnNpcs(params int[] npcIds)
    {
        foreach (Npc npc in GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(npcIds))
        {
            npc.GetController().Delete();
        }
    }
}
