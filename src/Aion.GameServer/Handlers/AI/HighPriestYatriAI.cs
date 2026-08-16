using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// High priest yatri (212308 and 280768). Retail pattern <c>Naga_PhA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. He was on plain <c>aggressive</c> with no class at
/// all, and he is the sender <see cref="ExedilGhostAI"/> shipped without: its naga half listens for
/// <c>3319</c> and until now nothing in the world sent it.
/// <para>
/// <b>The same architecture as <see cref="ExedilAI"/>, and none of the same numbers.</b> A six-second
/// heartbeat, banded rungs, a hand-over between the first two, and a deep rung that stops the clock —
/// but every delay, range and placement differs, and one difference changes the fight:
/// </para>
/// <list type="table">
/// <item><term>81–100</term><description>shouts and re-arms at <b>ten</b> seconds rather than the
/// fallback's six</description></item>
/// <item><term>56–80</term><description>two <c>NagaPriestSum</c> (280769) <b>on whoever he is
/// fighting</b>, five metres out, twenty minutes</description></item>
/// <item><term>26–55</term><description>the first pair is taken away and two more land on his target
/// again — same NPC, new group</description></item>
/// <item><term>below 25</term><description>two <c>NagaPriestSum2</c> (280819) around <em>himself</em>,
/// eight metres out, twenty minutes — and <b>3319</b>, which turns anything left of the first waves
/// into the same thing</description></item>
/// </list>
/// <para>
/// <b>His waves land on the raid, not on him.</b> Exedil scatters his ghosts around his own feet;
/// yatri's first two waves are <c>spawn_on_target</c>, so they appear on whoever he is fighting. Only
/// the deep rung comes home to him. Two bosses built from one template, and the placement is what
/// separates them.
/// </para>
/// <para>
/// <b>And his deep pair is not permanent.</b> Exedil's carries no <c>live_time</c> at all; yatri's
/// carries the same twenty minutes as everything else he calls. The rung still ends the chain — it
/// arms timer 6 rather than timer 0 — so a raid that takes him under twenty-five early still gets one
/// wave and no more, but here the wave expires.
/// </para>
/// <para>
/// <b>Not translated.</b> Seven skill indices across timers 1–7 and the rungs that arm them; the
/// <c>valid_distance</c> of fifty on both <c>spawn_on_target</c> waves, which retail uses to skip the
/// spawn when the target is further off; and four broadcasts. Of those, <b>3316</b> and <b>3318</b>
/// reach only cast branches, <b>3301</b> and <b>3302</b> reach nothing we have, and <b>3320</b> is the
/// re-aim recorded on <see cref="ExedilAI"/> — our <c>servant</c> class captures a summon's target at
/// spawn, so there is nothing for it to act on.
/// </para>
/// </remarks>
[AIName("high_priest_yatri")]
public class HighPriestYatriAI : PatternAi
{
    /// <summary><c>BLF3_NM_NagaPriestSum_47_Ah</c> — the wave that lands on the raid.</summary>
    private const int PowerOfYatri = 280769;

    /// <summary><c>BLF3_NM_NagaPriestSum2_47_Ah</c> — what the deep rung calls, and what the first
    /// waves become when they hear <see cref="ExedilAI.TrueForm"/>.</summary>
    private const int TruePowerOfYatri = 280819;

    // Retail's SPAWN_ID_1..3.
    private const int FirstWave = 1;
    private const int SecondWave = 2;
    private const int Deep = 3;

    /// <summary>Twenty minutes, and unlike Exedil it is on every wave including the deepest.</summary>
    private const int WaveLife = 1200;

    private const float OnThem = 5f;
    private const float AroundHim = 8f;

    private const int HeartbeatMillis = 6000;

    // Retail's ALPHA_1..4.
    private const int Opening = 1;
    private const int Step1 = 2;
    private const int Step2 = 3;
    private const int Below25 = 4;

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(13, "", When.Always,
                Do.ArmTimer(0, 8000))),

        OnBattleTimer = Of(
            // Arms timer 6 rather than timer 0, so the summoning chain ends here.
            Branch(10, "below 25", [When.Timer(0), When.HpBelow(25), When.FirstTime(Below25)],
                Do.Broadcast(ExedilAI.TrueForm, Reach),
                Do.SpawnNear(TruePowerOfYatri, Deep, count: 2, range: AroundHim, liveSeconds: WaveLife)),

            Branch(7, "26-55", [When.Timer(0), When.HpBetween(26, 55), When.FirstTime(Step2)],
                Do.ArmTimer(0, 7000),
                Do.Despawn(FirstWave),
                Do.SpawnOnTarget(PowerOfYatri, SecondWave, count: 2, range: OnThem, liveSeconds: WaveLife)),

            Branch(4, "56-80", [When.Timer(0), When.HpBetween(56, 80), When.FirstTime(Step1)],
                Do.ArmTimer(0, 7000),
                Do.SpawnOnTarget(PowerOfYatri, FirstWave, count: 2, range: OnThem, liveSeconds: WaveLife)),

            // Kept although its casts are not: ten seconds against the fallback's six.
            Branch(2, "81-100", [When.Timer(0), When.HpBetween(81, 100), When.FirstTime(Opening)],
                Do.ArmTimer(0, 10000)),

            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, HeartbeatMillis))),

        OnDie = Of(
            Branch(14, "", When.Always,
                Do.Despawn(FirstWave), Do.Despawn(SecondWave), Do.Despawn(Deep))),

        OnLeaveAttack = Of(
            Branch(15, "", When.Always,
                Do.Despawn(FirstWave), Do.Despawn(SecondWave), Do.Despawn(Deep))),
    };

    /// <summary>Retail's <c>range_as_meter</c> on the 3319 broadcast, the same fifty Exedil uses.</summary>
    private const float Reach = 50f;

    public HighPriestYatriAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
