using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/ThickDustAI (Estrayl).</summary>
[AIName("thick_dust")]
public class ThickDustAI : NpcAI
{
    public ThickDustAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, 10000L);
    }
}
