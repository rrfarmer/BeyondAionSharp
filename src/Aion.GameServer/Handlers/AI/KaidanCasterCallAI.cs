using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The kaidan casters and the smackstoppers they call. Retail numbers <c>1004</c> and <c>1005</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A hurt kaidan caster shouts twice at once</b> —
/// <c>1004</c> naming itself and <c>1005</c> naming whoever it is fighting — so one cry asks for a heal
/// and the other asks for a kill, and the camp splits its answer between them.
/// <para>
/// <b>Only half of that lands here.</b> The answer to <c>1004</c> is a heal cast on the caller, which
/// needs a skill index; the answer to <c>1005</c> is a target switch with a hundred points behind it,
/// which does not. The <c>1004</c> call is still sent — it costs nothing and a listener for it will
/// work the day skills land — but nothing on this server answers it yet.
/// </para>
/// <para>
/// <b>The call is a band, not a threshold.</b> Retail guards it with <c>is_hp_in_boundary</c>, so a
/// caster burned past the bottom of its band never calls at all: a burst that takes a shaman from full
/// to a fifth in one go silences it, where a slower fight would have brought the smackstoppers.
/// </para>
/// </remarks>
public static class KaidanCalls
{
	/// <summary>Retail's <c>1004</c>: heal me. Nothing here answers it yet — the answer is a skill.</summary>
	public const int HealMe = 1004;

	/// <summary>Retail's <c>1005</c>: kill him.</summary>
	public const int KillHim = 1005;

	/// <summary>Retail's <c>range_as_meter</c> on both calls.</summary>
	public const float Reach = 15f;

	/// <summary>The smackstopper's <c>points_to_add</c> on its switch.</summary>
	public const int Commit = 100;

	/// <summary>Retail's <c>BTIMERI_INDEX_0</c> — the caster's own clock.</summary>
	internal const int CallTimer = 0;

	/// <summary>Retail's <c>BTIMERI_INDEX_1</c>, which on a soothsayer drives its target switching.</summary>
	internal const int SwitchTimer = 1;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_*</c> on the call branch: it is spent once per fight.</summary>
	internal const int Called = 1;

	/// <summary>The two calls, in retail's order — self first, then the target.</summary>
	internal static PatternAction[] BothCalls =>
	[
		Do.Broadcast(HealMe, Reach, aboutTarget: false),
		Do.Broadcast(KillHim, Reach, aboutTarget: true),
	];
}

/// <summary>
/// The kaidan shamans. Retail pattern <c>NKrall_WeA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Calls between <b>41 and 75 percent</b>, once, on a
/// nine-second clock that then runs at six.
/// <para>
/// <b>Not translated:</b> every <c>use_skill</c> on the pattern, its <c>say_to_all</c>, and its
/// <c>1399</c> shout on entering the fight — a number with no live listener on this server. The
/// low-health branch above the call is gated on <c>is_skill_count_left</c>, which this port cannot read,
/// so it is left out rather than fired unconditionally.
/// </para>
/// </remarks>
[AIName("kaidan_shaman")]
public class KaidanShamanAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(5, "pulled", [], Do.ArmTimer(KaidanCalls.CallTimer, 9_000))),

		OnBattleTimer = Of(
			Branch(3, "hurt but not spent, once",
				[When.Timer(KaidanCalls.CallTimer), When.HpBetween(41, 75),
					When.FirstTime(KaidanCalls.Called)],
				[Do.ArmTimer(KaidanCalls.CallTimer, 6_000), .. KaidanCalls.BothCalls]),

			Branch(1, "keep the clock running", [When.Timer(KaidanCalls.CallTimer)],
				Do.ArmTimer(KaidanCalls.CallTimer, 6_000))),
	};

	public KaidanShamanAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The kaidan chieftains. Retail pattern <c>NKrall_WeB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The widest band of the three</b> — 36 to 75 — so a
