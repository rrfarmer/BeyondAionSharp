using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The four anuhart casters of Dark Poeta — the spiritlord (215249), invoker (215258), conjurer
/// (215267) and transporter (215276). Retail pattern <c>XDrakan_EeB_F_50</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. All four on plain <c>aggressive</c>; picked by
/// <c>audit_translatable.py</c> for having the best owner count against blocked actions left on the
/// list — twenty-five translatable actions against eleven casts, over four npcs.
/// <para>
/// <b>Each of them fights with a pet, and keeps telling it what to hit.</b> On engaging it puts a
/// <b>faithful subordinate</b> (281171) down at its own feet and immediately broadcasts <c>3406</c>
/// naming whoever it is fighting; the pet answers by going for that player (see
/// <see cref="AnuhartPetAI"/>). Nine seconds in it does it again and turns on a random attacker
/// itself; crossing seventy it does it again and takes the second-most-hated; and below thirty-five it
/// settles into a loop that re-points the pet roughly every twenty-seven seconds for the rest of the
/// fight.
/// </para>
/// <para>
/// <b>The order is the mechanic, not the pet.</b> One extra monster is a detail; a pet that is moved
/// onto the healer every time the caster changes its mind is the reason these four are dangerous in a
/// group. Retail even varies the shout radius — fifteen metres at the start, thirteen at the middle
/// rung, ten in the last third — which is carried as written and is <b>not observable here</b>: the
/// pet stands at its master's feet and our harness has no movement, so every radius reaches it. It
/// would matter in the live game to a pet that had chased somebody out past ten metres. Left as a
/// deliberate mutation survivor.
/// </para>
/// <para>
/// <b>The ladder stops itself below thirty-five</b>, the shape recorded against several bosses in this
/// log: that rung does not re-arm the six-second clock, so once the order loop is running there are no
/// more band steps.
/// </para>
/// <para>
/// <b>Not translated.</b> Eleven skill indices and the two cast-only timer loops that carry nothing
/// else. The <c>3407</c> broadcast, whose only listener answers with a self-cast — recorded as
/// cast-only; its rung is kept anyway, because the timer it arms is what paces the order loop. The
/// <c>say_to_all</c> lines, with no <c>npc_shouts.xml</c> row. And <c>on_enter_abnormal_state</c>,
/// which broadcasts <c>3403</c> ten metres when the caster is crowd-controlled — an event our runtime
/// does not raise, and the third pattern in this log to want it. That message has <b>twenty-seven</b>
/// listener patterns across the dump, so it is the most-wanted single event we are missing after the
/// friend-attacked pair.
/// </para>
/// </remarks>
[AIName("anuhart_caster")]
public class AnuhartCasterAI : PatternAi
{
    /// <summary><c>BXDrakan_EPet_F_50_an</c> — a faithful subordinate.</summary>
    private const int Pet = 281171;

    /// <summary>Retail's <c>SPAWN_ID_1</c> and its <c>spawn_range</c>. No lifetime: it lives with its master.</summary>
    private const int Kept = 1;
    private const float Ring = 2f;

    // Retail's own radii, which shrink as the fight goes on.
    private const float OpeningReach = 15f;
    private const float MiddleReach = 13f;
    private const float DeepReach = 10f;

    private const int Ladder = 0;
    private const int Opening = 1;
    private const int DeepOrder = 4;
    private const int DeepOrderBack = 5;

