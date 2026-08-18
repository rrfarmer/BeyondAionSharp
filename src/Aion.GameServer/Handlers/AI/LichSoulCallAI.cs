using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The liches and skeleton magicians that call a soul out of a stone when they are hurt. Retail pattern
/// <c>ND2_Callsoulst</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Below half health, once, it puts a faithful
/// servant at its feet and tells it who to go for</b> — a spawn and a call in the same branch, at ten
/// metres, naming whoever the lich is currently holding.
/// <para>
/// <b>The same shape as the stoneskin stoffu one entry ago, without the delay.</b> The stoffu arms a
/// three-second timer and calls when it runs out, which is a window; the lich calls immediately, which
/// is not. Two encounters, one idiom, and the difference between them is the entire difference in how
/// the two fights feel — worth noticing that retail expresses it by moving one action between two
/// branches.
/// </para>
/// <para>
/// <b>Both provocations, one flag.</b> Retail writes the branch twice — <c>on_attacked</c> and
/// <c>on_spelled</c>, the latter additionally guarded on <c>is_enemy</c> — sharing
/// <c>FLAGVARI_ALPHA_1</c>, so a lich pays out once whether it was hit or cast at.
/// </para>
/// <para>
/// <b>The call has to reach what the branch just made.</b> <c>PatternAi</c> excludes a branch's own
/// spawns from its broadcasts by default — a heuristic written for a pattern that lays traps and
/// immediately tells traps to leave — and spawn-then-point is the counter-example, so this table opts
/// back in. Without it the servant lands and stands there, which is what the first run of these pins
/// measured.
/// </para>
/// <para>
/// <b>Not translated:</b> the self-cast that goes with the summon (<c>SKILLI_INDEX_1</c>), and the
/// servant's own skill on engaging.
/// </para>
/// </remarks>
[AIName("lich_soul_call")]
public class LichSoulCallAI : PatternAi
{
	/// <summary>Retail's <c>BLF3_SouledstoneSULA_40_An</c> — the faithful servant.</summary>
	private const int FaithfulServant = 286080;

	/// <summary>Retail's <c>2006</c>: that one, go. Shared with the stoneskin stoffu's fragments.</summary>
	public const int PointIt = 2006;

	/// <summary>Retail's <c>SPAWN_ID_1</c>, <c>spawn_range</c> and <c>live_time</c>.</summary>
	private const int Servants = 1;
	private const float AtItsFeet = 3f;
	private const int FiftyMinutes = 3000;

	/// <summary>Retail's <c>range_as_meter</c> on the call — a tenth of the stoffu's forty.</summary>
	private const float CallReach = 10f;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c>, shared across both provocations.</summary>
	private const int Called = 1;

	private const int Half = 50;

	private static PatternAction[] Call() =>
	[
		Do.SpawnNear(FaithfulServant, Servants, count: 1, range: AtItsFeet, liveSeconds: FiftyMinutes),
		Do.Broadcast(PointIt, CallReach, aboutTarget: true, includeOwnSpawns: true),
	];

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(3, "hit, below half", [When.HpBelow(Half), When.FirstTime(Called)],
			Call())),

		OnSpelled = Of(Branch(2, "cast at, below half",
			[When.HpBelow(Half), When.CasterIsEnemy, When.FirstTime(Called)],
			Call())),

		OnDie = Of(Branch(4, "and take the servant", [], Do.Despawn(Servants))),
	};

	public LichSoulCallAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The faithful servants a lich calls out of its stone (286080). Retail pattern <c>ND2_PnC</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Its whole pattern is the answer to its lich's call.
/// <para>
/// <b>Retail writes the commitment twice over, and the two halves are different.</b> One
/// <c>add_hate_point</c> of a single point, and then a <c>switch_target</c> carrying a hundred points
/// <em>and</em> a hundred percent. The point is a glance; the switch is what actually commits it. Our
/// <see cref="Do.HateMessageTarget"/> is the second of those, so it is what the table uses — the lone
/// point ahead of it changes nothing that survives the switch.
/// </para>
/// <para>
/// <b>Not translated:</b> the skill it casts on engaging, and <c>percent_to_add</c>, which this port
/// has no equivalent for — retail can add a percentage of the target's existing hate as well as a flat
/// figure, and only the flat figure is ported. Recorded rather than approximated: on a fresh servant
/// with an empty aggro list a percentage of nothing is nothing, so the two agree today and would
/// diverge for a servant that had been fighting.
/// </para>
/// </remarks>
[AIName("faithful_servant")]
public class FaithfulServantAI : PatternAi
{
	/// <summary>Retail's <c>points_to_add</c> on the switch.</summary>
	private const int Claim = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "that one, go",
			[When.Message(LichSoulCallAI.PointIt)],
			Do.HateMessageTarget(Claim))),
	};

	public FaithfulServantAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
