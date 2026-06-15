using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/empyreanCrucible/TakunGojiraAI (Luzien).</summary>
[AIName("takun_gojira")]
public class TakunGojiraAI : AggressiveNpcAI
{
    private Npc counterpart;

    public TakunGojiraAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(
            _ =>
            {
                counterpart = GetPosition().GetWorldMapInstance().GetNpc(GetNpcId() == 217596 ? 217597 : 217596);
                if (counterpart != null)
                    GetAggroList().AddHate(counterpart, 1000000);
                return ValueTask.CompletedTask;
            },
            500L);
    }
}
