using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Ophidan Bridge's linked pull, and the sweep that comes with a boss. Retail patterns
/// <c>BIDF5_U01_Boss_Wi</c>, <c>BIDF5_U01_Monster_01</c>, <c>BIDF5_U01_Boss_Wi_Nor</c> and the twelve
/// <c>BIDF5_U01_Runaway_*</c>, all in <c>NpcAIPatterns_IDLDF5_Under_01_JSM.xml</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Seventeen npcs, every one a HERO on plain
/// <c>aggressive</c>, and two mechanics between them:
/// <list type="table">
/// <item><term>the call</term><description>on engaging, broadcast at <b>thirty metres</b> naming
/// whoever you are fighting; on hearing it, <b>ten thousand</b> hate on that player and
/// go</description></item>
/// <item><term>the sweep</term><description>a boss engaging puts four triggers at four fixed points
/// across the bridge, and each clears the fugitives around it</description></item>
/// </list>
/// <para>
/// <b>The call chains, and the chain is the mechanic.</b> Answering is an entry into combat, and
/// entering combat is what makes an NPC call in turn, so one careless pull walks from group to group.
/// It terminates because an NPC already fighting does not re-enter combat.
/// </para>
/// <para>
/// <b>Ten thousand hate is a hand-off, not a nudge</b> — far above anything a player accumulates, so
/// the called NPC goes to the named target and stays there.
/// </para>
/// <para>
/// <b>The two mechanics do not travel together.</b> Spirited Velkur, the normal-mode boss, sweeps but
/// does not call; the twelve fugitive grades call but do not sweep; only the three hard-mode velkurs
/// do both. That is three combinations out of four in one file, which is why this is a builder.
/// </para>
/// <para>
/// <b>Not translated.</b> The bosses' six-timer round-robin, which is a cast chain whose only
/// non-cast content is three broadcasts that reach nobody who answers with anything but a cast;
/// <c>set_condition_spawn_variable under_01_out</c>; messages <c>10900</c> and <c>10100</c>; fifteen
/// skill indices and a shout. Each with its reason in the log.
/// </para>
/// <para>
/// <b>The escape is half a mechanic, deliberately.</b> Retail's <c>10000</c> branch runs a system
/// message, a shout, a cast, a <c>teleport_target</c> and then <c>despawn_self</c>. We have the last of
/// those and none of the rest, so the fugitives vanish where retail throws them clear first. The
/// visible outcome is the same and the flight is not, which is recorded rather than dressed up.
/// </para>
/// </remarks>
[AIName("ophidan_bridge_call")]
public class OphidanBridgeCallAI : PatternAi
{
	/// <summary>Retail's <c>10500</c>: "this one is mine, help".</summary>
	public const int Call = 10500;

	/// <summary>
	/// Retail's <c>10000</c>: a stronghold has fallen, run. Broadcast by a middle boss as it dies and
	/// answered by every fugitive grade within fifty metres.
	/// </summary>
	public const int Escape = 10000;

	/// <summary>Retail's <c>range_as_meter</c> on the call.</summary>
	private const float Reach = 30f;

	/// <summary>Retail's <c>point_to_add</c>, which is meant to end the argument about who to hit.</summary>
	private const int Decisive = 10000;

	/// <summary><c>BIDLDF5_U01_T_DespawnAll_NPC</c> — the sweep trigger.</summary>
	private const int Sweeper = 857437;

	/// <summary>Retail's <c>SPAWN_ID_NONE</c> for the four triggers.</summary>
	private const int Loose = 0;

	/// <summary>
	/// Our stand-in for <c>despawn_at_attack_state</c>, which retail carries on these spawns and gives
	/// no <c>live_time</c> to back up.
	/// </summary>
	/// <remarks>
	/// The trigger's whole pattern is one sweep on waking, so a lifetime is the honest reading of a
	/// flag we do not model — the same call already made for <c>NTrapAI</c>. Left at five seconds
	/// because nothing about the mechanic depends on how long the trigger stands, only that it does not
	/// stand for ever.
	/// </remarks>
	private const int TriggerLife = 5;

	/// <summary>
	/// <c>IDF5_U01_N_Boss_Wi_65_Ah_Nor</c> — Spirited Velkur, the normal-mode boss. Each hard-mode
	/// velkur clears him the moment it appears, which is retail's own way of saying the two modes are
	/// the same fight and only one of them is running.
	/// </summary>
	private const int NormalModeBoss = 235768;

	/// <summary>
	/// <c>BIDF5_U01_T_Runaway_Check_NPC</c> (856062) — an invisible marker at a fugitive's post. A
	/// fugitive that reaches its second grade clears the marker for that post as it appears.
	/// </summary>
	private const int CheckMarker = 856062;

	/// <summary>Retail's <c>bound_radius</c> and <c>max_count</c>, identical on all eight wake clears.</summary>
	private const float ClearRange = 50f;
	private const int ClearCount = 10;

