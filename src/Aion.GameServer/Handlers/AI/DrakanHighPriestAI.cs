using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The drakan high priests — Elder Malekor (236449) and Head Priest Nashuma (236494). Retail pattern
/// <c>XDrakan_HighPriest</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Both on plain <c>aggressive</c>.
/// <para>
/// <b>He does not have a summon ladder, he has three of them, and they stack.</b> One relay starts
/// with the fight; crossing fifty opens a second; dropping below twenty-five opens a third. None of
/// them ever stops — the band rungs are once-only, but each relay is a pair of timers that re-arm each
/// other for the rest of the fight, and the relay branches carry no health guard at all.
/// </para>
/// <list type="table">
/// <item><term>from twenty seconds</term><description><b>two</b> lesser summons every forty
/// seconds</description></item>
/// <item><term>crossing fifty</term><description>one greater summon, then <b>three</b> lesser every
/// thirty seconds <em>on top</em></description></item>
/// <item><term>below twenty-five</term><description>one greater again, and a third relay of
/// <b>three</b> every thirty</description></item>
/// </list>
/// <para>
/// So a fight held in the last quarter is paying eight lesser summons a minute from three independent
/// clocks. Each lives thirty seconds, which is what keeps that from being unbounded — the pressure is
/// the arrival rate, not the count.
/// </para>
/// <para>
/// <b>Each relay is two timers rather than one, and both halves are carried.</b> Retail writes them as
/// a hand-off — the first arms the second, the second arms the first and spawns — so the interval is
/// the sum of the two delays, not either of them. Collapsing a relay to a single self-arming timer
/// would double its rate, which is the trap recorded against the Unstable Triroan from the other
/// direction.
/// </para>
/// <para>
/// <b>Not translated.</b> Sixteen skill indices and the branches that carry nothing else, including
/// timer 20's twenty-second cast loop and the four <c>unset_flag_var</c> rungs, which each let one
/// relay tick do a different cast the first time after its band opens. The <c>6311</c> broadcast on
/// timer 29 and the <c>on_message</c> handler that arms it: our message audit has no sender for
/// whatever message that is, so the whole chain is unreachable from either end.
/// </para>
/// </remarks>
[AIName("drakan_high_priest")]
public class DrakanHighPriestAI : PatternAi
{
    /// <summary><c>BLDF4_DrakanHighPriestSumA_55_Ah</c> — the greater summon, one per band step.</summary>
    private const int Greater = 281824;

    /// <summary><c>BLDF4_DrakanHighPriestSumB_55_Ah</c> — the lesser, which the relays pour out.</summary>
    private const int Lesser = 281825;

    /// <summary>Retail's <c>SPAWN_ID_1</c>, its <c>spawn_range</c> and its <c>live_time</c>.</summary>
    private const int Wave = 1;
    private const float Ring = 5f;
    private const int Life = 30;

    private const int Ladder = 0;

    // Three relays, each a pair of slots that hand off to one another. Retail's own indices.
    private const int BaseOut = 1;
    private const int BaseBack = 2;
    private const int DeepOut = 3;
    private const int DeepBack = 4;
    private const int MiddleOut = 5;
    private const int MiddleBack = 6;

    // Retail's ALPHA_1 and BETA_1; the other four flags gate cast-only rungs.
    private const int Below25 = 1;
    private const int Below50 = 2;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        // Retail also arms timer 20, a twenty-second cast loop.
        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(Ladder, 5000),
                Do.ArmTimer(BaseOut, 20000))),

        OnBattleTimer = Of(
            Branch(16, "below 25 opens the third relay", [When.Timer(Ladder), When.HpBelow(25),
                    When.FirstTime(Below25)],
                Do.ArmTimer(Ladder, 5000),
                Do.ArmTimer(DeepOut, 15000),
                Do.SpawnNear(Greater, Wave, count: 1, range: Ring, liveSeconds: Life)),

            Branch(14, "", [When.Timer(DeepOut)],
                Do.ArmTimer(DeepBack, 15000)),

            Branch(13, "", [When.Timer(DeepBack)],
                Do.ArmTimer(DeepOut, 15000),
                Do.SpawnNear(Lesser, Wave, count: 3, range: Ring, liveSeconds: Life)),

            Branch(10, "26-50 opens the second", [When.Timer(Ladder), When.HpBetween(26, 50),
                    When.FirstTime(Below50)],
                Do.ArmTimer(Ladder, 5000),
                Do.ArmTimer(MiddleOut, 15000),
                Do.SpawnNear(Greater, Wave, count: 1, range: Ring, liveSeconds: Life)),

            Branch(8, "", [When.Timer(MiddleOut)],
                Do.ArmTimer(MiddleBack, 15000)),

            Branch(7, "", [When.Timer(MiddleBack)],
                Do.ArmTimer(MiddleOut, 15000),
                Do.SpawnNear(Lesser, Wave, count: 3, range: Ring, liveSeconds: Life)),

            Branch(4, "", [When.Timer(BaseOut)],
                Do.ArmTimer(BaseBack, 20000)),

            Branch(2, "", [When.Timer(BaseBack)],
                Do.ArmTimer(BaseOut, 20000),
                Do.SpawnNear(Lesser, Wave, count: 2, range: Ring, liveSeconds: Life)),

            Branch(1, "", [When.Timer(Ladder)],
                Do.ArmTimer(Ladder, 5000))),

        OnLeaveAttack = Of(
            Branch(8, "", When.Always,
                Do.Despawn(Wave))),

        OnDie = Of(
            Branch(9, "", When.Always,
                Do.Despawn(Wave))),
    };

    public DrakanHighPriestAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
