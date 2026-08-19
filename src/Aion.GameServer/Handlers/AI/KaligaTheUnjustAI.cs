using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Kaliga the Unjust (217006), the Angry Judge of Kromede's Trial. Retail pattern
/// <c>Cromede_Named_Angry</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Only one branch of his pattern is built here, and
/// the reasons the rest is not are worth stating in the class rather than only in the log.
/// <para>
/// <b>What is built.</b> When he falls he drops three markers, one on each of his three servants'
/// posts across the manor. Each marker calls the servant standing there away and vanishes: beat the
/// judge and the trial empties. Retail's coordinates land within three metres of our own spawn points
/// for Hamam the Torturer, Lady Angerr and Justicetaker Wyr, which is what identifies them.
/// </para>
/// <para>
/// <b>His health ladder was unreachable, and now is not.</b> Every rung of it — two statues at eighty,
/// two more at fifty, a column on his target, the enrage under twenty-five — sits under
/// <c>on_battle_timer</c>, but timers 0 and 1 are armed <em>only</em> by
/// <c>on_arrived_at_waypoint</c>, at the end of a two-hop walk he takes on entering combat.
/// <c>audit_timer_reach.py</c> exists to find this shape and had ranked him third on the worth-doing
/// list.
/// <para>
/// Two things had to change. The engine grew <c>OnArrivedAtWaypoint</c> and <c>When.AtWaypoint</c>, so
/// retail's own branches can be written — and they are, above. But <b>the instance handler still spawns
/// him on one static spot with no route</b>, so that arrival cannot fire, and the ladder would stay dead
/// if that were all. The timers are therefore armed on entering combat as well: a divergence of the few
/// seconds the walk would have taken, against a fight that otherwise has no mechanics at all.
/// </para>
/// </para>
/// <para>
/// <b>And the rest of his pattern is entangled with <see cref="Instance.KromedesTrialInstance"/>.</b>
/// Retail converts him into Shadow Judge Kaliga (217005) on message <c>6404</c>, which the servants'
/// own death markers broadcast; our instance handler makes the same decision once, at treasury entry,
/// from whether all three servants are dead. Porting the conversion without the rest of that chain
/// would give a scared judge with none of the wounded servants beside him. Specified in full in the log
/// and deliberately left for one change rather than three.
/// </para>
/// </remarks>
[AIName("kaliga_the_unjust")]
public class KaligaTheUnjustAI : PatternAi
{
	/// <summary><c>IDCromede_Invisible_NPC15</c> — the marker that calls one servant away.</summary>
	private const int Dismissal = 282115;

	/// <summary>Retail's <c>SPAWN_ID_NONE</c>: nothing ever despawns these, they expire.</summary>
	private const int Loose = 0;

	/// <summary>Retail's <c>live_time</c>, far longer than the marker needs.</summary>
	private const int Life = 60;

	/// <summary>
	/// Retail's three absolute placements, each within three metres of one servant's post:
	/// Hamam the Torturer (216982), Lady Angerr (217000) and Justicetaker Wyr (217002).
	/// </summary>
	private static readonly SpawnSpot[] Posts =
	{
		new SpawnSpot(749.8f, 628.18f, 198.37f),
		new SpawnSpot(512.55f, 574.35f, 217.6f),
		new SpawnSpot(568.19f, 833.13f, 226.33f),
	};

	/// <summary>
	/// Retail <c>Cromede_Named_Angry</c> <c>on_leave_attack_state</c>: two invisible markers at his own
	/// point, ten seconds each.
	/// </summary>
	/// <remarks>
	/// <b>Retail-sourced; see docs/retail-ai-fidelity.md.</b> He placed nothing at all on going home,
	/// where retail leaves this pair behind — the going-home counterpart to the dismissal he already
	/// drops on death.
	/// </remarks>
	private const int LeavingMarkerA = 282084;
	private const int LeavingMarkerB = 282085;
	private const int LeavingMarkerLife = 10;

