using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Bulwark Jeshuchi (212282), the cherubim commander of Heiron. Retail pattern <c>ND2_KeD</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A HERO on plain <c>aggressive</c>, found by
/// <c>tools/client-extract/audit_translatable.py</c> — twenty-six translatable actions against ten
/// casts, the best ratio of anything left with a spawn in it.
/// <para>
/// <b>The wave grows with every band.</b> Three <b>disciples of Jeshuchi</b> (280758) on the first
/// clock tick, four on crossing seventy, five on crossing thirty-five — ten metres out, thirty minutes
/// each, and each step also turns him off the tank. The first two steps take the <b>third-most-hated</b>
/// player; the last one takes whoever is <b>closest to dying</b>, which is the escalation that matters
/// more than the extra disciple.
/// </para>
/// <para>
/// <b>He clears up on both exits</b> — retail declares the despawn on <c>on_leave_attack_state</c> and
/// on <c>on_killed_by_user</c>, so a reset and a kill both take the disciples with them.
/// </para>
/// <para>
/// <b>Not translated.</b> Ten skill indices. And both of his broadcasts, for the reason the message
/// audit exists: <c>6191</c> and <c>6192</c> reach only <c>ND2_Ksum2</c>, the disciple's own pattern,
/// whose handlers are a cast and a two-second timer that leads to a cast. Nothing a disciple does with
/// either is anything we can express, so sending them would be noise — recorded as <b>cast-only</b>
/// rather than left looking like a gap.
/// </para>
/// </remarks>
[AIName("bulwark_jeshuchi")]
public class BulwarkJeshuchiAI : PatternAi
{
    /// <summary><c>BLF3_NM_Cherubim3Sum_47_Ah</c> — a disciple of Jeshuchi.</summary>
    private const int Disciple = 280758;

    /// <summary>Retail's <c>SPAWN_ID_1</c>, its <c>spawn_range</c> and its <c>live_time</c>.</summary>
    private const int Wave = 1;
    private const float Ring = 10f;
    private const int WaveLife = 1800;

    private const int Ladder = 0;

