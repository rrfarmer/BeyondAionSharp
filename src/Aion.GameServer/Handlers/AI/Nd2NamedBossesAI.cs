using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Exedil (212317). Retail pattern <c>ND2_PhA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One of three <c>ND2_*</c> named bosses found by
/// <c>tools/client-extract/audit_missing_ai.py</c> with no AI class at all and adds nobody could
/// reach — the same family as <see cref="FrostmaneLestinAI"/>.
/// <para>
/// <b>His summons are a health ladder walked on a six-second clock, one rung per band.</b> An earlier
/// translation of this class read the three spawning branches as an unguarded sequence ordered by
/// priority alone; they carry <c>is_hp_in_boundary</c> guards, and dropping them inverted the fight.
/// See the fidelity log for what that cost.
/// </para>
/// <list type="table">
/// <item><term>81–100</term><description>casts only, and the clock keeps
/// running</description></item>
/// <item><term>56–80</term><description>two <c>PrSum1</c> (280774) six metres out, twenty
/// minutes</description></item>
/// <item><term>26–55</term><description><b>the first pair is removed</b> and two <c>PrSum2</c>
/// (280775) take their place, seven metres out, twenty minutes</description></item>
/// <item><term>below 25</term><description>two more <c>PrSum2</c>, six metres out, and <b>no lifetime
/// at all</b></description></item>
/// </list>
/// <para>
/// <b>The hand-over is the mechanic.</b> The 26–55 rung despawns <c>SPAWN_ID_1</c> before it spawns, so
/// a raid never faces both twenty-minute pairs at once — the first pair is taken away as the second
/// arrives. Only the deep rung adds to what is already standing.
/// </para>
/// <para>
/// <b>The deep rung ends the chain, and that is retail's own doing.</b> It is the only spawning rung
/// that does not re-arm timer 0. So a boss taken below twenty-five before the ladder has been walked
/// calls those two permanent ghosts and then <em>never summons again</em> — the twenty-minute pairs are
/// simply skipped. Reproduced rather than tidied.
/// </para>
/// <para>
/// <b>Retail's bands leave a gap at exactly twenty-five</b>, where no rung matches and only the
/// six-second fallback runs. Preserved rather than closed: the fallback is what carries the fight
/// across it, and widening a band to tidy the seam would move a threshold.
/// </para>
/// <para>
/// <b>And the deep rung does one more thing: it broadcasts 3319 to fifty metres.</b> Every first-wave
/// ghost that hears it sheds its form and becomes a second-wave one — see <see cref="ExedilGhostAI"/>.
/// Usually there are none, because the 26–55 rung took the first pair away. It matters exactly when a
/// raid <em>skipped</em> that band: the pair that survived because a rung was jumped over gets
/// upgraded instead of removed, so burning him down fast trades two twenty-minute ghosts for two
/// permanent ones rather than for nothing.
/// </para>
/// <para>
/// <b>Not translated.</b> Seven skill indices across timers 1, 2, 4, 5 and 6, and the branches that
/// arm those timers — every one of them is cast-only, so arming them would start clocks whose branches
/// do nothing. Also dropped: retail's 81–100 rung, whose only effect we can reproduce is re-arming
/// timer 0 at six seconds, which is exactly what the fallback below it already does. Kept where the
/// same test would keep it — a branch earns its place by changing what happens, and that one does not.
/// </para>
/// <para>
/// <b>Also not translated: message 3320</b>, which his timer-6 branch sends every twenty seconds once
/// the deep rung has armed it. Retail's second-wave ghosts answer it by taking hate on whoever he is
/// fighting and turning on them. Our <c>servant</c> class binds a summon's target when it spawns and
/// drives its casts from that captured target, so a re-aim has nothing to act on — the hate would
/// move and the ghost would keep casting at whoever it first saw. Sending it would look wired and
/// would not be.
/// </para>
/// </remarks>
[AIName("exedil")]
public class ExedilAI : PatternAi
{
    private const int GhostPriestOne = 280774;
    private const int GhostPriestTwo = 280775;

