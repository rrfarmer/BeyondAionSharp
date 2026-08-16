using System.Collections.Concurrent;
using System.Collections.Generic;
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
    /// <summary>
    /// The sand each thorn throws, by the thorn that throws it — one second at a time.
    /// </summary>
    /// <remarks>
    /// Hard mode runs a structurally identical pattern with its own cast: same two-second opening,
    /// same 3/4/3/4/4 bursts, same widening waits, and a different uplift. The devname does not say
    /// so — 856040 is called <c>…BurrowFX_Hard</c> but binds <c>IDTiamat_Hard_Earthquake_00</c> — so
    /// pointing it at the normal class would have thrown <b>normal-mode sand</b> in the hard fight.
    /// A table keyed by the thorn is what makes that impossible to get wrong by accident.
    /// </remarks>
    private static readonly Dictionary<int, int> UpliftByThorn = new Dictionary<int, int>
    {
        [283057] = 283135, // IDTiamat_BurrowingWorm_BurrowFX -> IDTiamat_Tiamat_Uplift
        [856040] = 856041, // ...BurrowFX_Hard -> BIDTiamat_Tiamat_Uplift_Hard
    };

    private const int Tracked = 1;
    private const int UpliftLife = 1;
    private const float Around = 3f;

    // Retail's FLAGVARI_ALPHA_1..5, one per burst.
    private const int First = 1;
    private const int Second = 2;
    private const int Third = 3;
    private const int Fourth = 4;
    private const int Fifth = 5;

    private static PatternAction Burst(int uplift, int howMany) =>
        Do.SpawnNear(uplift, Tracked, count: howMany, range: Around, liveSeconds: UpliftLife);

    private static readonly ConcurrentDictionary<int, AiPattern> ByNpcId = new ConcurrentDictionary<int, AiPattern>();
    private static readonly AiPattern Nothing = new AiPattern();

    private static AiPattern Build(int npcId)
    {
        if (!UpliftByThorn.TryGetValue(npcId, out int uplift))
            return Nothing;

        return new AiPattern
        {
            OnWakeUp = Of(
                Branch(6, "SetTimer", When.Always,
                    Do.SetIdleTimer(2000))),

            OnIdleTimer = Of(
                Branch(5, "Spawn_#1", [When.FirstTime(First)],
                    Burst(uplift, 3),
                    Do.SetIdleTimer(2000)),

                Branch(4, "Spawn_#2", [When.FirstTime(Second)],
                    Burst(uplift, 4),
                    Do.SetIdleTimer(2500)),

                Branch(3, "Spawn_#3", [When.FirstTime(Third)],
                    Burst(uplift, 3),
                    Do.SetIdleTimer(3000)),

                Branch(2, "Spawn_#4", [When.FirstTime(Fourth)],
                    Burst(uplift, 4),
                    Do.SetIdleTimer(3500)),

                Branch(1, "Spawn_#5", [When.FirstTime(Fifth)],
                    Burst(uplift, 4),
                    Do.DespawnSelf())),
        };
    }

    public TiamatBurrowingThornAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => ByNpcId.GetOrAdd(GetOwner().GetNpcId(), static id => Build(id));
}