    // Retail's ALPHA_1..3.
    private const int FirstStep = 1;
    private const int Below70 = 2;
    private const int Below35 = 3;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(11, "", When.Always,
                Do.ArmTimer(Ladder, 9000))),

        OnBattleTimer = Of(
            Branch(8, "below 35 calls five", [When.Timer(Ladder), When.HpBelow(35), When.FirstTime(Below35)],
                Do.ArmTimer(Ladder, 6000),
                Do.SpawnNear(Disciple, Wave, count: 5, range: Ring, liveSeconds: WaveLife),
                Do.SwitchTarget(AggroTarget.LOWEST_HP)),

            Branch(5, "36-70 calls four", [When.Timer(Ladder), When.HpBetween(36, 70), When.FirstTime(Below70)],
                Do.ArmTimer(Ladder, 6000),
                Do.SpawnNear(Disciple, Wave, count: 4, range: Ring, liveSeconds: WaveLife),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            Branch(2, "71-100 calls three", [When.Timer(Ladder), When.HpBetween(71, 100),
                    When.FirstTime(FirstStep)],
                Do.ArmTimer(Ladder, 7000),
                Do.SpawnNear(Disciple, Wave, count: 3, range: Ring, liveSeconds: WaveLife),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            Branch(1, "", [When.Timer(Ladder)],
                Do.ArmTimer(Ladder, 6000))),

        OnLeaveAttack = Of(
            Branch(13, "", When.Always,
                Do.Despawn(Wave))),

        OnDie = Of(
            Branch(12, "", When.Always,
                Do.Despawn(Wave))),
    };

    public BulwarkJeshuchiAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Watcher Zapiel (212283), the LEGENDARY of the same camp. Retail pattern <c>ND2_KeE</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Also on plain <c>aggressive</c>.
/// <para>
/// <b>He does not summon; he commands.</b> Every band step — at eighty-one, at eighty, at fifty-five —
/// he broadcasts <c>6190</c> fifty metres carrying whoever he is fighting, and every <b>disciple of
/// Zapiel</b> (280760) standing near him drops what it is doing and goes for that player. Then he turns
/// onto the <b>third-most-hated</b> himself, so the tank loses him and the raid's healer gains four
/// cherubim. Below thirty he stops stepping and starts repeating: <c>6189</c>, the same order, roughly
/// every thirty-two seconds for the rest of the fight.
/// </para>
/// <para>
/// <b>His spawns are not translated, and they are the reason to record the walker gap again.</b> All
/// four of his band steps place disciples with <c>SPAWN_LOCATION_WAY_POINT_START</c> and a
/// <c>pathname</c> — <c>E3_Cheru3_1</c> through <c>_4</c> — which means "at the start of that route,
/// then walk it". We have neither the location kind nor the route mapping. What saves the encounter is
/// that our spawn file already stands disciples around him, so the orders land on real cherubim; what
/// is missing is the reinforcement, and it is missing for the same reason as everything else with a
/// <c>pathname</c> on it.
/// </para>
/// <para>
/// <b>The ladder stops itself below thirty</b>, exactly as several others in this log do: that rung
/// does not re-arm the six-second clock, so once the order loop is running there are no more band
/// steps however long the fight lasts.
/// </para>
/// <para>
/// <b>Not translated:</b> fifteen skill indices, the <c>6191</c> broadcasts on the per-band cast loops
/// (cast-only at the listener, like Jeshuchi's), and the spawns above.
/// </para>
/// </remarks>
[AIName("watcher_zapiel")]
public class WatcherZapielAI : PatternAi
{
    /// <summary>Retail's <c>range_as_meter</c> on every order.</summary>
    private const float Reach = 50f;

    private const int Ladder = 0;
    private const int Order = 8;
    private const int OrderBack = 9;

    // Retail's ALPHA_1..4.
    private const int Below100 = 1;
    private const int Below80 = 2;
    private const int Below55 = 3;
    private const int Below30 = 4;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(17, "", When.Always,
                Do.ArmTimer(Ladder, 9000))),

        OnBattleTimer = Of(
            Branch(16, "", [When.Timer(OrderBack), When.HpBelow(30)],
                Do.ArmTimer(Order, 15000)),

            Branch(15, "and keeps ordering", [When.Timer(Order), When.HpBelow(30)],
                Do.ArmTimer(OrderBack, 17000),
                Do.Broadcast(DiscipleOfZapielAI.GoForThisOne, Reach, aboutTarget: true)),

            // Does not re-arm the ladder: below thirty there are no more band steps.
            Branch(14, "below 30 opens the order loop", [When.Timer(Ladder), When.HpBelow(30),
                    When.FirstTime(Below30)],
                Do.ArmTimer(Order, 15000)),

            Branch(6, "31-55", [When.Timer(Ladder), When.HpBetween(31, 55), When.FirstTime(Below55)],
                Do.ArmTimer(Ladder, 7000),
                Do.Broadcast(DiscipleOfZapielAI.TakeThisOne, Reach, aboutTarget: true),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            Branch(4, "56-80", [When.Timer(Ladder), When.HpBetween(56, 80), When.FirstTime(Below80)],
                Do.ArmTimer(Ladder, 7000),
                Do.Broadcast(DiscipleOfZapielAI.TakeThisOne, Reach, aboutTarget: true),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            Branch(2, "81-100", [When.Timer(Ladder), When.HpBetween(81, 100), When.FirstTime(Below100)],
                Do.ArmTimer(Ladder, 7000),
                Do.Broadcast(DiscipleOfZapielAI.TakeThisOne, Reach, aboutTarget: true),
                Do.SwitchTarget(AggroTarget.THIRD_MOST_HATED)),

            Branch(1, "", [When.Timer(Ladder)],
                Do.ArmTimer(Ladder, 6000))),

        OnLeaveAttack = Of(
            Branch(19, "", When.Always,
                Do.Despawn(1))),

        OnDie = Of(
            Branch(18, "", When.Always,
                Do.Despawn(1))),
    };

    public WatcherZapielAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The disciples of Zapiel (280760). Retail pattern <c>ND2_Ksum3</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Two of its three handlers are the summon-order
/// shape and both are translatable: on <c>6190</c> — the band step — and on <c>6189</c> — the
/// repeating call below thirty — it hates the player the message names and goes for them.
/// <para>
/// <b>Retail's two orders differ by one action and ours cannot tell them apart.</b> <c>6190</c> is
/// <c>add_hate_point</c> + <c>attack_most_hating</c> + <c>switch_target</c>; <c>6189</c> is
/// <c>add_hate_point</c> + <c>switch_target</c> with no attack, and a four-second timer that leads to
/// a cast. Our <see cref="Do.HateMessageTarget"/> does hate-then-attack, so the second comes out very
/// slightly stronger than retail wrote it — an aggressive cherubim that has just switched target was
/// going to attack anyway, which is why the widening is accepted rather than worked around.
/// </para>
/// <para>
/// <b>Not translated:</b> the casts on both handlers and on the four-second timer, and the
/// <c>6191</c> handler, which is a cast and nothing else.
/// </para>
/// </remarks>
[AIName("disciple_of_zapiel")]
public class DiscipleOfZapielAI : PatternAi
{
    /// <summary>Retail's band-step order, and the repeating one below thirty.</summary>
    public const int TakeThisOne = 6190;
    public const int GoForThisOne = 6189;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnMessage = Of(
            Branch(3, "", [When.Message(GoForThisOne)],
                Do.HateMessageTarget(SummonOrder.OnePoint)),

            Branch(1, "", [When.Message(TakeThisOne)],
                Do.HateMessageTarget(SummonOrder.OnePoint))),
    };

    public DiscipleOfZapielAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
