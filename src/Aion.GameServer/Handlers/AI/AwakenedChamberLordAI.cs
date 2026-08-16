using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The three awakened chamber lords — Krotan Lord (215136), Kysis Duke (215179) and Miren Prince
/// (215222). Retail pattern <c>BGuard_ChiefD</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All three HERO, all three on plain
/// <c>aggressive</c>, and everything the pattern calls up was spawned by nothing anywhere: the
/// illusion gate (281226) and the dredgion elite fighters (296338, 296339).
/// <list type="bullet">
/// <item><b>below 25</b> — an illusion gate opens at its feet and stands for ten minutes</item>
/// <item><b>on dying</b> — six drakan arrive by teleporter, two at each of three points, and three
/// more come through the barrier. The teleported six last eighteen seconds and the barrier three
/// twelve, so this is a parting shot rather than a second fight.</item>
/// </list>
/// <para>
/// <b>On trusting absolute coordinates under three owners.</b> The death spawns are placed absolutely,
/// and a pattern with three owners normally makes those unusable — the standing rule is to check
/// single ownership first. Here the check passes for a reason worth recording: the three chambers are
/// separate maps (300140000, 300120000, 300130000) that <b>share one layout</b>. Each lord stands at
/// (526.4, 845.3) in its own map and the coordinate ranges match across all three, so one set of
/// points serves them all. Multi-owner absolute coordinates are safe exactly when the owners' maps
/// agree, and that is checkable from our own spawn data rather than assumed.
/// </para>
/// <para>
/// The pattern's z of 200 is nominal — the chambers' own spawns sit around 190 — so the drakan are
/// placed at the lord's own height instead, which is the floor they are arriving on.
/// </para>
/// <para>
/// <b>The casts are not translated.</b> Ten indices are addressed and the pattern has no branch
/// comments at all. Omitted with them: the three cast-only band timers (T2 below 25, T3 at 26-50, T4
/// at 51-75), each with a coin-flip pair, and the timer-1 band at full health.
/// <para>
/// The two one-shots at 26-50 and 51-75 are kept as bare re-arms for structural fidelity, and — unlike
/// the gateway guards' empty rungs — they are <b>inert</b>. There the empty rungs mattered because a
/// deeper trap rung would otherwise match on the tick they consumed; here the only rung that does
/// anything is guarded below 25, which cannot overlap 26-50 or 51-75, and both empty branches do
/// exactly what the catch-all beneath them does. Mutation testing confirmed it: removing them changes
/// nothing observable. They are kept so the table still reads against the pattern, not because they
/// carry behaviour.
/// </para>
/// <para>
/// <b>Also not translated:</b> the world flag the pattern sets on engaging and on dying, which nothing
/// on our side reads; the broadcast on leaving the fight; and the <c>on_message</c> handler for 6682
/// that dismisses it, which no ported NPC sends.
/// </para>
/// </remarks>
[AIName("awakened_chamber_lord")]
public class AwakenedChamberLordAI : PatternAi
{
    private const int IllusionGate = 281226;
    private const int DrakanByTeleporter = 296339;
    private const int DrakanByBarrier = 296338;

    /// <summary>Ten minutes, three metres out.</summary>
    private const int GateLife = 600;
    private const float AtItsFeet = 3f;

    private const int TeleportedLife = 18;
    private const int BarrierLife = 12;

    // Retail's ALPHA_1..3, one per band, each entered once.
    private const int Below25 = 1;
    private const int Band26To50 = 2;
    private const int Band51To75 = 3;

    /// <summary>Retail broadcasts the disengage call to fifty metres.</summary>
    private const float GateCallRange = 50f;

    /// <summary>
    /// The three arrival points and the barrier. Shared by all three chambers — see the class remarks
    /// on why that is safe here.
    /// </summary>
    private static readonly (float X, float Y)[] TeleportPoints =
        [(496f, 847f), (529f, 874f), (554f, 850f)];

    private static readonly (float X, float Y) BarrierPoint = (580f, 840f);

    /// <summary>Places the death wave at the lord's own height rather than the pattern's nominal z.</summary>
    private static readonly PatternAction DeathWave = ai =>
    {
        float z = ai.GetOwner().GetZ();
        foreach ((float x, float y) in TeleportPoints)
            ai.SpawnAt(DrakanByTeleporter, 0, TeleportedLife,
                new SpawnSpot(x, y, z, 0), new SpawnSpot(x, y, z, 0));

        ai.SpawnAt(DrakanByBarrier, 0, BarrierLife,
            new SpawnSpot(BarrierPoint.X, BarrierPoint.Y, z, 0),
            new SpawnSpot(BarrierPoint.X, BarrierPoint.Y, z, 0),
            new SpawnSpot(BarrierPoint.X, BarrierPoint.Y, z, 0));
    };

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(14, "", When.Always,
                Do.ArmTimer(0, 5000))),

        OnBattleTimer = Of(
            Branch(11, "the gate", [When.Timer(0), When.HpBelow(25), When.FirstTime(Below25)],
                Do.ArmTimer(0, 5000),
                Do.SpawnNear(IllusionGate, 0, count: 1, range: AtItsFeet, liveSeconds: GateLife)),

            // Cast-only in retail, kept because each spends the tick it fires on.
            Branch(10, "", [When.Timer(0), When.HpBetween(26, 50), When.FirstTime(Band26To50)],
                Do.ArmTimer(0, 5000)),
            Branch(9, "", [When.Timer(0), When.HpBetween(51, 75), When.FirstTime(Band51To75)],
                Do.ArmTimer(0, 5000)),

            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, 5000))),

        // Retail's on_leave_attack_state. The gate it opened listens for this and shuts itself, so
        // resetting the lord clears its reinforcements too — see IllusionGateAI.
        OnLeaveAttack = Of(
            Branch(12, "call the gate down", When.Always,
                Do.Broadcast(IllusionGateAI.LordDisengaged, GateCallRange))),

        OnDie = Of(
            Branch(7, "the parting shot", When.Always, DeathWave)),
    };

    public AwakenedChamberLordAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
