using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Prectaz (219934), the Enshar world boss. Retail pattern <c>DF5_ItemNamed_24_SSH</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. LEGENDARY, on plain <c>aggressive</c>. Below 35% it
/// puts out <b>eight tentacles</b> — both kinds are HERO-rated and neither was spawned by anything
/// anywhere in the server:
/// <list type="bullet">
/// <item>855911 on the four cardinals, eighteen metres out</item>
/// <item>856067 on the four diagonals, ten metres out</item>
/// </list>
/// They live fifty seconds, and the chain brings the summon back round roughly every seventy-three.
/// <para>
/// Three bands, each a T0 → T1 → T2 → T3 → T5 → T0 loop at its own speed: 10/11/14/10/14 above 85,
/// 14/17/14/10/10 between 35 and 85, and 25/10/14/10/14 below 35 where the first step is the summon.
/// </para>
/// <para>
/// <b>A dead branch, reproduced by omission.</b> Retail writes the summon twice with <i>identical</i>
/// guards — same timer, same health test, no probability on either — and the two differ only in
/// geometry: the higher-priority one puts the cardinals at eighteen metres and the diagonals at ten,
/// the lower one swaps them. Branches are first-match-wins, so the second can never run. Only the
/// first arrangement is translated. The same duplication appears in the message handlers, where two
/// branches both answer message 55003.
/// </para>
/// <para>
/// <b>The casts are not translated.</b> Eight indices are addressed and our <c>npc_skills</c> carries
/// <b>five</b> skills, so there is nothing to map them onto. The chains above 35 are kept anyway, as
/// Gatekeeper Flox's were and unlike the Golden Tatars': they are what brings timer 0 back round, and
/// timer 0 below 35 is where the tentacles come from. A boss fought down from full would never reach
/// them otherwise.
/// </para>
/// <para>
/// <b>Not translated:</b> timer 10, a three-second heartbeat that broadcasts message 100001 to
/// tentacles whose own pattern is not ported — a forever-ticking timer with no listener, so it is not
/// armed; the <c>on_message</c> handlers for 55001-55003, where the tentacles call him and he answers
/// with a frontal area attack toward the caller (indices 7 and 3, both unresolvable); index 0, the
/// spawn buff; and timer 4, which three branches arm and no branch answers.
/// </para>
/// </remarks>
[AIName("prectaz")]
public class PrectazAI : PatternAi
{
    private const int CardinalTentacle = 855911;
    private const int DiagonalTentacle = 856067;

    /// <summary>Retail's <c>SPAWN_ID_1</c>.</summary>
    private const int Tentacles = 1;

    private const int TentacleLife = 50;

    private static PatternAction Cardinal(float dx, float dy) =>
        Do.SpawnOffset(CardinalTentacle, Tentacles, dx, dy, TentacleLife);

    private static PatternAction Diagonal(float dx, float dy) =>
        Do.SpawnOffset(DiagonalTentacle, Tentacles, dx, dy, TentacleLife);

    /// <summary>A link of a band's loop. The casts do not resolve, so this is the timing alone.</summary>
    private static PatternBranch Step(int priority, int on, PatternCondition band, int next, int delay)
        => Branch(priority, "", [When.Timer(on), band], Do.ArmTimer(next, delay));

    private static readonly PatternCondition Healthy = When.HpBetween(85, 100);
    private static readonly PatternCondition Middle = When.HpBetween(35, 85);
    private static readonly PatternCondition Low = When.HpBelow(35);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timer 10 here, the heartbeat that calls to the tentacles.
        OnEnterAttack = Of(
            Branch(90, "", When.Always, Do.ArmTimer(0, 6000))),

        OnBattleTimer = Of(
            // --- 85-100 ------------------------------------------------------------------------------
            Step(50, on: 0, Healthy, next: 1, delay: 10000),
            Step(49, on: 1, Healthy, next: 2, delay: 11000),
            Step(48, on: 2, Healthy, next: 3, delay: 14000),
            Step(47, on: 3, Healthy, next: 5, delay: 10000),
            Step(46, on: 5, Healthy, next: 0, delay: 14000),

            // --- 35-85 -------------------------------------------------------------------------------
            Step(40, on: 0, Middle, next: 1, delay: 14000),
            Step(39, on: 1, Middle, next: 2, delay: 17000),
            Step(38, on: 2, Middle, next: 3, delay: 14000),
            Step(37, on: 3, Middle, next: 5, delay: 10000),
            Step(35, on: 5, Middle, next: 0, delay: 10000),

            // --- below 35: the tentacles ---------------------------------------------------------------
            // Retail's second copy of this branch, with the distances swapped, carries the same guards
            // and so can never match. Only this arrangement exists.
            Branch(20, "summon", [When.HpBelow(35), When.Timer(0), Low],
                Do.ArmTimer(1, 25000),
                Cardinal(18f, 0f),
                Cardinal(0f, 18f),
                Cardinal(-18f, 0f),
                Cardinal(0f, -18f),
                Diagonal(10f, 10f),
                Diagonal(-10f, 10f),
                Diagonal(-10f, -10f),
                Diagonal(10f, -10f)),

            Step(18, on: 1, Low, next: 2, delay: 10000),
            Step(17, on: 2, Low, next: 3, delay: 14000),
            Step(16, on: 3, Low, next: 5, delay: 10000),
            Step(15, on: 5, Low, next: 0, delay: 14000)),

        OnLeaveAttack = Of(
            Branch(7, "", When.Always, Do.Despawn(Tentacles))),

        OnDie = Of(
            Branch(99, "", When.Always, Do.Despawn(Tentacles))),
    };

    public PrectazAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
