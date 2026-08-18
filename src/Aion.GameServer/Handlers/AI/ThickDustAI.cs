using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/ThickDustAI (Estrayl).</summary>
[AIName("thick_dust")]
public class ThickDustAI : NpcAI
{
    /// <summary>Retail <c>IDTiamat_Tiamat_Dust</c> gives this six seconds; Java used ten.</summary>
    /// <remarks>
    /// <b>The number belongs here, not in the summoner.</b> An earlier pass gave Tiamat's spawn call a
    /// six-second lifetime, which worked only because it was shorter than this class's own ten and so
    /// won the race — two clocks for one add, and the visible one was whichever happened to be smaller.
    /// </remarks>
    private const long DustLifeMillis = 6000L;

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
        }, DustLifeMillis);
    }
}