	/// <summary><c>IDCromede_StatueM_38_An</c> — the ancient temple nagolem, two at a time.</summary>
	private const int Nagolem = 282124;

	/// <summary>
	/// The two posts the statues take. Retail names the same pair at eighty and at fifty.
	/// </summary>
	private static readonly SpawnSpot[] StatuePosts =
	{
		new SpawnSpot(633.67f, 756.79f, 216.14f),
		new SpawnSpot(633.67f, 791.59f, 216.14f),
	};

	/// <summary><c>IDCromede_Invisible_NPC20</c> — the votaic column, dropped on his quarry.</summary>
	private const int VotaicColumn = 282120;
	private const float ColumnReach = 50f;

	/// <summary>Retail's <c>hatepoints_to_add</c> on the column: one, which is a nudge and not a lock.</summary>
	private const int ColumnHate = 1;

	/// <summary>Retail's <c>SPAWN_ID_2</c> and <c>_3</c>: one group per statue rung.</summary>
	private const int EightyStatues = 2;
	private const int FiftyStatues = 3;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1..3</c> — each rung opens once.</summary>
	private const int Below25Opened = 1;
	private const int Below80Opened = 2;
	private const int Below50Opened = 3;

	private const int LadderClock = 0;
	private const int ColumnClock = 1;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		// Retail's own way in: he walks two hops on entering combat and arms the ladder on reaching the
		// fourth waypoint. Both branches are here so that a Kaliga who is ever given a route runs the
		// retail path exactly -- the engine grew `OnArrivedAtWaypoint` and `When.AtWaypoint` this
		// session, which is what makes writing them possible at all.
		OnArrivedAtWaypoint = Of(
			Branch(109, "second hop", [When.AtWaypoint(2)],
				Do.StartWalking()),

			Branch(108, "and the ladder starts", [When.AtWaypoint(4)],
				Do.ArmTimer(LadderClock, 5000),
				Do.ArmTimer(ColumnClock, 20000))),

		OnEnterAttack = Of(
			Branch(110, "", When.Always,
				Do.StartWalking(),

				// AND THE SAME TWO TIMERS, WHICH IS A DIVERGENCE. Retail arms them only after the walk,
				// and our instance handler spawns him on a single static spot with no route -- so the
				// arrival never fires and, until now, his entire health ladder was dead: no statues at
				// eighty, none at fifty, no columns, no enrage. Arming them here costs the few seconds
				// the walk would have taken and buys the whole fight. If he is ever given his route the
				// branches above arm them again, which is harmless.
				Do.ArmTimer(LadderClock, 5000),
				Do.ArmTimer(ColumnClock, 20000))),

		OnBattleTimer = Of(
			// The ladder. Retail writes the rungs deepest-first, so a judge dropped straight past two
			// of them takes the deepest that matches and walks up on later ticks.
			Branch(100, "below 25", [When.Timer(LadderClock), When.HpBelow(25),
					When.FirstTime(Below25Opened)],
				Do.ArmTimer(LadderClock, 5000)),

			Branch(99, "below 50", [When.Timer(LadderClock), When.HpBelow(50),
					When.FirstTime(Below50Opened)],
				Do.ArmTimer(LadderClock, 5000),
				Do.SpawnAt(Nagolem, FiftyStatues, liveSeconds: 0, StatuePosts)),

			Branch(98, "below 80", [When.Timer(LadderClock), When.HpBelow(80),
					When.FirstTime(Below80Opened)],
				Do.ArmTimer(LadderClock, 5000),
				Do.SpawnAt(Nagolem, EightyStatues, liveSeconds: 0, StatuePosts)),

			// A coin flip every twenty seconds below fifty: a column on whoever he is facing.
			Branch(97, "the column", [When.Chance(50), When.HpBelow(50), When.Timer(ColumnClock)],
				Do.ArmTimer(ColumnClock, 20000),
				Do.SpawnOnTarget(VotaicColumn, Loose, count: 1, liveSeconds: 0,
					attackHate: ColumnHate, validDistance: ColumnReach)),

			// The rungs whose only content is a cast keep their clocks running; without them the chain
			// stops on its first tick in a band that has already opened.
			Branch(96, "below 50, casts", [When.Timer(ColumnClock), When.HpBelow(50)],
				Do.ArmTimer(ColumnClock, 20000)),

			Branch(95, "51-100, casts", [When.Timer(ColumnClock), When.HpBetween(51, 100)],
				Do.ArmTimer(ColumnClock, 20000)),

            Branch(1, "", [When.Timer(LadderClock)],
				Do.ArmTimer(LadderClock, 5000))),

