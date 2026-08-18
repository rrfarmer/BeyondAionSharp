using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The ratman camps, and the lycans they call. Retail numbers <c>1007</c> and <c>8001</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The farmers are not the fight — their owners
/// are.</b> Mumu, dundun, munmun and nunu all keep the same arrangement: a worker that is attacked
/// names its attacker, and what answers is a lycan.
/// <para>
/// <b>Two camps, two payloads.</b> A gray mane stalker commits a hundred and one to a dundun's call; a
/// kuriuta commits <b>two hundred</b> to a munmun's. The Beluslan camp answers twice as hard as the
/// Altgard one, for the same call in the same arrangement one zone north.
/// </para>
/// </remarks>
public static class RatmanCalls
{
	/// <summary>Retail's <c>1007</c>: the Altgard farmers' call.</summary>
	public const int Farmers = 1007;

	/// <summary>Retail's <c>8001</c>: the Beluslan camp's.</summary>
	public const int Camp = 8001;

	/// <summary>Retail's <c>range_as_meter</c> on the farmers' call.</summary>
	public const float FarmerReach = 12f;

	/// <summary>Retail's <c>range_as_meter</c> on the Beluslan camp's.</summary>
	public const float CampReach = 15f;

	/// <summary>The stalkers' <c>point_to_add</c>, taken before their switch.</summary>
	public const int Glance = 1;

	/// <summary>The kuriuta's, which is a hundred times it.</summary>
	public const int Notice = 100;

	/// <summary>The <c>points_to_add</c> both answers carry on the switch.</summary>
	public const int Commit = 100;
}

/// <summary>
/// The mumu and dundun farmers of Altgard. Retail patterns <c>Ratman_FnR</c> and
/// <c>Ratman_FnR_LWaSu11</c>, <c>_12</c>, <c>_13</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Every blow, no flag and no health guard</b> — a
/// farmer under attack names its attacker for as long as the attack lasts, which is the kerubiel
/// bandits' shape at a lower level.
/// <para>
/// <b>The spell branch checks the caster is an enemy and the melee branch checks nothing</b> — the
/// tenth encounter in this log to carry that asymmetry, and the tenth to keep it.
/// </para>
/// <para>
/// <b>Not translated:</b> the skill on each branch, and their <c>1017</c> call — the same event
/// broadcast a second time <em>naming themselves</em>, whose only live listeners belong to an unrelated
/// Lepharist conversation that happens to use the same number. Self-named and cross-wired; recorded
/// rather than built.
/// </para>
/// </remarks>
[AIName("ratman_farmer")]
public class RatmanFarmerAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(2, "hit", [When.HpBetween(0, 45), ],
			Do.Broadcast(RatmanCalls.Farmers, RatmanCalls.FarmerReach, aboutTarget: true))),

		OnSpelled = Of(Branch(1, "cast at", [When.HpBelow(45), When.CasterIsEnemy],
			Do.Broadcast(RatmanCalls.Farmers, RatmanCalls.FarmerReach, aboutTarget: true))),
	};

	public RatmanFarmerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The gray mane stalkers who answer them. Retail pattern <c>Lycan_KnA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A point and then a hundred</b>, so a hundred and
/// one lands — the same figure the dukaki miners and the pet drakes give.
/// </remarks>
[AIName("gray_mane_stalker")]
public class GrayManeStalkerAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "one of the farmers is calling", [When.Message(RatmanCalls.Farmers)],
			Do.HateMessageParam(RatmanCalls.Glance),
			Do.HateMessageTarget(RatmanCalls.Commit))),
	};

	public GrayManeStalkerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The munmun maned warriors and sentinels of Beluslan. Retail patterns <c>NRatman_FnA</c> and
/// <c>NRatman_RnA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>They call on being pulled rather than on being
/// hurt</b>, which is the difference between them and the Altgard farmers: the warriors announce a
/// fight, the farmers complain about one.
/// <para>
/// <b>Not translated:</b> <c>NRatman_FnA</c>'s second call, sent when it stops fleeing and naming
/// <c>OBJI_FLEE_FROM</c> — <b>the object it ran away from</b>, which this port does not retain.
/// Every other blocked param in this log is a skill index or a self-name; this is the first that has
/// no equivalent at all.
/// </para>
/// </remarks>
[AIName("munmun_warrior")]
public class MunmunWarriorAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(1, "pulled", [],
			Do.Broadcast(RatmanCalls.Camp, RatmanCalls.CampReach, aboutTarget: true))),
	};

	public MunmunWarriorAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The nunu farmers. Retail pattern <c>NRatman_FnC</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It calls when it is nearly dead, and when
/// somebody else is.</b> Below a third health, once; and once more when it watches a friend killed —
/// on that branch naming <b>the killer</b> rather than its own attacker.
/// <para>
/// <b>Two flags, so each fires once.</b> A nunu beaten low that then sees a neighbour fall calls
/// twice, and never a third time.
/// </para>
/// </remarks>
[AIName("nunu_farmer")]
public class NunuFarmerAI : PatternAi
{
	private const int CalledHurt = 1;

	private const int CalledForAFriend = 2;

	private const int Third = 35;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(3, "hit, below a third",
			[When.HpBelow(Third), When.FirstTime(CalledHurt)],
			Do.Broadcast(RatmanCalls.Camp, RatmanCalls.CampReach, aboutTarget: true))),

		OnSpelled = Of(Branch(2, "cast at, below a third",
			[When.HpBelow(Third), When.FirstTime(CalledHurt)],
			Do.Broadcast(RatmanCalls.Camp, RatmanCalls.CampReach, aboutTarget: true))),

		OnFriendKilled = Of(Branch(1, "they killed one of us", [When.FirstTime(CalledForAFriend)],
			Do.BroadcastAboutFriendsKiller(RatmanCalls.Camp, RatmanCalls.CampReach))),
	};

	public NunuFarmerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The munmun patrols. Retail pattern <c>NRatman_RnC</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It calls on being pulled like the warriors, and
/// again below a third on a seven-second clock</b> — the only ratman in either camp that calls twice
/// for itself.
/// </remarks>
[AIName("munmun_patrol")]
public class MunmunPatrolAI : PatternAi
{
	private const int CallTimer = 0;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(2, "pulled", [],
			Do.ArmTimer(CallTimer, 7_000),
			Do.Broadcast(RatmanCalls.Camp, RatmanCalls.CampReach, aboutTarget: true))),

		OnBattleTimer = Of(Branch(1, "below a third, once",
			[When.Timer(CallTimer), When.HpBelow(35), When.FirstTime(1)],
			Do.ArmTimer(CallTimer, 7_000),
			Do.Broadcast(RatmanCalls.Camp, RatmanCalls.CampReach, aboutTarget: true))),
	};

	public MunmunPatrolAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The kuriuta who answer the Beluslan camp. Retail pattern <c>NLycan_KeA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A hundred and then a hundred, so two hundred
/// lands</b> — twice what the gray mane stalkers give their own farmers.
/// </remarks>
[AIName("kuriuta")]
public class KuriutaAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "the camp is calling", [When.Message(RatmanCalls.Camp)],
			Do.HateMessageParam(RatmanCalls.Notice),
			Do.HateMessageTarget(RatmanCalls.Commit))),
	};

	public KuriutaAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
