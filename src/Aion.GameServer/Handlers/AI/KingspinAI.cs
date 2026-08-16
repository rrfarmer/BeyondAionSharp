using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Kingspin (215792), the spider of Lower Udas Temple. Retail pattern <c>IDTP_OctaNm</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Found by <c>tools/client-extract/audit_missing_ai.py</c>:
/// an ELITE boss on plain <c>aggressive</c> with no AI class, and the one NPC his fight is made of —
/// the <b>web</b> (281391) — reachable by nobody.
/// <para>
/// <b>Opening.</b> A web on each of up to three players within fifty metres, thirty seconds each, and
/// four more thrown behind him at fixed offsets — (-15, 0), (-15, -5), (-5, -15) and (0, -15), two
/// metres up — which last six seconds. Those four are the only thing in the pattern placed relative to
/// the boss rather than on somebody.
/// </para>
/// <para>
/// <b>Then a health ladder that repeats.</b> This is the first pattern translated here whose HP
/// branches carry <b>no flag var</b>: they are regimes, not steps. While he is below the threshold the
/// branch fires <em>every eight seconds</em>, for as long as the fight lasts.
/// </para>
/// <list type="table">
/// <item><term>below 86</term><description>casts only</description></item>
/// <item><term>below 71</term><description>a web on each of the <b>four most-hated</b></description></item>
/// <item><term>below 51</term><description>a web on each of the <b>five least-hated</b></description></item>
/// <item><term>below 36</term><description>casts only</description></item>
/// </list>
/// <para>
/// <b>The ordering flips, and it is the mechanic.</b> At 71 he webs the people at the top of his hate
/// list — the tanks. At 51 he webs the bottom of it, which is the healers and the ranged. Retail spells
/// this with <c>ORDERI_DESCENDING</c> and then <c>ORDERI_ASCENDING</c>, and getting it backwards would
/// invert who the fight is hard on.
/// </para>
/// <para>
/// A second web timer runs alongside from twelve seconds: four more on random targets, every eighteen.
/// </para>
/// <para>
/// <b>Not translated.</b> Five skill indices, and with them timer 2 (cast-only, armed by the 51 rung),
/// the two <c>on_message</c> branches that answer 6957 and 6958 by re-arming timer 1, and the cast-only
/// halves of the timer-1 chain. The webs and the timings are index-free.
/// </para>
/// </remarks>
[AIName("kingspin")]
public class KingspinAI : PatternAi
{
    /// <summary><c>BIDTP_Web_55_Ae</c>.</summary>
    private const int Web = 281391;

    /// <summary>Retail's <c>SPAWN_ID_1</c> — every web he throws is in the one group.</summary>
    private const int Webs = 1;

    /// <summary>Retail's <c>valid_distance</c> on every multi-target throw.</summary>
    private const float Reach = 50f;

    private const float OnThem = 1f;

    /// <summary>The opening web on a player lasts half a minute; every later one lasts eight seconds.</summary>
    private const int OpeningLife = 30;
    private const int LaterLife = 8;

    /// <summary>The four he throws behind himself last six, and are two metres up.</summary>
    private const int BehindLife = 6;
    private const float BehindHeight = 2f;

    private const int HeartbeatMillis = 8000;

    /// <summary>Above the first threshold the heartbeat runs a second at a time.</summary>
    private const int IdleHeartbeatMillis = 1000;

    private static PatternAction WebOn(int cap, MultiTargetOrder order, int liveSeconds) =>
        Do.SpawnOnEachTarget(Web, Webs, Reach, maxTargets: cap, order, range: OnThem,
            liveSeconds: liveSeconds);

    private static readonly AiPattern Pattern_ = new AiPattern
    {
        OnEnterAttack = Of(
            Branch(20, "", When.Always,
                Do.ArmTimer(0, HeartbeatMillis),
                Do.ArmTimer(1, 12000),
                WebOn(3, MultiTargetOrder.Random, OpeningLife),
                Do.SpawnOffset(Web, Webs, -15f, 0f, BehindLife, BehindHeight),
                Do.SpawnOffset(Web, Webs, -15f, -5f, BehindLife, BehindHeight),
                Do.SpawnOffset(Web, Webs, -5f, -15f, BehindLife, BehindHeight),
                Do.SpawnOffset(Web, Webs, 0f, -15f, BehindLife, BehindHeight))),

        OnBattleTimer = Of(
            // No flag vars anywhere on this ladder: each of these is a regime that fires on every
            // heartbeat it matches, not a step that fires once.
            Branch(14, "below 36", [When.Timer(0), When.HpBelow(36)],
                Do.ArmTimer(0, HeartbeatMillis)),

            Branch(13, "below 51", [When.Timer(0), When.HpBelow(51)],
                Do.ArmTimer(0, HeartbeatMillis),
                Do.ArmTimer(2, 8000),
                WebOn(5, MultiTargetOrder.Ascending, LaterLife)),

            Branch(12, "below 71", [When.Timer(0), When.HpBelow(71)],
                Do.ArmTimer(0, HeartbeatMillis),
                WebOn(4, MultiTargetOrder.Descending, LaterLife)),

            Branch(11, "below 86", [When.Timer(0), When.HpBelow(86)],
                Do.ArmTimer(0, HeartbeatMillis)),

            Branch(10, "", [When.Timer(1)],
                Do.ArmTimer(1, 18000),
                WebOn(4, MultiTargetOrder.Random, LaterLife)),

            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, IdleHeartbeatMillis))),
    };

    public KingspinAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}
