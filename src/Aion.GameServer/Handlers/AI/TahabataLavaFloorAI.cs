using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tahabata's lava floor (283116, 283118, 283120). Retail patterns
/// <c>IDTiamat_Tahabata_Area1_FX</c> through <c>_Area3_FX</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The floor burns for as long as it is there.</b> Retail's FX sets a two-second idle timer on
/// waking and then drops its <c>_Dmg</c> twin — 283117, 283119, 283121 — at its own point for three
/// seconds, re-arming at one second. So it pulses every second, and it carries no <c>live_time</c>: it
/// stands until Tahabata's next health rung despawns it.
/// </para>
/// <para>
/// <b>This port spawned the FX and one damage npc together, once</b>, ten seconds after the rung fired.
/// A floor that ticks once is not the mechanic — the phase is meant to be a place you cannot stand.
/// </para>
/// </remarks>
[AIName("tahabata_lava_floor")]
public class TahabataLavaFloorAI : NpcAI
{
    /// <summary>Retail's idle timer: two seconds to the first tick, one between.</summary>
    private const long FirstTickMillis = 2000L;
    private const long TickMillis = 1000L;

    /// <summary>Retail's <c>live_time</c> on each damage npc.</summary>
    private const int DamageLife = 3;

    private ScheduledTask tickTask;

    public TahabataLavaFloorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Arm(FirstTickMillis);
    }

    private void Arm(long delay)
    {
        tickTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!GetOwner().IsSpawned())
                return ValueTask.CompletedTask;

            // Each floor's damage npc is the next id up: Area1_FX 283116 pairs with Area1_Dmg 283117.
            SpawnFor(GetNpcId() + 1, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
                (sbyte)0, DamageLife);
            Arm(TickMillis);
            return ValueTask.CompletedTask;
        }, delay);
    }

    private void Stop()
    {
        if (tickTask != null && !tickTask.IsDone())
            tickTask.Cancel(true);
        tickTask = null;
    }

    protected override void HandleDespawned()
    {
        Stop();
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT => false,
            _ => base.Ask(question),
        };
    }
}
