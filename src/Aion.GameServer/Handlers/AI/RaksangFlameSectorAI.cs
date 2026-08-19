using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// One floor marker of a Raksang flame quadrant (282455-282458). Retail patterns
/// <c>IDRaksha_NoshowNPC_11</c> through <c>_14</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>Retail's chain.</b> A fire deliverer (282451-282454, <c>Raksha_Deliverfire</c>) walks its route,
/// and at the last waypoint casts, <b>broadcasts 12501 at eighty metres</b> and despawns. The thirty-two
/// markers of that quadrant are permanent invisible npcs on the floor; each hears 12501 and puts a
/// <b>torment blaze</b> (282459) on itself for <b>ten seconds</b>. That is the quadrant catching fire.
/// </para>
/// <para>
/// <b>This port had the markers and nothing else.</b> <see cref="ScaldingExecutorAI"/> places all
/// thirty-two at the quadrant's own coordinates when its executor arrives — which is the right places,
/// hard-coded, because our spawn tables do not carry retail's permanent floor markers. But they were
/// bound to <c>general</c>: <b>no flame, no lifetime, no behaviour</b>. The executor's whole trip left
/// thirty-two invisible npcs standing on the floor for the rest of the instance and lit nothing.
/// </para>
/// <para>
/// Being placed <i>is</i> this port's version of hearing 12501 — the marker only exists because the
/// deliverer arrived — so the blaze goes down on spawn, and the marker leaves with it.
/// </para>
/// <para>
/// <b>Not translated: the damage.</b> Retail's blaze (<c>IDRaksha_NoshowNPC_15</c>) casts
/// <c>SKILLI_INDEX_0</c> on waking and on seeing a player, and broadcasts <b>12505 at fifty metres</b> so
/// neighbouring blazes fire too. Our 282459 is bound <c>general</c> with no skill row, so the blaze is
/// scenery here and the burn does no damage. That is the same skill-index gap logged against Tiamat's
/// hard-mode uplift.
/// </para>
/// </remarks>
[AIName("raksang_flame_sector")]
public class RaksangFlameSectorAI : NpcAI
{
    /// <summary><c>BIDRaksha_BossFlame</c>, and retail's <c>live_time</c> for it.</summary>
    private const int TormentBlaze = 282459;
    private const int BlazeLife = 10;

    public RaksangFlameSectorAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();

        // Retail's spawn_range is 1, so the blaze lands on the marker rather than around it.
        SpawnFor(TormentBlaze, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
            (sbyte)GetOwner().GetHeading(), BlazeLife);
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
