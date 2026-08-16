using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Aurelian Dadar (235966, Cygnea) and Tatar's Blaze (220019, Enshar). Retail pattern
/// <c>LDF4b_Golden_Gururu</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Two LEGENDARY world bosses sharing one pattern,
/// both on plain <c>aggressive</c>, and all three things they call up were spawned by nothing
/// anywhere in the server:
/// <list type="table">
/// <item><term>below 85</term><description>tatar's clone (282743), on the eight most-hated</description></item>
/// <item><term>below 60</term><description>paralysis eye (282744), on two at random</description></item>
/// <item><term>90 / 70 / 45 / 25</term><description>lava (282746), once at each threshold, on six at random</description></item>
/// </list>
/// <para>
/// Each of the three runs on its own timer, and each timer carries a repeat branch: the threshold
/// branch re-arms at fifty seconds, the repeat at six, so the boss re-checks every six seconds while
/// out of range and then goes quiet for fifty once it fires. The four lava thresholds are one-shots,
/// each with its own flag.
/// </para>
/// <para>
/// <b>The casts are not translated, and two whole chains are therefore omitted.</b> Fifteen skill
/// indices are addressed and <b>neither boss has an <c>npc_skills</c> entry at all</b> — not a short
/// list, no list — so there is nothing to map them onto. Retail's other two chains do nothing but
/// cast: the main rotation (T0 → T1 → T2 → T3 → T0 at 8s, 8s, 8s, 12s, casting indices 14, 13, 11 and
/// then 7+8+8) and the debuff cycle (T7 → T8 → T9 → T10 → T11 → T7, forty seconds apart, indices 2
/// through 6). Arming them here would schedule a heartbeat forever to do nothing, so they are left
/// out, exactly as Lost Balor's unarmed chains were. The timings are recorded above and in
/// docs/retail-ai-fidelity.md so they can be restored the moment a skill list surfaces.
/// </para>
/// <para>
/// What remains is the whole of what this boss can actually be made to do: the three add mechanics,
/// which are the part that was missing from the server entirely.
/// </para>
/// <para>
/// <b>Not translated:</b> the door. Retail opens door 1 on waking, closes it sixty seconds into the
/// fight — shutting the raid in — and re-opens it on death, reset, or leaving combat. The pattern
/// runtime has no door control, and this is the second boss to want it (Researcher Teselik opens door
/// 210 on death). Also absent: the three shouts, which have no numeric id in our data.
/// </para>
/// </remarks>
[AIName("golden_tatar")]
public class GoldenTatarAI : PatternAi
{
    private const int Clone = 282743;
    private const int ParalysisEye = 282744;
    private const int Lava = 282746;

    /// <summary>Retail's <c>SPAWN_ID_1</c> — everything it calls up, cleared together.</summary>
    private const int Adds = 1;

    // One flag per lava threshold: they fire once each, in descending order, as it loses ground.
    private const int Lava90 = 1;
    private const int Lava70 = 2;
    private const int Lava45 = 3;
    private const int Lava25 = 4;

    /// <summary>Retail checks a threshold every six seconds and rests fifty once it fires.</summary>
    private const int Recheck = 6000;
    private const int AfterFiring = 50000;

    /// <summary>Every one of the three lands within fifty metres of it, on the aggro list.</summary>
    private const float InRange = 50f;

    private static PatternAction LavaBurst() =>
        Do.SpawnOnEachTarget(Lava, Adds, InRange, maxTargets: 6, MultiTargetOrder.Random,
            range: 1f, liveSeconds: 60);

    /// <summary>The four lava steps differ only in their threshold and their one-shot flag.</summary>
    private static PatternBranch LavaStep(int priority, int below, int flag)
        => Branch(priority, $"Magma {below}%",
            [When.HpBelow(below), When.Timer(6), When.FirstTime(flag)],
            LavaBurst(),
            Do.ArmTimer(6, Recheck));

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            // Retail also arms T0 at 6s and T7 at 40s for the two cast-only chains, and T12 at 60s to
            // close the door. None of the three has anything to run here.
            Branch(20, "SetTimer", When.Always,
                Do.ArmTimer(4, 10000),
                Do.ArmTimer(5, 10000),
                Do.ArmTimer(6, 10000))),

        OnBattleTimer = Of(
            // --- the clone, below 85 ----------------------------------------------------------------
            Branch(14, "Search", [When.HpBelow(85), When.Timer(4)],
                Do.SpawnOnEachTarget(Clone, Adds, InRange, maxTargets: 8, MultiTargetOrder.Descending,
                    range: 20f, liveSeconds: 60),
                Do.ArmTimer(4, AfterFiring)),
            Branch(13, "Search_Repeat", [When.Timer(4)], Do.ArmTimer(4, Recheck)),

            // --- the paralysis eye, below 60 --------------------------------------------------------
            Branch(12, "ParalyzeEye", [When.HpBelow(60), When.Timer(5)],
                Do.SpawnOnEachTarget(ParalysisEye, Adds, InRange, maxTargets: 2, MultiTargetOrder.Random,
                    range: 2f, liveSeconds: 30),
                Do.ArmTimer(5, AfterFiring)),
            Branch(11, "ParalyzeEye_Repeat", [When.Timer(5)], Do.ArmTimer(5, Recheck)),

            // --- the lava, four one-shot thresholds -------------------------------------------------
            LavaStep(10, below: 90, flag: Lava90),
            LavaStep(9, below: 70, flag: Lava70),
            LavaStep(8, below: 45, flag: Lava45),
            LavaStep(7, below: 25, flag: Lava25),
            Branch(6, "SummonMagma_Repeat", [When.Timer(6)], Do.ArmTimer(6, Recheck))),

        OnLeaveAttack = Of(
            Branch(20, "DespawnAll", When.Always, Do.Despawn(Adds))),

        OnDie = Of(
            Branch(21, "DespawnAll", When.Always, Do.Despawn(Adds))),
    };

    public GoldenTatarAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
