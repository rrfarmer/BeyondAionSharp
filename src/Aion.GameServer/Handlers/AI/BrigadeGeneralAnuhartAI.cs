using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Brigade General Anuhart (214904), Dark Poeta's last boss. Retail pattern <c>XDrakan_LastBoss</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A HERO on plain <c>aggressive</c> — the final boss
/// of an instance whose other five grades this project translated some time ago.
/// <list type="table">
/// <item><term>on engaging</term><description>he takes a <b>random</b> attacker, which is what he does
/// at every step of the fight</description></item>
/// <item><term>crossing seventy</term><description>four <b>faithful subordinates</b> (281249) at four
/// fixed marks around the room, an order to take whoever he is fighting, and another random
/// turn</description></item>
/// <item><term>from then on</term><description>a relay re-issues that order roughly every twenty-seven
/// seconds</description></item>
/// <item><term>below thirty</term><description>four <b>flame centres</b> (281246) at his feet, a random
/// turn, and the ladder stops</description></item>
/// <item><term>and then the enrage</term><description>every thirty-four seconds: four more flame
/// centres, <b>two more subordinates</b>, and the order again</description></item>
/// </list>
/// <para>
/// <b>The four opening subordinates are placed absolutely, and that is what makes them portable.</b>
/// Retail names four coordinates around his platform rather than a walker route, so unlike the Akairun's
/// protectors an entry ago these can be put exactly where they belong. The enrage's extra pair are at
/// his own feet.
/// </para>
/// <para>
/// <b>Two of retail's branches are unreachable in retail.</b> The relay that drives the enrage exists
/// twice over: once unguarded at priorities 10 and 11, and once guarded on 31–100 at priorities 8 and
/// 9. First-match-wins means the unguarded pair always wins, so the guarded copies never run at all —
/// and the practical effect is that the enrage relay is <em>not</em> bounded by health, only by the
/// rung that starts it. Recorded rather than ported, so nobody restores them as a missing band.
/// </para>
/// <para>
/// <b>Not translated.</b> Nineteen skill indices, and the 71–100 timer 1 and 2 chain, which is a cast
/// loop and nothing else.
/// </para>
/// </remarks>
[AIName("brigade_general_anuhart")]
public class BrigadeGeneralAnuhartAI : PatternAi
{
    /// <summary><c>BIDLF1_BXDrakan_LastBossSu_50_An</c> — a faithful subordinate.</summary>
    private const int Subordinate = 281249;

    /// <summary><c>BIDLF1_Dragon_G4NFrRain_A_50_An</c> — a flame centre, which fires and goes.</summary>
    private const int FlameCentre = 281246;

    /// <summary>Retail's <c>SPAWN_ID_1</c> for the subordinates and <c>SPAWN_ID_2</c> for the flames.</summary>
    private const int Wave = 1;
    private const int Rain = 2;

    private const float RainRing = 4f;
    private const float PairRing = 3f;
    private const int RainLife = 10;

    /// <summary>Retail's <c>range_as_meter</c>: fifty at the band step, twenty in the enrage.</summary>
    private const float StepReach = 50f;
    private const float EnrageReach = 20f;

    private const int Ladder = 0;
    private const int Opening = 1;
    private const int OrderOut = 3;
    private const int OrderBack = 4;
    private const int EnrageOut = 5;
    private const int EnrageBack = 6;

    // Retail's ALPHA_1 and ALPHA_3.
    private const int Below70 = 1;
    private const int Below30 = 2;