    // Retail's SPAWN_ID_1..3, one per rung.
    private const int First = 1;
    private const int Second = 2;
    private const int Deep = 3;

    /// <summary>Twenty minutes, which is retail's <c>live_time</c> on the two banded pairs.</summary>
    private const int GhostLife = 1200;

    // Retail's ALPHA_2..4.
    private const int Step1 = 2;
    private const int Step2 = 3;
    private const int Below25 = 4;

    private const int HeartbeatMillis = 6000;

    /// <summary>
    /// Retail's 3319: every first-wave ghost still standing sheds its old form. See
    /// <see cref="ExedilGhostAI"/>.
    /// </summary>
    public const int TrueForm = 3319;

    /// <summary>Retail's <c>range_as_meter</c> on that broadcast.</summary>
    private const float Reach = 50f;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(13, "", When.Always,
                Do.ArmTimer(0, 10000))),

        OnBattleTimer = Of(
            // Highest priority, and the only spawning rung that does not re-arm the heartbeat.
            Branch(10, "below 25", [When.Timer(0), When.HpBelow(25), When.FirstTime(Below25)],
                Do.Broadcast(TrueForm, Reach),
                Do.SpawnNear(GhostPriestTwo, Deep, count: 2, range: 6f)),

            Branch(7, "26-55", [When.Timer(0), When.HpBetween(26, 55), When.FirstTime(Step2)],
                Do.ArmTimer(0, 8000),
                Do.Despawn(First),
                Do.SpawnNear(GhostPriestTwo, Second, count: 2, range: 7f, liveSeconds: GhostLife)),

            Branch(4, "56-80", [When.Timer(0), When.HpBetween(56, 80), When.FirstTime(Step1)],
                Do.ArmTimer(0, 10000),
                Do.SpawnNear(GhostPriestOne, First, count: 2, range: 6f, liveSeconds: GhostLife)),

            // Retail's lowest rung: no guard but the timer, and it is what keeps the ladder reachable
            // while he is above eighty and again in the gap at exactly twenty-five.
            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, HeartbeatMillis))),
    };

    public ExedilAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Ulan (212315). Retail pattern <c>ND2_WhB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The same shape as <see cref="ExedilAI"/> — a banded
/// ladder on a heartbeat, with a hand-over between the two summoning rungs — and <b>three</b> ghosts a
/// time rather than two, all ten metres out.
/// <list type="table">
/// <item><term>81–100</term><description>casts, and re-arms the heartbeat at <b>seven</b> seconds
/// rather than the fallback's six</description></item>
/// <item><term>61–80</term><description>three <c>WiSum1</c> (280806), <b>ten minutes</b></description></item>
/// <item><term>36–60</term><description>the first three are removed and three <c>WiSum2</c> (280807)
/// replace them, <b>forty minutes</b></description></item>
/// <item><term>below 35</term><description>no summon at all — and the clock stops</description></item>
/// </list>
/// <para>
/// <b>His deep rung summons nothing and ends the ladder.</b> It is above both summoning rungs and does
/// not re-arm timer 0, so a raid that takes him under thirty-five quickly gets <em>fewer</em> adds, not
/// more: whichever pairs have not been called never will be. That is the opposite of the shape a
/// hand-written ladder reaches for, and it is worth stating because it reads as a bug until the
/// pattern is read.
/// </para>
/// <para>
/// The two lifetimes are the fight's only asymmetry, and they run the way a port would not guess: the
/// pair that arrives <b>first</b> is the one that lasts ten minutes, and the replacements last forty.
/// </para>
/// <para>
/// <b>The 81–100 rung is kept</b> although its casts are not, because its re-arm differs from the
/// fallback's — seven seconds against six. Exedil's equivalent rung re-arms at the same six seconds as
/// its fallback and is dropped instead. A branch earns its place by changing what happens.
/// </para>
/// <para>
/// <b>Not translated:</b> seven skill indices across timers 1, 2, 3, 4, 5 and 6, the branches that arm
/// them, and two <c>say_to_all</c> lines. Retail's bands leave a gap at exactly thirty-five, carried by
/// the fallback.
/// </para>
/// </remarks>
[AIName("ulan")]
public class UlanAI : PatternAi
{
    private const int GhostWizardOne = 280806;
    private const int GhostWizardTwo = 280807;

