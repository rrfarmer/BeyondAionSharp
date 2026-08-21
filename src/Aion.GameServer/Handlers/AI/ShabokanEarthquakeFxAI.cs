using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Shabokan's earthquake (283081). Retail pattern <c>IDTiamat_Shavorkhan_EarthQuakeFX</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The earthquake is a train of five, not one shock.</b> Retail's FX waits a second, drops an
/// <c>EarthQuakeDMG</c> at its own point for five seconds, and repeats every two — five rungs, each
/// carrying its own test-and-set flag var so the sequence runs once through and then the FX despawns
/// itself.
/// </para>
/// <para>
/// <b>This port skipped the FX entirely</b> and had Shabokan place the damage npc directly, once. So
/// the ground shook a fifth as much as it should, and the npc retail actually spawns — 283081 — was
/// bound to <c>general</c> and placed by nothing.
/// </para>
/// <para>
/// Note the arithmetic: retail gives the FX eight seconds and its fifth tick falls at nine, so the last
/// one is cut off by its own lifetime. That is retail's, not an error here, and the count is left as
/// retail wrote it.
/// </para>
/// </remarks>
[AIName("shabokan_earthquake_fx")]
public class ShabokanEarthquakeFxAI : NpcAI
{
    /// <summary><c>IDTiamat_Shavorkhan_EarthQuakeDMG</c>, and retail's <c>live_time</c> for it.</summary>
    private const int DamageNpc = 283082;
    private const int DamageLife = 5;

    /// <summary>Retail's idle timer: one second to the first tick, two between.</summary>
    private const long FirstTickMillis = 1000L;
    private const long TickMillis = 2000L;

    /// <summary>Retail writes five rungs, each guarded by its own flag var.</summary>
    private const int Ticks = 5;

    private ScheduledTask? tickTask;

    public ShabokanEarthquakeFxAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        Arm(FirstTickMillis, 0);
    }

    private void Arm(long delay, int tick)
    {
        tickTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!GetOwner().IsSpawned() || tick >= Ticks)
                return ValueTask.CompletedTask;

            SpawnFor(DamageNpc, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
                (sbyte)0, DamageLife);

            if (tick + 1 >= Ticks)
                AIActions.DeleteOwner(this);
            else
                Arm(TickMillis, tick + 1);

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
