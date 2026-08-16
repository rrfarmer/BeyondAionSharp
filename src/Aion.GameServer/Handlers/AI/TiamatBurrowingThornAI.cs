using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The burrowing thorn Tiamat leaves in the sand (283057). Retail pattern
/// <c>IDTiamat_BurrowingWorm_BurrowFX</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. It appears, and then throws up five bursts of
/// sand — three, four, three, four, four hazards — at widening intervals before removing itself.
/// Nothing in our server spawned it, and the hazard it throws (283135) was spawned only by the boss
/// directly.
/// <para>
/// <b>This is what aionemu's "sinking sand" was approximating.</b> The old
/// <c>TiamatWeakenedDragonAI.ScheduleSinkingSand</c> puts 283135 out itself, every two minutes, in a
/// hand-computed arc from -25° to +25° at seven distances. Retail never has the boss place that
/// hazard at all: the boss places <i>thorns</i> at fixed marks and each thorn throws its own sand.
/// The arc is aionemu inventing a shape for a mechanic whose real shape is the thorn coordinates in
/// <see cref="TiamatRotation"/>.
/// </para>
/// <para>
/// Ported on its own here because it is self-contained and because the boss's rotation depends on
/// it. Until that rotation is wired, nothing places these thorns and this class is inert — which is
/// the honest state: the piece is built and the thing that calls it is not.
/// </para>
/// <para>
/// The one-shot flags are retail's own, and they are what makes this a sequence rather than a loop:
/// each rung fires once, arms a longer wait than the last, and the fifth removes the thorn.
/// </para>
/// </remarks>
[AIName("tiamat_burrowing_thorn")]
public class TiamatBurrowingThornAI : PatternAi
{
    /// <summary>283135, <c>IDTiamat_Tiamat_Uplift</c> — the sand itself, one second at a time.</summary>
    private const int Uplift = 283135;

    private const int Tracked = 1;
    private const int UpliftLife = 1;
    private const float Around = 3f;

    // Retail's FLAGVARI_ALPHA_1..5, one per burst.
    private const int First = 1;
    private const int Second = 2;
    private const int Third = 3;
    private const int Fourth = 4;
    private const int Fifth = 5;

    private static PatternAction Burst(int howMany) =>
        Do.SpawnNear(Uplift, Tracked, count: howMany, range: Around, liveSeconds: UpliftLife);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnWakeUp = Of(
            Branch(6, "SetTimer", When.Always,
                Do.SetIdleTimer(2000))),

        OnIdleTimer = Of(
            Branch(5, "Spawn_#1", [When.FirstTime(First)],
                Burst(3),
                Do.SetIdleTimer(2000)),

            Branch(4, "Spawn_#2", [When.FirstTime(Second)],
                Burst(4),
                Do.SetIdleTimer(2500)),

            Branch(3, "Spawn_#3", [When.FirstTime(Third)],
                Burst(3),
                Do.SetIdleTimer(3000)),

            Branch(2, "Spawn_#4", [When.FirstTime(Fourth)],
                Burst(4),
                Do.SetIdleTimer(3500)),

            Branch(1, "Spawn_#5", [When.FirstTime(Fifth)],
                Burst(4),
                Do.DespawnSelf())),
    };

    public TiamatBurrowingThornAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
