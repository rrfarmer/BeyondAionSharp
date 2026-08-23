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
    /// <summary>Retail's <c>6952</c>: a web reporting that it caught someone.</summary>
    public const int WebCaught = 6952;

    /// <summary>Retail's delay on both timers armed by that call.</summary>
    private const int WindowArmMillis = 5_000;

    /// <summary>Retail's <c>FLAGVARI_BETA_1</c>: the windows open once.</summary>
    private const int WindowsOpened = 4;

    /// <summary>The throw clock inside a window, against the eighteen seconds outside one.</summary>
    private const int AcceleratedThrowMillis = 8_000;

    /// <summary>Retail's re-arm on the two branches above fifty-one, which throw nothing.</summary>
    private const int QuietThrowMillis = 15_000;

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

            // HE ONLY THROWS BELOW FIFTY-ONE. Above that the clock still runs -- branches 8 and 7 keep
            // it alive at fifteen seconds -- and nothing is thrown. Without those two and this guard he
            // threw at every health, which also made the accelerators pointless: shortening a clock that
            // already produces a throw on every tick changes nothing.
            Branch(10, "the throw itself", [When.Timer(1), When.HpBetween(0, 51)],
                Do.ArmTimer(1, 18000),
                WebOn(4, MultiTargetOrder.Random, LaterLife)),

            Branch(8, "above fifty-one the clock runs empty", [When.Timer(1), When.HpBetween(51, 70)],
                Do.ArmTimer(1, QuietThrowMillis)),

            Branch(7, "and above seventy too", [When.Timer(1), When.HpBetween(71, 100)],
                Do.ArmTimer(1, QuietThrowMillis)),

            // The two accelerator windows. Retail arms timers 3 and 4 from on_message and each re-arms
            // his throw clock at eight seconds instead of the eighteen branch 10 gives it -- so inside
            // 30-37 and 45-53 the webs come more than twice as fast, and outside them the pressure
            // drops back. Armed by a web's cry; see WebAI and the on_message branches below.
            Branch(9, "the deep window", [When.Timer(3), When.HpBetween(30, 37)],
                Do.ArmTimer(1, AcceleratedThrowMillis)),

            Branch(8, "the middle window", [When.Timer(4), When.HpBetween(45, 53)],
                Do.ArmTimer(1, AcceleratedThrowMillis)),

            Branch(1, "", [When.Timer(0)],
                Do.ArmTimer(0, IdleHeartbeatMillis))),

        // A web caught somebody and said so. Retail writes this twice, at priorities 25 and 24, arming
        // one timer each; one branch arming both is the same thing on a first-match-wins list.
        OnMessage = Of(
            // ONCE PER FIGHT, not once per cry. Retail guards this with set_flag_var, so the first web
            // to catch somebody opens the accelerator windows and the rest do not re-open them. Without
            // the flag every cry re-armed timers 3 and 4, which is what made the whole "does a cry
            // shorten or starve his clock" thread so hard to read -- the question only arises if the
            // arming repeats, and retail says it does not.
            Branch(25, "a web caught somebody", [When.Message(WebCaught), When.FirstTime(WindowsOpened)],
                Do.ArmTimer(3, WindowArmMillis),
                Do.ArmTimer(4, WindowArmMillis))),
    };

    public KingspinAI(Npc owner)
        : base(owner)
    {
    }

    protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Kingspin's webs. Retail pattern <c>IDTP_Web</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A web is a one-shot trap that reports in.</b>
/// Somebody walks into it, it holds them, it tells Kingspin, and it is gone — and eight seconds after it
/// lands it goes anyway, caught or not.
/// <para>
/// <b>Its cry is what gives Kingspin his accelerators.</b> The webs were spawned here long before
/// anything was known to listen, and this file used to describe 281391 as "reachable by nobody"; the
/// listener turned out to be the boss that spawns them. See <see cref="KingspinAI.WebCaught"/>.
/// </para>
/// <para>
/// <b>Not translated:</b> the snare itself, a <c>use_skill</c> on whoever is seen, and the
/// <c>BTIMERI_INDEX_0</c>/<c>_1</c> pair that drives its idle bookkeeping. The call and both despawns
/// are the parts that carry the mechanic.
/// </para>
/// </remarks>
[AIName("kingspin_web")]
public class KingspinWebAI : PatternAi
{
	/// <summary>Retail's <c>range_as_meter</c> on the cry: wide enough to reach him anywhere in the room.</summary>
	private const float CryReach = 50f;

