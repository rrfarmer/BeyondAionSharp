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
/// <b>His whole health ladder is unreachable, and not for the usual reason.</b> Every rung of it — two
/// statues at eighty, two more at fifty, a hazard on his target, the enrage under twenty-five — sits
/// under <c>on_battle_timer</c>, which is an ordinary handler. But timers 0 and 1 are armed <em>only</em>
/// by <c>on_arrived_at_waypoint</c>, at the end of a two-hop scripted walk he takes on entering combat.
/// Our runtime has no waypoint-arrival event and the instance handler gives him a single static spot,
/// so those timers are never armed and the whole ladder is dead. <c>audit_timer_reach.py</c> exists to
/// find this shape; it had ranked him third on the worth-doing list.
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

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnDie = Of(
			Branch(7, "", When.Always,
				Do.SpawnAt(Dismissal, Loose, Life, Posts))),
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
