using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The shulack mercenaries of the Danuar Sanctuary, and the numbers they talk on.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The first relay in this log.</b> Every other call
/// here goes out once and is answered; a mercenary that hears <c>21253</c> takes its hundred points,
/// arms a one-second timer, and <b>re-broadcasts the call itself</b> — so the alarm walks outward
/// through the camp a second at a time rather than reaching only what stood in the first circle.
/// <para>
/// <b>The relay is flagged and the answer is not.</b> A mercenary relays once, ever, which is what
/// stops the camp ringing forever; but it will answer <c>21271</c> as often as it is sent. Two guards
/// on the same npc doing different jobs.
/// </para>
/// </remarks>
public static class ShulackCalls
{
	/// <summary>Retail's <c>21251</c>: the officers' number, answered with a thousand.</summary>
	public const int Officers = 21251;

	/// <summary>Retail's <c>21253</c>: the alarm that relays.</summary>
	public const int Alarm = 21253;

	/// <summary>Retail's <c>21271</c>: the rank and file's number.</summary>
	public const int RankAndFile = 21271;

	/// <summary>
	/// Retail's <c>21153</c> — <b>a typo, kept.</b> See <see cref="ShulackAssaulterAI"/>.
	/// </summary>
	public const int Mistyped = 21153;

	/// <summary>Retail's <c>range_as_meter</c> on everything but the cannon chief's call.</summary>
	public const float Far = 50f;

	/// <summary>The cannon chief's, which is the shortest in the family.</summary>
	public const float Near = 15f;

	/// <summary>What an officer's call is worth.</summary>
	public const int Officer = 1000;

	/// <summary>What every other call in the family is worth.</summary>
	public const int Ordinary = 100;

	/// <summary>Retail's <c>delay</c> on the timer that carries a relay.</summary>
	public const int RelayMillis = 1_000;

	/// <summary>Retail's <c>BTIMERI_INDEX_10</c>, which exists only to carry the relay.</summary>
	public const int RelayTimer = 10;
}

/// <summary>
/// The rank and file: veteran medics, elite sorcerers and elite scouts. Retail patterns
/// <c>IDF5_U2_ShulackF_Pr_party_65_Ae</c>, <c>_Wi_party_65_Ae</c> and
/// <c>IDF5_U2_ShulackM_Ri_party_65_Ae</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Three patterns, one answer, no differences: a
/// hundred points on whoever the cannon chief named.
/// </remarks>
[AIName("shulack_soldier")]
public class ShulackSoldierAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(99, "the chief is calling", [When.Message(ShulackCalls.RankAndFile)],
			Do.HateMessageTarget(ShulackCalls.Ordinary))),
	};

	public ShulackSoldierAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The dukaki peons and the seized sanctuary miners. Retail patterns
/// <c>IDF5_U2_BrownieM_party_65_An</c> and <c>_solo_65_An</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The slaves answer the alarm too.</b> Their tribe
/// is <c>IDF5U2_SHULACK_SLAVE</c> and they are what the mercenaries are guarding, and they still take a
/// hundred points on whoever the alarm names.
/// </remarks>
[AIName("shulack_slave")]
public class ShulackSlaveAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(99, "the alarm", [When.Message(ShulackCalls.Alarm)],
			Do.HateMessageTarget(ShulackCalls.Ordinary))),
	};

	public ShulackSlaveAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The mercenary watchers, who carry the relay. Retail pattern
/// <c>IDF5_U2_ShulackM_As_party_65_Ae</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Hearing the alarm, it takes a hundred and passes
/// the alarm on a second later</b> — and passes the officers' number with it, so one pull reaches the
/// bodyguards through however many watchers stand between.
/// <para>
/// <b>The relay is once and the answer is not.</b> Retail flags the alarm branch, so a watcher relays a
/// single time; its <c>21271</c> answer carries no flag and fires as often as it is sent.
/// </para>
/// <para><b>Not translated:</b> a probabilistic skill rotation on three further timers.</para>
/// </remarks>
[AIName("shulack_watcher")]
public class ShulackWatcherAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "engaging", [], Do.ArmTimer(0, 3_000))),

		OnMessage = Of(
			Branch(100, "the alarm, and I have not passed it on",
				[When.Message(ShulackCalls.Alarm), When.FirstTime(1)],
				Do.HateMessageTarget(ShulackCalls.Ordinary),
				Do.ArmTimer(ShulackCalls.RelayTimer, ShulackCalls.RelayMillis)),

			Branch(99, "the chief is calling", [When.Message(ShulackCalls.RankAndFile)],
				Do.HateMessageTarget(ShulackCalls.Ordinary))),

		OnBattleTimer = Of(Branch(90, "passing it on", [When.Timer(ShulackCalls.RelayTimer)],
			Do.Broadcast(ShulackCalls.Alarm, ShulackCalls.Far, aboutTarget: true),
			Do.Broadcast(ShulackCalls.Officers, ShulackCalls.Far, aboutTarget: true))),
	};

	public ShulackWatcherAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The veteran assaulters, whose relay is broken in retail's own data. Retail pattern