    /// <summary>The four marks around his platform, from retail's own absolute spawns.</summary>
    private static readonly SpawnSpot[] Marks =
    {
        new SpawnSpot(276.078f, 330.308f, 130.878f),
        new SpawnSpot(266.974f, 323.075f, 129.818f),
        new SpawnSpot(274.366f, 314.857f, 130.484f),
        new SpawnSpot(282.226f, 320.699f, 131.450f),
    };

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(10, "", When.Always,
                Do.ArmTimer(Ladder, 6000),
                Do.ArmTimer(Opening, 11000),
                Do.SwitchTarget(AggroTarget.RANDOM))),

        OnBattleTimer = Of(
            Branch(11, "the enrage wave", [When.Timer(EnrageBack)],
                Do.ArmTimer(EnrageOut, 18000),
                Do.SpawnNear(FlameCentre, Rain, count: 4, range: RainRing, liveSeconds: RainLife),
                Do.SpawnNear(Subordinate, Wave, count: 2, range: PairRing),
                Do.Broadcast(AnuhartSubordinateAI.TakeThisOne, EnrageReach, aboutTarget: true)),

            Branch(10, "", [When.Timer(EnrageOut)],
                Do.ArmTimer(EnrageBack, 16000)),

            // Does not re-arm the ladder: below thirty there are no more band steps, only the enrage.
            Branch(7, "below 30 opens the enrage", [When.Timer(Ladder), When.HpBelow(30),
                    When.FirstTime(Below30)],
                Do.ArmTimer(EnrageOut, 22000),
                Do.SpawnNear(FlameCentre, Rain, count: 4, range: RainRing, liveSeconds: RainLife),
                Do.SwitchTarget(AggroTarget.RANDOM)),

            Branch(6, "and re-issues the order", [When.Timer(OrderBack), When.HpBetween(31, 100)],
                Do.ArmTimer(OrderOut, 15000),
                Do.Broadcast(AnuhartSubordinateAI.GoForThisOne, StepReach, aboutTarget: true)),

            Branch(5, "", [When.Timer(OrderOut), When.HpBetween(31, 100)],
                Do.ArmTimer(OrderBack, 12000)),

            Branch(4, "31-70 calls the four", [When.Timer(Ladder), When.HpBetween(31, 70),
                    When.FirstTime(Below70)],
                Do.ArmTimer(Ladder, 10000),
                Do.ArmTimer(OrderOut, 18000),
                Do.SpawnAt(Subordinate, Wave, 0, Marks),
                Do.Broadcast(AnuhartSubordinateAI.TakeThisOne, StepReach, aboutTarget: true),
                Do.SwitchTarget(AggroTarget.RANDOM)),

            Branch(1, "", [When.Timer(Ladder)],
                Do.ArmTimer(Ladder, 6000))),

        OnLeaveAttack = Of(
            Branch(12, "", When.Always,
                Do.Despawn(Wave))),

        OnDie = Of(
            Branch(13, "", When.Always,
                Do.Despawn(Wave))),
    };

    public BrigadeGeneralAnuhartAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Anuhart's faithful subordinates (281249). Retail pattern <c>LastBoss_Su</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Its whole pattern is four branches that mean one
/// thing: <b>go for whoever the general named.</b> Two messages — <c>6833</c> at the band step and in
/// the enrage, <c>6834</c> on the relay between them — and each is written twice, split on whether the
/// subordinate is already fighting: idle it adds hate and attacks, fighting it switches target and
/// casts. All four arrive at the same place, and <see cref="Do.HateMessageTarget"/> is what we have to
/// say it with.
/// <para>
/// That is the second pattern in this log written as a four-way split on npc state — the anuhart
/// casters' pet was the first — and it collapses the same way for the same reason: our runtime has no
/// vocabulary for testing an NPC's own state inside a branch, and the outcome does not depend on it.
/// </para>
/// <para>
/// <b>Not translated:</b> the casts on all four branches, and <c>on_leave_attack_state</c>'s
/// <c>despawn_self</c> — the general already clears the group on both his own exits, so a subordinate
/// removing itself as well would only race him.
/// </para>
/// </remarks>
[AIName("anuhart_subordinate")]
public class AnuhartSubordinateAI : PatternAi
{
    /// <summary>Retail's two orders: the step's and the relay's.</summary>
    public const int TakeThisOne = 6833;
    public const int GoForThisOne = 6834;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            Branch(4, "", [When.Message(TakeThisOne)],
                Do.HateMessageTarget(SummonOrder.OnePoint)),

            Branch(3, "", [When.Message(GoForThisOne)],
                Do.HateMessageTarget(SummonOrder.OnePoint))),
    };

    public AnuhartSubordinateAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