/// chieftain calls where a soothsayer beside it has already gone quiet.
/// <para>
/// <b>Not translated:</b> its two <c>on_attacked</c> bands and their <c>on_spelled</c> twins, which are
/// a <c>say_to_all</c> and a skill apiece and gated on <c>is_user_class</c>; the skills; and
/// <c>1399</c>.
/// </para>
/// </remarks>
[AIName("kaidan_chieftain")]
public class KaidanChieftainAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(9, "pulled", [], Do.ArmTimer(KaidanCalls.CallTimer, 6_000))),

		OnBattleTimer = Of(
			Branch(7, "hurt but not spent, once",
				[When.Timer(KaidanCalls.CallTimer), When.HpBetween(36, 75),
					When.FirstTime(KaidanCalls.Called)],
				[Do.ArmTimer(KaidanCalls.CallTimer, 6_000), .. KaidanCalls.BothCalls]),

			Branch(1, "keep the clock running", [When.Timer(KaidanCalls.CallTimer)],
				Do.ArmTimer(KaidanCalls.CallTimer, 6_000))),
	};

	public KaidanChieftainAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The crack kaidan soothsayers. Retail pattern <c>NKrall_WeC</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The narrowest band — 46 to 75 — and the call kills
/// its own clock.</b> Retail's call branch is the only one on <c>BTIMERI_INDEX_0</c> that does not
/// re-arm it, and branches are first-match-wins, so the tick that carries the cry is the last tick that
/// timer ever has. The soothsayer calls once and then keeps its remaining clocks only.
/// <para>
/// <b>And it switches targets every twenty-five seconds</b>, taking a random one of its attackers with a
/// hundred points behind the switch — the one piece of its three-skill rotation that is not a skill.
/// </para>
/// <para>
/// <b>Not translated:</b> the three skills on that rotation, the fourth on its seventeen-second clock,
/// its <c>say_to_all</c>, and <c>percent_to_add=10</c> on the switch, which this port has no equivalent
/// for and which is recorded on every switch in this log.
/// </para>
/// </remarks>
[AIName("kaidan_soothsayer")]
public class KaidanSoothsayerAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(5, "pulled", [],
			Do.ArmTimer(KaidanCalls.CallTimer, 6_000),
			Do.ArmTimer(KaidanCalls.SwitchTimer, 20_000))),

		OnBattleTimer = Of(
			// No re-arm, exactly as retail writes it: this is the last tick timer 0 gets.
			Branch(4, "hurt but not spent, once, and the clock stops",
				[When.Timer(KaidanCalls.CallTimer), When.HpBetween(46, 75),
					When.FirstTime(KaidanCalls.Called)],
				KaidanCalls.BothCalls),

			Branch(3, "pick somebody else",
				[When.Timer(KaidanCalls.SwitchTimer)],
				Do.ArmTimer(KaidanCalls.SwitchTimer, 25_000),
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(1, "keep the clock running", [When.Timer(KaidanCalls.CallTimer)],
				Do.ArmTimer(KaidanCalls.CallTimer, 6_000))),
	};

	public KaidanSoothsayerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The kaidan smackstoppers who answer them. Retail pattern <c>NKrall_KeC</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It answers each call once and never again</b> —
/// retail puts a separate flag on each branch, so a smackstopper that has already gone to a caster's
/// target will not be moved by a second cry, whoever makes it.
/// <para>
/// <b>Only the <c>1005</c> half is built.</b> The <c>1004</c> answer is a single <c>use_skill</c> on the
/// caller — a heal — and skill indices are this port's oldest blocker. The branch is left out rather
/// than approximated, and the flag numbering keeps its slot so the two answers stay independent when it
/// arrives.
/// </para>
/// </remarks>
[AIName("kaidan_smackstopper")]
public class KaidanSmackstopperAI : PatternAi
{
	/// <summary>Retail's <c>FLAGVARI_ALPHA_2</c>, on the <c>1005</c> branch.</summary>
	private const int AnsweredKillHim = 2;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(3, "kill him, they said", [When.Message(KaidanCalls.KillHim),
				When.FirstTime(AnsweredKillHim)],
			// One action, not two: HateMessageTarget already sets the target, so retail's
			// switch_target target=OBJI_MESSAGE_PARAM points_to_add=100 maps onto it whole. Adding
			// Do.TargetMessageParam beside it reads like a second step and is a no-op. See
			// docs/retail-ai-fidelity.md on what that conflation costs elsewhere.
			Do.HateMessageTarget(KaidanCalls.Commit))),
	};

	public KaidanSmackstopperAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
