using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rakes/SteelRakeKeyBoxAI (xTz).</summary>
[AIName("steel_rake_key_box")]
public class SteelRakeKeyBoxAI : ChestAI
{
    public SteelRakeKeyBoxAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (!IsDead() && GetOwner().IsSpawned())
            {
                AIActions.DeleteOwner(this);
            }
            return ValueTask.CompletedTask;
        }, 180000L);
    }
}