    // Retail's ALPHA_1 and ALPHA_2.
    private const int Below70 = 1;
    private const int Below35 = 2;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(12, "", When.Always,
                Do.ArmTimer(Ladder, 6000),
                Do.ArmTimer(Opening, 9000),
                Do.SpawnNear(Pet, Kept, count: 1, range: Ring),
                Do.Broadcast(AnuhartPetAI.GoForThisOne, OpeningReach, aboutTarget: true))),

        OnBattleTimer = Of(
            Branch(10, "and keeps re-pointing it", [When.Timer(DeepOrderBack), When.HpBelow(35)],
                Do.ArmTimer(DeepOrder, 15000),
                Do.Broadcast(AnuhartPetAI.GoForThisOne, DeepReach, aboutTarget: true)),

            // Retail also broadcasts 3407 here, which its listener answers with a cast. The rung is
            // kept for the timer it arms: it is what paces the loop above.
            Branch(9, "", [When.Timer(DeepOrder)],
                Do.ArmTimer(DeepOrderBack, 12000)),

            // Does not re-arm the ladder: below thirty-five there are no more band steps.
            Branch(8, "below 35 opens the loop", [When.Timer(Ladder), When.HpBelow(35),
                    When.FirstTime(Below35)],
                Do.ArmTimer(DeepOrder, 13000),
                Do.Broadcast(AnuhartPetAI.GoForThisOne, DeepReach, aboutTarget: true)),

            Branch(6, "36-70 re-points and peels", [When.Timer(Ladder), When.HpBetween(36, 70),
                    When.FirstTime(Below70)],
                Do.ArmTimer(Ladder, 6000),
                Do.Broadcast(AnuhartPetAI.GoForThisOne, MiddleReach, aboutTarget: true),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

            // Armed once on engaging and never re-armed, so this happens exactly once a fight.
            Branch(4, "nine seconds in", [When.Timer(Opening)],
                Do.Broadcast(AnuhartPetAI.GoForThisOne, OpeningReach, aboutTarget: true),
                Do.SwitchTarget(AggroTarget.RANDOM)),

            Branch(1, "", [When.Timer(Ladder)],
                Do.ArmTimer(Ladder, 6000))),

        OnLeaveAttack = Of(
            Branch(15, "", When.Always,
                Do.Despawn(Kept))),

        OnDie = Of(
            Branch(14, "", When.Always,
                Do.Despawn(Kept))),
    };

    public AnuhartCasterAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The faithful subordinate an anuhart caster fights with (281171). Retail pattern <c>XD_EPet</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Its whole job is to go where it is pointed.
/// <para>
/// <b>Retail writes the order twice and we carry it once.</b> There are two <c>3406</c> branches,
/// split on whether the pet is already fighting: the fighting one casts and switches target, the idle
/// one adds hate and attacks. Both mean "that player now", and
/// <see cref="Do.HateMessageTarget"/> does what either arrives at — so the split is collapsed and
/// recorded rather than reproduced with a state test we would have to invent.
/// </para>
/// <para>
/// <b>Not translated:</b> the casts on both branches, and the <c>3407</c> handler, which is a self-cast
/// and nothing else.
/// </para>
/// </remarks>
[AIName("anuhart_pet")]
public class AnuhartPetAI : PatternAi
{
    /// <summary>Retail's order: whoever my master named.</summary>
    public const int GoForThisOne = 3406;

    /// <summary>Retail's <c>point_to_add</c> and <c>points_to_add</c> on both branches.</summary>
    private const int Commit = 100;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // TWO BRANCHES, one per npc state, which is how retail writes it -- see XD_EPet priorities 2
        // and 1. This port had them folded into one, so a pet already fighting and a pet standing idle
        // answered identically, and both answered with a single point where retail spends a hundred.
        OnMessage = Of(
            // Already in a fight: drop what it is doing and go.
            Branch(2, "already fighting, so switch", [When.Message(GoForThisOne), When.Fighting],
                Do.HateMessageTarget(Commit)),

            // Standing idle: take the hate and pick its most hated, which with an empty list is the
            // one just named. Retail's add_hate_point plus attack_most_hating, not a forced switch.
            Branch(1, "idle, so join", [When.Message(GoForThisOne), When.Idle],
                Do.HateMessageParam(Commit),
                Do.SwitchTarget(Aion.GameServer.Controllers.Attack.AggroTarget.MOST_HATED))),
    };

    public AnuhartPetAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
