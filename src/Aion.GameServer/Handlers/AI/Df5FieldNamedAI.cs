using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Tidalsail Spirit (219929), the mine layer of the <c>DF5</c> named-boss family. Retail pattern
/// <c>DF5_ItemNamed_6_Ra_01_SSH</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A HERO on plain <c>aggressive</c>, and the whole
/// fight is one cycle repeated:
/// <list type="table">
/// <item><term>six seconds in</term><description>the summoning motion</description></item>
/// <item><term>then four times, six seconds apart</term><description><b>two mines each on a randomly
/// chosen attacker</b> — eight in all, each of them live for forty seconds</description></item>
/// <item><term>six seconds after the last pair</term><description><b>they all go off at once</b>, and
/// eleven seconds later the cycle starts again</description></item>
/// </list>
/// <para>
/// <b>Every pair picks its player independently.</b> Retail writes the spawn twice rather than asking
/// for two, and each is its own <c>ATTACKERI_RANDOM_ONE</c>, so a raid of six ends up with mines
/// scattered across it rather than eight under one person — which is the difference between a mechanic
/// and an execution.
/// </para>
/// <para>
/// <b>Retail's own clean-up here does nothing.</b> Both death branches and the leash branch despawn
/// <c>SPAWN_ID_1</c>, and every mine is laid with <c>SPAWN_ID_NONE</c>, so nothing is ever in that
/// group. Kept as written — the mines' forty-second lifetime is what actually clears them, and porting
/// a despawn that clears an empty group is porting the quirk rather than fixing it.
/// </para>
/// <para>
/// <b>Not translated.</b> Timers 1 and 2, a closed two-cast loop nothing else touches; the summoning
/// motion on timer 0 and the self-buff on waking; and <c>on_enter_return_sp</c>, an event our runtime
/// does not raise.
/// </para>
/// </remarks>
[AIName("tidalsail_spirit")]
public class TidalsailSpiritAI : PatternAi
{
	/// <summary><c>BDF5_ItemNamed_6_05_Summon_65_Ah</c> — a mine.</summary>
	private const int Mine = 855920;

	/// <summary>Retail's <c>SPAWN_ID_NONE</c>: the mines belong to no group, and nothing collects them.</summary>
	private const int Loose = 0;

	/// <summary>Retail's <c>spawn_range</c> and <c>live_time</c> on all eight.</summary>
	private const float UnderFoot = 1f;
	private const int Life = 40;

	/// <summary>Retail's <c>range_as_meter</c> on the detonation.</summary>
	private const float Reach = 50f;

	// Retail's battle timer indices. 1 and 2 are its cast loop and are not built.
	private const int Cycle = 0;
	private const int First = 3;
	private const int Second = 4;
	private const int Third = 5;
	private const int Fourth = 6;
	private const int Blow = 7;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(7, "", When.Always,
				Do.ArmTimer(Cycle, 6000))),

		OnBattleTimer = Of(
			Branch(50, "", [When.Timer(Cycle)],
				Do.ArmTimer(First, 6000)),

			Lay(49, First, Second),
			Lay(48, Second, Third),
			Lay(47, Third, Fourth),
			Lay(46, Fourth, Blow),

			Branch(45, "and they all go off", [When.Timer(Blow)],
				Do.ArmTimer(Cycle, 11000),
				Do.Broadcast(Df5MineAI.Detonate, Reach))),
	};

	/// <summary>One pair of mines, each on its own randomly chosen attacker.</summary>
	private static PatternBranch Lay(int priority, int timer, int next)
		=> Branch(priority, "a pair of mines", [When.Timer(timer)],
			Do.ArmTimer(next, 6000),
			Do.SpawnOnAttacker(AggroTarget.RANDOM, Mine, Loose, range: UnderFoot, liveSeconds: Life),
			Do.SpawnOnAttacker(AggroTarget.RANDOM, Mine, Loose, range: UnderFoot, liveSeconds: Life));

	public TidalsailSpiritAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The Tidalsail Spirit's mines (855920). Retail pattern <c>DF5_ItemNamed_6_Ra_Summon_SSH</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One branch: on hearing the word, cast and go. The
