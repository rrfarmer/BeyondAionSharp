using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The two silikor guards standing either side of the Silikor of Memory — the first (280971, melee)
/// and the second (280972, caster). Retail patterns <c>ND2_WhG1</c> and <c>ND2_WhG2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Both were on plain <c>aggressive</c>, and the
/// caster was the highest-scoring pattern on <c>audit_missing_ai.py</c> with two spawns and only eight
/// skill indices — a rare case where most of a pattern is structure rather than casts.
/// <para>
/// <b>They belong to the boss's fight, not to the room.</b> Every fifteen seconds he broadcasts
/// <c>6622</c> carrying whoever he is fighting, and both guards answer by hating that player and going
/// for them. A raid that pulls the boss and leaves the guards standing gets them anyway.
/// </para>
/// <para>
/// <b>Both peel, and the trigger is health.</b> Below thirty percent each opens a timer that switches
/// to the <b>second-most-hated</b> player and keeps doing it — every fifteen seconds for the melee
/// guard, every ten for the caster, who also starts peeling from seventy-five down.
/// </para>
/// <para>
/// <b>And the caster drops something on people.</b> <c>spawn_on_target_by_attacker_indicator</c> puts
/// a holy servant summon (281025) <em>on a random attacker</em> for thirty seconds, and the rate rises
/// as it weakens: a one-in-four roll every fifteen seconds above seventy-six, one-in-two every ten
/// through the middle, three-in-four every ten below thirty. That escalation is the whole reason to
/// kill the caster first and it was not in our server at all.
/// </para>
/// <para>
/// <b>Each also refuses to be duplicated.</b> On waking, the melee guard broadcasts <c>6655</c> and the
/// caster <c>6656</c>, and the only listener for each is the other guard of the same kind, which
/// leaves. That is what keeps the akaimum's re-placement from stacking guards on a spot — see
/// <see cref="SilikorGuardMarkerAI"/> for the other half of that loop.
/// </para>
/// <para>
/// <b>Not translated:</b> eight skill indices and the branches that carry nothing else — the melee
/// guard's timer-1 and timer-3 chains and the caster's cast halves, all of which sit under the same
/// timers as the branches kept here and differ only in casting instead of spawning.
/// </para>
/// </remarks>
[AIName("silikor_guard")]
public class SilikorGuardAI : PatternAi
{
    /// <summary>Retail's message: the boss is pointing at somebody.</summary>
    public const int TakeThisOne = 6622;

    /// <summary>Retail's "a new one of me has arrived": one per guard kind.</summary>
    private const int AnotherMelee = 6655;
    private const int AnotherCaster = 6656;

    private const float Reach = 50f;

    private const int MeleeGuard = 280971;

    /// <summary><c>BIDLF2A_HolyServantSum_MeleeDespawn_50_n</c> and its caster twin.</summary>
    private const int MeleeMarker = 281034;
    private const int CasterMarker = 281035;

    /// <summary><c>IDLF2A_HolyServantSum_CasterSum_50_An</c> — what the caster drops on people.</summary>
    private const int CasterSummon = 281025;

    /// <summary>Retail's <c>SPAWN_ID_1</c> on both the marker and the summon.</summary>
    private const int Mine = 1;

    private const int MarkerLife = 12;
    private const int SummonLife = 30;

    // Retail's battle timer indices, shared shape across the two guards.
    private const int Heartbeat = 0;
    private const int High = 1;
    private const int Low = 2;
    private const int Mid = 3;

    // Retail's ALPHA_1 and ALPHA_2.
    private const int Below30 = 1;
    private const int Below75 = 2;

