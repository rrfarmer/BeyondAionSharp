using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The klaws that call for the pack. Retail pattern <c>ND2_CnD_BR1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Below half health, once, it names whoever it is
/// fighting to every klaw within twenty metres</b> — and it goes on fighting. It is the ordinary klaw of
/// Beluslan, Morheim and Brusthonin: a warden, a patrol, a gatherer, and the two klaw royals.
/// <para>
/// <b>It also answers.</b> One hate point on whoever another klaw named — the glance, not the claim, in
/// the vasharti watch's sense. What makes the mechanic is that the answer has no state guard at all:
/// a klaw already in a fight of its own takes the point too, so a call landing in the middle of a camp
/// pulls the camp.
/// </para>
/// <para>
/// <b>Not translated:</b> the skill it opens the call with, and the skill it answers with — both skill
/// indices, this port's oldest blocker.
/// </para>
/// </remarks>
[AIName("klaw_call")]
public class KlawCallerAI : PatternAi
{
	/// <summary>
	/// Retail's <c>2003</c>: this one is hurting me. Shared by every pattern in the klaw family, which
	/// is what makes a mixed camp answer as one.
	/// </summary>
	public const int HurtingMe = 2003;

	/// <summary>Retail's <c>range_as_meter</c>, the same on every caller in the family.</summary>
	public const float CallReach = 20f;

	/// <summary>Retail's <c>point_to_add</c> on an ordinary klaw's answer.</summary>
	private const int Glance = 1;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c>, shared across both provocations.</summary>
	private const int Called = 1;

	private const int Half = 50;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(1, "hit, below half", [When.HpBelow(Half), When.FirstTime(Called)],
			Do.Broadcast(HurtingMe, CallReach, aboutTarget: true))),

		OnSpelled = Of(Branch(1, "cast at, below half",
			[When.HpBelow(Half), When.CasterIsEnemy, When.FirstTime(Called)],
			Do.Broadcast(HurtingMe, CallReach, aboutTarget: true))),

		// No state guard, unlike every other answer in the family -- see the class remarks.
		OnMessage = Of(Branch(2, "a klaw is calling", [When.Message(HurtingMe)],
			Do.HateMessageParam(Glance))),
	};

	public KlawCallerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The klaw sentinels, who call and then run. Retail pattern <c>ND2_CnD_BR3</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>At a third health it buffs itself, names its
/// target to the camp, and flees</b> — the only member of the family that leaves. King klawtun, nanny
/// nuk, the klaw royal guard and the assaulters and sentinels around them.
/// <para>
/// <b>The flee is the mechanic and the call is what makes it work.</b> A sentinel breaking away from a
/// player mid-fight would otherwise just be a reset; naming the player on the way out means the klaws
/// it runs past pick the fight up. The player chasing the runner is the one the camp is already
/// hating.
/// </para>
/// <para>
/// <b>Three seconds when it is hit and four when it is cast at</b>, which is retail's own asymmetry and
/// is kept. It is the only difference between the two branches beyond the caster guard.
/// </para>
/// <para>
/// <b>And its answer is guarded on being idle</b>, unlike <see cref="KlawCallerAI"/>'s — a sentinel
/// already fighting gets retail's <c>attack_most_hating</c> and nothing else, which for an npc already
/// attacking its most hated is a no-op. That branch is deliberately absent rather than untranslated.
/// </para>
/// <para>
/// <b>Not translated:</b> the self-buff, the skill on the answer, and <c>on_stop_to_flee</c> — a skill
/// on whoever it stops in front of. All three are skill indices.
/// </para>
/// </remarks>
[AIName("klaw_sentinel")]
public class KlawSentinelAI : PatternAi
{
	/// <summary>Retail's <c>seconds</c> on the melee branch's <c>flee_from</c>.</summary>
	private const int ThreeSeconds = 3;

	/// <summary>Retail's <c>seconds</c> on the spell branch's — one longer, and kept.</summary>
	private const int FourSeconds = 4;

	/// <summary>Retail's <c>point_to_add</c> on the answer.</summary>
	private const int Glance = 1;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c>, shared across both provocations.</summary>
	private const int Called = 1;

	private const int Third = 35;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(4, "hit, at a third", [When.HpBelow(Third), When.FirstTime(Called)],
			Do.Broadcast(KlawCallerAI.HurtingMe, KlawCallerAI.CallReach, aboutTarget: true),
			Do.Flee(ThreeSeconds))),

		OnSpelled = Of(Branch(3, "cast at, at a third",
			[When.HpBelow(Third), When.CasterIsEnemy, When.FirstTime(Called)],
			Do.Broadcast(KlawCallerAI.HurtingMe, KlawCallerAI.CallReach, aboutTarget: true),
			Do.Flee(FourSeconds))),

		OnMessage = Of(Branch(1, "a klaw is calling, and I am not busy",
			[When.Message(KlawCallerAI.HurtingMe), When.Idle],
			Do.HateMessageParam(Glance))),
	};

	public KlawSentinelAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The klaws that only ever answer, and answer hard. Retail pattern <c>ND2_CnD_RE1</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It has no <c>on_attacked</c> and no
/// <c>on_spelled</c> — it never calls for anybody.</b> What it has is a thousand hate points for
/// whoever a caller named, which is not a glance: an idle spy or gatherer hearing the call crosses the
/// camp and commits.
/// <para>
/// <b>That thousand against the callers' one is the whole shape of the pack.</b> Twenty-six of these
/// stand around Beluslan and Morheim next to seventeen callers, and pulling one klaw at a third health
/// brings the peons rather than the wardens.
/// </para>
/// <para>
/// <b>Already fighting, it switches to a random attacker instead</b> — the same call, and what it does
/// with it depends entirely on whether it was busy. A camp answering a single cry therefore scatters
/// its existing fights and converges its idle ones at once.
/// </para>
/// <para>
/// <b>Not built: the <c>2004</c> pair.</b> Retail gives this pattern the same two branches again on a
/// second message number, whose only sender anywhere in the 5.8 files is <c>ND2_CnD_BR2</c>'s relay —
/// and our data places no <c>ND2_CnD_BR2</c> npc at all. The branches would be unreachable duplicates
/// of the two below. Recorded rather than written; if a BR2 klaw is ever placed, this is the pattern
/// that needs the pair back.
/// </para>
/// <para>
/// <b>Not translated:</b> the skill on each branch, and retail's <c>points_to_add=100</c> on the
/// switch, which this port's <c>SwitchTarget</c> does not carry — the established translation since the
/// Anuhart casters.
/// </para>
/// </remarks>
[AIName("klaw_escort")]
public class KlawEscortAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c> — a claim, against the callers' single point.</summary>
	private const int Commit = 1000;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(2, "a klaw is calling, and I am busy",
				[When.Message(KlawCallerAI.HurtingMe), When.Fighting],
				Do.SwitchTarget(AggroTarget.RANDOM)),

			Branch(1, "a klaw is calling, and I am not",
				[When.Message(KlawCallerAI.HurtingMe), When.Idle],
				Do.HateMessageParam(Commit))),
	};

	public KlawEscortAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