	/// <summary>Retail's four absolute placements, one per quarter of the bridge.</summary>
	private static readonly SpawnSpot[] Quarters =
	{
		new SpawnSpot(674.2f, 471.7f, 599.4f),
		new SpawnSpot(604.3f, 555.5f, 590.5f),
		new SpawnSpot(528.8f, 437.2f, 620.3f),
		new SpawnSpot(468.6f, 516.8f, 597.5f),
	};

	/// <summary>
	/// What each npc does. None of the three is universal in this file, which is why this is a
	/// builder: <c>Calls</c> is the linked pull, <c>Sweeps</c> drops the four bridge triggers, and
	/// <c>ClearsOnWake</c> is one <c>despawn_by_nameid</c> the moment the npc appears.
	/// </summary>
	private static readonly Dictionary<int, (bool Calls, bool Sweeps, int ClearsOnWake)> Roster = new()
	{
		[235768] = (false, true, 0),               // spirited velkur, normal mode: sweeps, never calls
		[235769] = (true, true, NormalModeBoss),   // velkur aethercaster
		[235770] = (true, true, NormalModeBoss),   // velkur aetherpriest
		[235771] = (true, true, NormalModeBoss),   // velkur aetherknife

		[235756] = (true, false, 0),               // fugitive mazikin, first grade
		[235757] = (true, false, CheckMarker),     // and second, which clears the check marker
		[235787] = (true, false, CheckMarker),
		[235758] = (true, false, 0),               // and third
		[235759] = (true, false, 0),               // fugitive mazikin leader

		[235760] = (true, false, 0),               // runaway hirakiki, the same three grades
		[235761] = (true, false, CheckMarker),
		[235788] = (true, false, CheckMarker),
		[235762] = (true, false, 0),

		[235764] = (true, false, 0),               // escapee asachin, likewise
		[235765] = (true, false, CheckMarker),
		[235789] = (true, false, CheckMarker),
		[235766] = (true, false, 0),
	};

	private static readonly Dictionary<int, AiPattern> Patterns =
		Roster.ToDictionary(e => e.Key, e => Build(e.Value.Calls, e.Value.Sweeps, e.Value.ClearsOnWake));

	private static AiPattern Build(bool calls, bool sweeps, int clearsOnWake)
	{
		var opening = new List<PatternAction>();
		if (calls)
			opening.Add(Do.Broadcast(Call, Reach, aboutTarget: true));
		if (sweeps)
			opening.Add(Do.SpawnAt(Sweeper, Loose, TriggerLife, Quarters));

		return new AiPattern
		{
			OnWakeUp = clearsOnWake == 0
				? Of()
				: Of(Branch(1000, "", When.Always,
					Do.DespawnKind(clearsOnWake, ClearRange, ClearCount))),

			OnEnterAttack = Of(
				Branch(1000, "", When.Always, opening.ToArray())),

			// Calling without sweeping is what a fugitive does, and fleeing is the other half of it:
			// the three velkurs and the normal-mode boss hold their ground when a stronghold falls.
			OnMessage = calls && !sweeps
				? Of(
					Branch(1300, "", [When.Message(Call)],
						Do.HateMessageParam(Decisive)),
					Branch(1250, "a stronghold fell", [When.Message(Escape)],
						Do.DespawnSelf()))
				: calls
					? Of(Branch(1300, "", [When.Message(Call)],
						Do.HateMessageParam(Decisive)))
					: Of(),
		};
	}

	private readonly AiPattern pattern;

	public OphidanBridgeCallAI(Npc owner)
		: base(owner)
	{
		pattern = Patterns[owner.GetNpcId()];
	}

	protected override AiPattern Pattern => pattern;
}

/// <summary>
/// The sweep trigger Ophidan Bridge's bosses drop on engaging (857437). Retail pattern
/// <c>BIDF5_U01_Middle_Boss_Ice</c>, which binds to this one npc and nothing else.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Its entire pattern is one handler and nine
/// identical actions: <b>clear up to ten of each fugitive grade within fifty metres.</b> Four of these
/// land at four fixed points as the boss is pulled, so engaging him empties the bridge of everything
/// the raid was picking through on the way in.
/// <para>
/// <b>This is the first user of <c>despawn_by_nameid</c></b>, which had no vocabulary at all until now
/// and appears 849 times across 171 patterns in the 5.8 dump. Retail names its targets by client
/// devname; the nine here resolve to the three fugitive families times their three grades.
/// </para>
/// </remarks>
[AIName("ophidan_bridge_sweeper")]
public class OphidanBridgeSweeperAI : PatternAi
{
	/// <summary>Retail's <c>bound_radius</c> and <c>max_count</c>, identical on all nine.</summary>
	private const float Sweep = 50f;
	private const int Each = 10;

	/// <summary>The nine grades retail names: mazikin, hirakiki and asachin, P1 through P3.</summary>
	private static readonly int[] Fugitives =
	{
		235756, 235757, 235758,
		235760, 235761, 235762,
		235764, 235765, 235766,
	};

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnWakeUp = Of(
			Branch(1000, "", When.Always,
				Fugitives.Select(id => Do.DespawnKind(id, Sweep, Each)).ToArray())),
	};

	public OphidanBridgeSweeperAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
