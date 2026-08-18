using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The black claw hunters and breeders of Morheim. Retail patterns <c>Lycan_HeA</c> and
/// <c>Lycan_HnA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>When it picks a fight it tells its taygas who
/// it picked</b>, at fifteen metres — and they come. That is the whole of the lycan camps: the lycan
/// is the one you pull and the tayga is the one that arrives.
/// <para>
/// <b>Two retail patterns translate to one class here, and that is worth saying out loud.</b>
/// <c>Lycan_HeA</c> and <c>Lycan_HnA</c> differ in their timers and their skills — HeA runs a
/// six-second and a twenty-second timer, HnA only the twenty — and every one of those differences is a
/// skill index. When the skill-index blocker lifts, these split back into two.
/// </para>
/// <para>
/// <b>Not translated:</b> the opening skill, the timed skills, and <c>say_to_all</c>. HeA's six-second
/// timer below half health broadcasts <c>2303</c>, which <b>nothing in the 5.8 files listens to</b> —
/// a shout into a channel with no ear on it, and the reason that timer is absent rather than built.
/// </para>
/// </remarks>
[AIName("black_claw_hunter")]
public class BlackClawHunterAI : PatternAi
{
	/// <summary>Retail's <c>2301</c>: that one is mine. Answered only by the taygas.</summary>
	public const int ThatOneIsMine = 2301;

	/// <summary>Retail's <c>range_as_meter</c>, the same across the family.</summary>
	public const float CallReach = 15f;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(5, "picking a fight, and naming it", [],
			Do.Broadcast(ThatOneIsMine, CallReach, aboutTarget: true))),
	};

	public BlackClawHunterAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The black claw tamers and tracers, and Jahama the Ruthless. Retail pattern <c>Lycan_HeB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It calls its taygas onto its target like the
/// hunters do — and when one of them dies, it runs from whoever killed it.</b>
/// <para>
/// <b>The flight is the reason this is a separate class.</b> Everything else HeB has beyond HeA is a
/// skill: a cleanse when a tayga is crowd-controlled, a heal when one drops below half. Those are the
/// answers a tamer is supposed to give, and all three are skill indices. What is left is the answer it
/// gives when it has nothing left to save — it leaves. A player who kills the beast first is not then
/// fighting the tamer; they are chasing it.
/// </para>
/// <para>
/// <b>Not translated:</b> the cleanse (<c>2305</c>), the heal (<c>2306</c>, itself gated on
/// <c>is_skill_count_left</c>), the opening and timed skills, and <c>say_to_all</c>. Its
/// <c>on_stop_to_flee</c> broadcasts <c>1019</c> to the <c>Lycan</c> pattern, <b>none of whose npcs our
/// data places</b>.
/// </para>
/// </remarks>
[AIName("black_claw_tamer")]
public class BlackClawTamerAI : PatternAi
{
	/// <summary>Retail's <c>seconds</c> on the flight.</summary>
	private const int ThreeSeconds = 3;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(9, "picking a fight, and naming it", [],
			Do.Broadcast(BlackClawHunterAI.ThatOneIsMine, BlackClawHunterAI.CallReach,
				aboutTarget: true))),

		OnMessage = Of(Branch(7, "my tayga is dead, and that is who did it",
			[When.Message(TamedTaygaAI.ItWasThem)],
			Do.FleeFromMessageParam(ThreeSeconds))),
	};

	public BlackClawTamerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The taygas the black claws keep. Retail pattern <c>D2_FnM</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It answers its lycan's call, and it answers its
/// lycan's death.</b> Those are two different mechanics and a player meets them in that order.
/// <para>
/// <b>The call.</b> One hate point on whoever the lycan named, then a hundred more — retail writes it
/// as <c>add_hate_point</c> of one followed by <c>switch_target</c> carrying a hundred, so the number
/// that lands is <b>a hundred and one</b>, and it is retail's hundred and one rather than a rounding
/// of ours.
/// </para>
/// <para>
/// <b>The death.</b> Retail's <c>on_sense_friend_killed_by_user</c> takes a point on
/// <c>OBJI_KILLER</c> and then a hundred on whoever it is now facing. <b>That branch is the reason the
/// friend-killed handler now carries the killer at all</b> — it was shipped with a remark saying
/// retail's branches never name one, and they name one in a third of them. See
/// <see cref="Ai.Pattern.PatternAi.FriendsKiller"/>.
/// </para>
/// <para>
/// <b>And it says its own killer's name as it dies</b>, which is what sends its tamer running.
/// </para>
/// <para>
/// <b>Not translated:</b> every skill on every branch; the shouts; the two-second and three-second
/// battle timers, whose only action is a shout; <c>on_enter_abnormal_state</c>, which broadcasts
/// <c>2305</c> — this port has no handler for entering an abnormal state, and the cleanse that answers
/// it is a skill index anyway; and the <c>2302</c> and <c>2304</c> branches, whose message numbers
/// <b>nothing in either the 2.7 or the 5.8 files broadcasts</b>. Those two are dead wire in NCSoft's
/// own data, not a gap in ours.
/// </para>
/// </remarks>
[AIName("tamed_tayga")]
public class TamedTaygaAI : PatternAi
{
	/// <summary>Retail's <c>2307</c>: this is who killed me.</summary>
	public const int ItWasThem = 2307;

	/// <summary>Retail's <c>range_as_meter</c> on the dying call.</summary>
	private const float DeathReach = 15f;

	/// <summary>Retail's <c>point_to_add</c>, taken before the switch on both branches.</summary>
	private const int Glance = 1;

	/// <summary>Retail's <c>points_to_add</c> on the switch that follows it.</summary>
	private const int Commit = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		// One point and then a hundred, in that order, because that is how retail writes it.
		OnMessage = Of(Branch(4, "my lycan has picked one", [When.Message(BlackClawHunterAI.ThatOneIsMine)],
			Do.HateMessageTarget(Glance),
			Do.HateMessageTarget(Commit))),

		OnFriendKilled = Of(Branch(3, "they killed my lycan", [],
			Do.HateFriendsKiller(Glance),
			Do.HateTarget(Commit))),

		OnDie = Of(Branch(8, "naming whoever did it", [],
			Do.BroadcastAboutKiller(ItWasThem, DeathReach))),
	};

	public TamedTaygaAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
