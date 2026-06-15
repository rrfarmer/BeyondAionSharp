using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/siege/MineAI (@author Source).</summary>
[AIName("siege_mine")]
public class MineAI : SiegeNpcAI
{
    public MineAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        AIActions.UseSkill(this, 18407);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            AIActions.DeleteOwner(this);
            return ValueTask.CompletedTask;
        }, 1500L);
    }
}