/// cast is the blast and we cannot say it; the <c>despawn_self</c> is what makes the eight of them
/// disappear together, which is the visible half and the half that tells a raid the cycle has turned
/// over. Third pattern in this log whose whole point hides behind what looks like a cast — after the
/// naga summons' dismissal and Kaliga's markers.
/// </remarks>
[AIName("df5_mine")]
public class Df5MineAI : PatternAi
{
	/// <summary>Retail's <c>1001</c>, which here means "now".</summary>
	public const int Detonate = 1001;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(7, "", [When.Message(Detonate)],
				Do.DespawnSelf())),
	};

	public Df5MineAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Infernomane Vortile (219930), the blaze dropper of the same family. Retail pattern
/// <c>DF5_ItemNamed_6_Wi_01_SSH</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. A HERO on plain <c>aggressive</c> running a timer
/// loop that closes on itself, twice over:
/// <list type="table">
/// <item><term>above fifty</term><description>five steps of ten seconds, and on the third and fifth of
/// them he turns to a random attacker and drops <b>two blazes</b> on them</description></item>
/// <item><term>below fifty</term><description>the loop loses a step and the blazes gain one: four
/// steps, <b>three blazes</b> twice round</description></item>
/// </list>
/// <para>
/// <b>The escalation is in the shape of the loop, not in a new branch.</b> Dropping a step takes the
/// cycle from fifty seconds to forty while the count per drop goes from two to three, so the blaze rate
/// nearly doubles without retail writing a single word about enraging.
/// </para>
/// <para>
/// <b>Not translated.</b> Eight skill indices, including the area attack that lands with each drop;
/// the self-buff on waking and on leashing; and <c>on_enter_return_sp</c>. The blazes themselves
/// (282390) walk and lay a trail of standing fire in retail, which is an encounter of its own in
/// another file and is left on the stock AI — recorded so the trail is not mistaken for missing here.
/// </para>
/// </remarks>
[AIName("infernomane_vortile")]
public class InfernomaneVortileAI : PatternAi
{
	/// <summary><c>IDYun_3Nmd_Blaze</c> — a walking fire.</summary>
	private const int Blaze = 282390;

	/// <summary>Retail's <c>SPAWN_ID_1</c>, which both his death branches clear.</summary>
	private const int Fires = 1;

	/// <summary>Retail's <c>spawn_range</c> and <c>live_time</c> on every drop.</summary>
	private const float Ring = 2f;
	private const int Life = 15;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(90, "", When.Always,
				Do.ArmTimer(0, 6000))),

		OnBattleTimer = Of(
			// Above fifty: five steps, and two of them drop a pair.
			Branch(50, "", [When.Timer(0), When.HpBetween(50, 100)], Do.ArmTimer(1, 10000)),
			Branch(49, "", [When.Timer(1), When.HpBetween(50, 100)], Do.ArmTimer(2, 10000)),
			Drop(48, 2, 3, count: 2, band: When.HpBetween(50, 100)),
			Branch(47, "", [When.Timer(3), When.HpBetween(50, 100)], Do.ArmTimer(4, 10000)),
			Drop(46, 4, 0, count: 2, band: When.HpBetween(50, 100)),

			// Below fifty: one step shorter, one blaze more.
			Branch(30, "", [When.Timer(0), When.HpBelow(49)], Do.ArmTimer(1, 10000)),
			Branch(29, "", [When.Timer(1), When.HpBelow(49)], Do.ArmTimer(2, 10000)),
			Drop(28, 2, 3, count: 3, band: When.HpBelow(49)),
			Drop(26, 3, 0, count: 3, band: When.HpBelow(49))),

		OnDie = Of(
			Branch(11, "", When.Always,
				Do.Despawn(Fires))),
	};

	/// <summary>A drop: turn to a random attacker, then put the fires on whoever that is.</summary>
	private static PatternBranch Drop(int priority, int timer, int next, int count, PatternCondition band)
		=> Branch(priority, $"{count} blazes", [When.Timer(timer), band],
			Do.ArmTimer(next, 10000),
			Do.SwitchTarget(AggroTarget.RANDOM),
			Do.SpawnOnTarget(Blaze, Fires, count: count, range: Ring, liveSeconds: Life));

	public InfernomaneVortileAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
