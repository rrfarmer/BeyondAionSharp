using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/beshmundirTemple/IsbariyaServantsAI (Luzien). TODO: Random aggro switch.</summary>
[AIName("isbariyaServants")]
public class IsbariyaServantsAI : AggressiveNpcAI
{
    public IsbariyaServantsAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        int lifetime = GetNpcId() == 281659 ? 20000 : 10000;
        ToDespawn(lifetime);
    }

    private void ToDespawn(int delay)
    {
        ThreadPoolManager.GetInstance().Schedule(
            _ =>
            {
                AIActions.DeleteOwner(this);
                return ValueTask.CompletedTask;
            },
            (long)delay);
    }
}