		OnDie = Of(
			Branch(7, "", When.Always,
				Do.SpawnAt(Dismissal, Loose, Life, Posts))),

		OnLeaveAttack = Of(
			Branch(7, "", When.Always,
				Do.SpawnNear(LeavingMarkerA, Loose, count: 1, range: 0f, liveSeconds: LeavingMarkerLife),
				Do.SpawnNear(LeavingMarkerB, Loose, count: 1, range: 0f, liveSeconds: LeavingMarkerLife))),
	};

	public KaligaTheUnjustAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The marker Kaliga leaves on each servant's post (282115). Retail pattern
/// <c>Cromede_Kkt_Noshow</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Two actions and nothing else: say the word within
/// fifty metres, and go. It is invisible, it lives a minute, and its entire purpose is to carry one
/// message to one place — which is how retail addresses a specific NPC from a pattern that has no way
/// to name one.
/// </remarks>
[AIName("kromede_dismissal_marker")]
public class KromedeDismissalMarkerAI : PatternAi
{
	/// <summary>Retail's <c>range_as_meter</c>. The servant it is for stands within three.</summary>
	private const float Reach = 50f;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnWakeUp = Of(
			Branch(7, "", When.Always,
				Do.Broadcast(KromedeServantAI.Dismissed, Reach),
				Do.DespawnSelf())),
	};

	public KromedeDismissalMarkerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Kaliga's servants — Hamam the Torturer (216982) and Justicetaker Wyr (217002). Retail patterns
/// <c>Cromede_Torture</c> and <c>Cromede_Assijudge</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. One branch of each pattern is built: <b>they leave
/// when the judge falls.</b>
/// <para>
/// <b>Not translated, and why.</b> Both patterns are much larger, and the two mechanics worth having
/// are held back together rather than half-landed. At thirty percent each servant drops a marker at
/// the judge's dais and <em>removes itself instead of dying</em>; that marker seats a wounded copy of
/// the servant (217003, 217004 and Lady Angerr's 217001) beside him and broadcasts the conversion.
/// <see cref="Instance.KromedesTrialInstance"/> already produces the end state of that chain — scared
/// judge plus three wounded servants, at coordinates within two metres of retail's — but it does so
/// from a single <c>IsDead</c> check at treasury entry. Landing retail's half of it piecemeal would
/// either double the wounded servants or strand the <c>IsDead</c> gate on servants that no longer die.
/// </para>
/// <para>
/// <b>Lady Angerr (217000) is not here.</b> She is on our <c>summoner</c> AI with a tuned
/// <c>spawn_helpers.xml</c> ladder for her six bats; giving her this class would drop it. Her retail
/// pattern carries that same wave, so she wants the whole hand-over at once, not a fourth branch.
/// </para>
/// </remarks>
[AIName("kromede_servant")]
public class KromedeServantAI : PatternAi
{
	/// <summary>Retail's word for "the trial is over, go".</summary>
	public const int Dismissed = 6406;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(7, "", [When.Message(Dismissed)],
				Do.DespawnSelf())),
	};

	public KromedeServantAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