/// <c>IDF5_U2_ShulackM_Fi_party_65_Ae</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It is the watcher with one digit changed, and the
/// digit matters.</b> Where the watcher relays <c>21253</c>, this pattern relays <b><c>21153</c></b> —
/// and <c>21153</c>'s only listener anywhere in the 5.8 files is
/// <c>IDRuneWP_A3_Protection_65_n</c>, a rune-weapon pattern from a different instance entirely.
/// <para>
/// <b>So half of this npc's relay goes nowhere</b>, and the alarm stops at it while the officers' call
/// still passes. Two npcs standing in the same camp, one relaying correctly and one not, because
/// somebody typed a 1 for a 2.
/// </para>
/// <para>
/// <b>Kept exactly as written.</b> Correcting it would be inventing a mechanic NCSoft does not ship —
/// the shape of the whole log is that retail's quirks are the specification, and a typo is a quirk with
/// a cause rather than a different kind of thing.
/// </para>
/// </remarks>
[AIName("shulack_assaulter")]
public class ShulackAssaulterAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "engaging", [], Do.ArmTimer(0, 5_000))),

		OnMessage = Of(
			Branch(100, "the alarm, and I have not passed it on",
				[When.Message(ShulackCalls.Alarm), When.FirstTime(1)],
				Do.HateMessageTarget(ShulackCalls.Ordinary),
				Do.ArmTimer(ShulackCalls.RelayTimer, ShulackCalls.RelayMillis)),

			Branch(99, "the chief is calling", [When.Message(ShulackCalls.RankAndFile)],
				Do.HateMessageTarget(ShulackCalls.Ordinary))),

		// 21153, not 21253. See the class remarks -- this is retail's typo and it is kept.
		OnBattleTimer = Of(Branch(90, "passing it on, into nothing", [When.Timer(ShulackCalls.RelayTimer)],
			Do.Broadcast(ShulackCalls.Mistyped, ShulackCalls.Far, aboutTarget: true),
			Do.Broadcast(ShulackCalls.Officers, ShulackCalls.Far, aboutTarget: true))),
	};

	public ShulackAssaulterAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The cannon chiefs. Retail pattern <c>IDF5_U2_ShugoG_Fi_party_65_Ae2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Fifteen metres, the shortest call in the
/// family</b>, sent the moment it is pulled — and answered by the eight rank and file with a hundred
/// apiece.
/// </remarks>
[AIName("shulack_cannon_chief")]
public class ShulackCannonChiefAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.ArmTimer(0, 5_000),
			Do.Broadcast(ShulackCalls.RankAndFile, ShulackCalls.Near, aboutTarget: true))),

		OnMessage = Of(Branch(99, "another chief is calling", [When.Message(ShulackCalls.RankAndFile)],
			Do.HateMessageTarget(ShulackCalls.Ordinary))),
	};

	public ShulackCannonChiefAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Unscrupulous Sachirunerk, who calls both numbers at once. Retail pattern
/// <c>IDF5_U2_ShugoG_Wi_party_SN_65_Ae2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Pulled, it sends the alarm and the officers' call
/// together at fifty metres</b>, which is the widest opening in the family — and answers the officers'
/// number with <b>a thousand</b>, ten times what anyone else in the camp gives.
/// </remarks>
[AIName("shulack_chief")]
public class ShulackChiefAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.ArmTimer(0, 5_000),
			Do.Broadcast(ShulackCalls.Officers, ShulackCalls.Far, aboutTarget: true),
			Do.Broadcast(ShulackCalls.Alarm, ShulackCalls.Far, aboutTarget: true))),

		OnMessage = Of(Branch(99, "an officer is calling", [When.Message(ShulackCalls.Officers)],
			Do.HateMessageTarget(ShulackCalls.Officer))),
	};

	public ShulackChiefAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Bodyguard Girakin, who raises only the alarm. Retail pattern
/// <c>IDF5_U2_ShulackF_As_party_SN_65_Ae2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The alarm alone, where its opposite number sends
/// both</b> — and it answers the officers' call with a thousand like the rest of its rank.
/// </remarks>
[AIName("shulack_bodyguard_alarm")]
public class ShulackBodyguardAlarmAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.ArmTimer(0, 7_000),
			Do.Broadcast(ShulackCalls.Alarm, ShulackCalls.Far, aboutTarget: true))),

		OnMessage = Of(Branch(99, "an officer is calling", [When.Message(ShulackCalls.Officers)],
			Do.HateMessageTarget(ShulackCalls.Officer))),
	};

	public ShulackBodyguardAlarmAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// Bodyguard Yatakin, who sends both. Retail pattern <c>IDF5_U2_ShulackF_Ri_party_SN_65_Ae2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Identical to Sachirunerk's opening in every
/// translated action, and a separate class only because its skill timer runs seven seconds against
/// five.</b> That timer carries nothing this port can cast — but a pattern missing a timer is a
/// different pattern, and merging the two would erase the only thing that distinguishes them.
/// </remarks>
[AIName("shulack_bodyguard_both")]
public class ShulackBodyguardBothAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.ArmTimer(0, 7_000),
			Do.Broadcast(ShulackCalls.Officers, ShulackCalls.Far, aboutTarget: true),
			Do.Broadcast(ShulackCalls.Alarm, ShulackCalls.Far, aboutTarget: true))),

		OnMessage = Of(Branch(99, "an officer is calling", [When.Message(ShulackCalls.Officers)],
			Do.HateMessageTarget(ShulackCalls.Officer))),
	};

	public ShulackBodyguardBothAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