    private static readonly AiPattern Melee = new AiPattern
    {
        OnWakeUp = Of(
            Branch(7, "", When.Always,
                Do.Broadcast(AnotherMelee, Reach))),

        OnMessage = Of(
            Branch(8, "", [When.Message(AnotherMelee)],
                Do.DespawnSelf()),

            // Retail patterns <c>ND2_WhG1</c> and <c>ND2_WhG2</c>. THIS HATE IS OURS, NOT RETAIL'S:
            // every listener on 6655 and 6656 answers with a single <c>use_skill</c> and no hate action
            // at all. The point here stands in for a skill this port cannot cast, and it is the only
            // way the order has any effect -- but it is an invention, and it should become the skill
            // rather than survive alongside it. See docs/retail-ai-fidelity.md.
            Branch(7, "", [When.Message(TakeThisOne)],
                Do.HateMessageParam(SummonOrder.OnePoint))),

        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(Heartbeat, 5000))),

        OnBattleTimer = Of(
            Branch(6, "below 30 opens the peel", [When.Timer(Heartbeat), When.HpBelow(30), When.FirstTime(Below30)],
                Do.ArmTimer(Heartbeat, 5000),
                Do.ArmTimer(Low, 23000),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

            Branch(4, "and keeps peeling", [When.Timer(Low)],
                Do.ArmTimer(Low, 15000),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

            Branch(1, "", [When.Timer(Heartbeat)],
                Do.ArmTimer(Heartbeat, 5000))),

        OnDie = Of(
            Branch(100, "", When.Always,
                Do.SpawnNear(MeleeMarker, Mine, count: 1, liveSeconds: MarkerLife))),
    };

    private static readonly AiPattern Caster = new AiPattern
    {
        OnWakeUp = Of(
            Branch(7, "", When.Always,
                Do.Broadcast(AnotherCaster, Reach))),

        OnMessage = Of(
            Branch(8, "", [When.Message(AnotherCaster)],
                Do.DespawnSelf()),

            Branch(7, "", [When.Message(TakeThisOne)],
                Do.HateMessageTarget(SummonOrder.OnePoint))),

        OnEnterAttack = Of(
            Branch(10, "", When.Always,
                Do.ArmTimer(Heartbeat, 5000),
                Do.ArmTimer(High, 15000))),

        OnBattleTimer = Of(
            Branch(9, "below 30 opens the fast drop", [When.Timer(Heartbeat), When.HpBelow(30), When.FirstTime(Below30)],
                Do.ArmTimer(Heartbeat, 5000),
                Do.ArmTimer(Low, 18000),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

            Branch(8, "31-75 opens the middle drop", [When.Timer(Heartbeat), When.HpBetween(31, 75), When.FirstTime(Below75)],
                Do.ArmTimer(Heartbeat, 5000),
                Do.ArmTimer(Mid, 20000),
                Do.SwitchTarget(AggroTarget.SECOND_MOST_HATED)),

            Branch(7, "three in four", [When.Chance(75), When.Timer(Low)],
                Do.ArmTimer(Low, 10000),
                Do.SpawnOnAttacker(AggroTarget.RANDOM, CasterSummon, Mine, liveSeconds: SummonLife)),

            Branch(6, "", [When.Timer(Low)],
                Do.ArmTimer(Low, 10000)),

            Branch(5, "one in two", [When.Chance(50), When.Timer(Mid), When.HpBetween(31, 100)],
                Do.ArmTimer(Mid, 10000),
                Do.SpawnOnAttacker(AggroTarget.RANDOM, CasterSummon, Mine, liveSeconds: SummonLife)),

            Branch(4, "", [When.Timer(Mid), When.HpBetween(31, 100)],
                Do.ArmTimer(Mid, 10000)),

            Branch(3, "one in four", [When.Chance(25), When.Timer(High), When.HpBetween(76, 100)],
                Do.ArmTimer(High, 15000),
                Do.SpawnOnAttacker(AggroTarget.RANDOM, CasterSummon, Mine, liveSeconds: SummonLife)),

            Branch(2, "", [When.Timer(High), When.HpBetween(76, 100)],
                Do.ArmTimer(High, 15000)),

            Branch(1, "", [When.Timer(Heartbeat)],
                Do.ArmTimer(Heartbeat, 5000))),

        OnDie = Of(
            Branch(100, "", When.Always,
                Do.SpawnNear(CasterMarker, Mine, count: 1, liveSeconds: MarkerLife))),
    };

    private readonly AiPattern pattern;

    public SilikorGuardAI(Npc owner)
        : base(owner)
    {
        pattern = owner.GetNpcId() == MeleeGuard ? Melee : Caster;
    }

    protected override AiPattern Pattern => pattern;
}