	/// <summary>Retail's <c>BTIMERI_INDEX_5</c>: a web that catches nobody still goes.</summary>
	private const int Lifetime = 5;

	/// <summary>Retail's <c>BTIMERI_INDEX_0</c>: the two and a half seconds a web takes to arm.</summary>
	private const int Settle = 0;

	/// <summary>Retail's <c>BTIMERI_INDEX_1</c>: the sweep for anybody standing on it.</summary>
	private const int Sweep = 1;

	/// <summary>Retail's <c>BTIMERI_INDEX_2</c>. Armed by the catch and read by nothing — see below.</summary>
	private const int Spent = 2;

	/// <summary>
	/// Retail's <c>FLAGVARI_ALPHA_1</c>, and <b>one flag doing two jobs</b>: set on waking, which is
	/// what keeps the sight rung quiet while the web settles; unset by the settle timer, which is what
	/// arms the web; set again by the catch, so it catches only once.
	/// </summary>
	private const int Armed = 1;

	/// <summary><c>SKILLI_INDEX_0</c> for 281391: <c>BNWI_Root_Spider</c>, which is what a web is for.</summary>
	private const int Root = 18607;

	/// <summary>Retail's <c>is_distance_shorter_than</c> on the sweep: two metres, not the one it spawns in.</summary>
	private const int Underfoot = 2;

	/// <remarks>
	/// <b>Three quarters of this was missing, and the shape of what was missing is why nobody noticed.</b>
	/// The web still appeared, still vanished after eight seconds, and still cried when it happened to
	/// see somebody, so it read as a working trap that was merely quiet.
	/// <para>
	/// What it never did: <b>root anybody</b>. Retail casts <c>SKILLI_INDEX_0</c> on both catch paths
	/// and this port cast nothing at all, so Kingspin's webs have been decorative.
	/// </para>
	/// <para>
	/// And it could barely catch. A web is spawned within <b>one</b> metre of a player
	/// (<c>spawn_range=1</c>) and its own sight is <b>one</b> metre (<c>srange="1"</c>), so whether it
	/// ever sees them is a coin flip — which is why an opening throw of three webs produced one cry,
	/// and why a pin counting cries flaked for months. Retail does not rely on sight at all: it arms a
	/// sweep a second and a half after the web settles and asks whether anybody is within <b>two</b>
	/// metres. The sight rung is the fast path; the sweep is the mechanic.
	/// </para>
	/// <para>
	/// <b>Neither clock could run before this.</b> A marker npc never enters combat, and
	/// <c>PatternAi</c> both cancelled its timers on settling and refused to fire the survivors outside
	/// <c>AIState.FIGHT</c>. Both are fixed; this is the encounter that found them.
	/// </para>
	/// <para>
	/// <c>Spent</c> is armed by the catch and read by nothing, in retail as here. It is kept because
	/// the branch is retail's, and dropping an action from a branch is how a mechanic quietly changes.
	/// </para>
	/// </remarks>
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnWakeUp = Of(Branch(15, "settle, and start both clocks", [When.FirstTime(Armed)],
			Do.ArmTimer(Settle, 2_500),
			Do.ArmTimer(Lifetime, 8_000))),

		// Quiet until the settle timer clears the flag, then one catch only. Both halves are that one
		// flag, which is why this guard reads as a first-time test rather than a plain one.
		OnSeeUser = Of(Branch(10, "somebody walked into it", [When.FirstTime(Armed)],
			Do.SkillOnSeenNow(Root),
			Do.Broadcast(KingspinAI.WebCaught, CryReach),
			Do.DespawnSelf())),

		OnBattleTimer = Of(
			Branch(8, "nobody came", [When.Timer(Lifetime)],
				Do.DespawnSelf()),

			Branch(7, "armed now", [When.Timer(Settle), When.Consuming(Armed)],
				Do.ArmTimer(Sweep, 1_500)),

			Branch(6, "somebody is standing on it", [When.Timer(Sweep), When.TargetWithin(Underfoot)],
				Do.ArmTimer(Spent, 1_000),
				Do.SkillOnTargetNow(Root),
				Do.Broadcast(KingspinAI.WebCaught, CryReach),
				Do.DespawnSelf())),
	};

	public KingspinWebAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