    private const int First = 1;
    private const int Second = 2;

    private const int LongLife = 2400;
    private const int ShortLife = 600;

    private const float OutTen = 10f;

    private const int HeartbeatMillis = 6000;

    // Retail's ALPHA_1..4.
    private const int Opening = 1;
    private const int Step1 = 2;
    private const int Step2 = 3;
    private const int Below35 = 4;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(12, "", When.Always,
                Do.ArmTimer(0, 12000))),

        OnBattleTimer = Of(
            // No spawn, and no re-arm: below thirty-five the summoning is over.
            Branch(10, "below 35", [When.Timer(0), When.HpBelow(35), When.FirstTime(Below35)]),

            Branch(7, "36-60", [When.Timer(0), When.HpBetween(36, 60), When.FirstTime(Step2)],
                Do.ArmTimer(0, 7000),
                Do.Despawn(First),
                Do.SpawnNear(GhostWizardTwo, Second, count: 3, range: OutTen, liveSeconds: LongLife)),

            Branch(4, "61-80", [When.Timer(0), When.HpBetween(61, 80), When.FirstTime(Step1)],
                Do.ArmTimer(0, 8000),
                Do.SpawnNear(GhostWizardOne, First, count: 3, range: OutTen, liveSeconds: ShortLife)),

            Branch(2, "81-100", [When.Timer(0), When.HpBetween(81, 100), When.FirstTime(Opening)],
                Do.ArmTimer(0, 7000)),

            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, HeartbeatMillis))),
    };

    public UlanAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// RM-13b (214800). Retail pattern <c>ND2_AhD</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The plainest of the three: one add, one clock, two
/// banded rungs, and the only thing that changes between them is <b>how many</b>.
/// <list type="bullet">
/// <item>between thirty-one and seventy-five he calls <b>two</b> pretorians;</item>
/// <item>below thirty he calls <b>three</b>.</item>
/// </list>
/// <para>
/// <b>He calls nothing above seventy-five.</b> Both rungs are banded, so the opening of the fight is
/// the five-second fallback and nothing else — an earlier translation read the shallower rung as
/// unguarded and had him summoning on the first heartbeat at full health.
/// </para>
/// <para>
/// Both waves are five metres out and last a minute, which makes them a pressure rather than a wave,
/// and every rung re-arms at five seconds — so the one that has not fired yet is always five seconds
/// away. Both file into the same spawn group and neither despawns it, so the two waves overlap.
/// </para>
/// <para>
/// <b>Not translated:</b> five skill indices on timer 1 and the enter-combat arm that starts it.
/// </para>
/// </remarks>
[AIName("rm13b")]
public class Rm13bAI : PatternAi
{
    /// <summary><c>BIDLF3CL_NM_PretorSumA_45_An</c>.</summary>
    private const int Pretorian = 281278;

    /// <summary>Retail's <c>SPAWN_ID_1</c> — both rungs file into the one group.</summary>
    private const int Called = 1;

    private const int PretorianLife = 60;
    private const float OutFive = 5f;

    private const int HeartbeatMillis = 5000;

    // Retail's ALPHA_1 and ALPHA_2.
    private const int Below30 = 1;
    private const int Between = 2;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(7, "", When.Always,
                Do.ArmTimer(0, HeartbeatMillis))),

        OnBattleTimer = Of(
            Branch(6, "below 30", [When.Timer(0), When.HpBelow(30), When.FirstTime(Below30)],
                Do.ArmTimer(0, HeartbeatMillis),
                Do.SpawnNear(Pretorian, Called, count: 3, range: OutFive, liveSeconds: PretorianLife)),

            Branch(5, "31-75", [When.Timer(0), When.HpBetween(31, 75), When.FirstTime(Between)],
                Do.ArmTimer(0, HeartbeatMillis),
                Do.SpawnNear(Pretorian, Called, count: 2, range: OutFive, liveSeconds: PretorianLife)),

            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, HeartbeatMillis))),
    };

    public Rm13bAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
