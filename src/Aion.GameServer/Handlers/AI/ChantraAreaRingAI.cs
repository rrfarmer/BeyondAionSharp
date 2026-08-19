using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Chantra's area ring (283092, 283094). Retail patterns <c>IDTiamat_Chantra_AreaA_FX</c> and
/// <c>_AreaB_FX</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// The ring is the warning and the <b>after-ring</b> is the hazard. Retail's ring sets a three-second
/// idle timer on waking and then places its own <c>_After</c> twin — 283171 for the A ring, 283172 for
/// the B — for four seconds. Both npcs live four seconds, so the warning and the hazard overlap by one.
/// </para>
/// <para>
/// <b>The boss used to do this itself, five seconds after placing the ring, and delete the ring by
/// hand.</b> That put the hazard down a second late, left the ring standing a second too long, and gave
/// the after-ring no lifetime at all — it stayed until something else removed it. Retail hangs the whole
/// thing off the ring, which is where it is now.
/// </para>
/// </remarks>
[AIName("chantra_area_ring")]
public class ChantraAreaRingAI : NpcAI
{
    /// <summary>The hazard each ring leaves, and where retail places it.</summary>
    private const int AfterRingA = 283171;
    private const int AfterRingB = 283172;
    private const float RingX = 1031.1f;
    private const float RingY = 466.38f;
    private const float RingZ = 445.45f;

    /// <summary>Retail's <c>set_idle_timer</c> on the ring, and the <c>live_time</c> on what it leaves.</summary>
    private const long AfterRingDelayMillis = 3000L;
    private const int AfterRingLife = 4;

    private ScheduledTask afterTask;

    public ChantraAreaRingAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        int after = GetNpcId() == 283092 ? AfterRingA : AfterRingB;
        afterTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (GetOwner().IsSpawned())
                SpawnFor(after, RingX, RingY, RingZ, (sbyte)0, AfterRingLife);
            return ValueTask.CompletedTask;
        }, AfterRingDelayMillis);
    }

    private void Stop()
    {
        if (afterTask != null && !afterTask.IsDone())
            afterTask.Cancel(true);
        afterTask = null;
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
