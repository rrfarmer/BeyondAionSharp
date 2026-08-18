using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The brutal ice claw and mist mane camp of Beluslan, and the numbers it talks on.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The black claw lycans of Morheim, tuned up.</b>
/// The same shape — hunters and tamers who keep taygas — with three differences worth the reading: the
/// taygas answer with <b>five hundred</b> rather than a hundred and one, there are <b>two grades of
/// tayga</b> in the same camp answering the same call with different payloads, and the conversation
/// <b>runs both ways</b>.
/// </remarks>
public static class IceClawCalls
{
	/// <summary>Retail's <c>7006</c>: the hunters' and tamers' call, sent on engaging.</summary>
	public const int OnMe = 7006;

	/// <summary>Retail's <c>7007</c>: the hunter's second call, below half health.</summary>
	public const int Hurting = 7007;

	/// <summary>
	/// Retail's <c>7003</c>, sent beside <c>7006</c> when a hunter engages. Nothing our data places
	/// listens to it.
	/// </summary>
	public const int Unheard = 7003;

	/// <summary>Retail's <c>range_as_meter</c> on every call in the camp.</summary>
	public const float CallReach = 15f;

	/// <summary>What a ruthless tayga commits.</summary>
	public const int Ruthless = 500;

	/// <summary>What the lesser grade commits to the same call.</summary>
	public const int Lesser = 100;

	/// <summary>Retail's <c>points_to_add</c> when a tayga answers a friend's death.</summary>
	public const int ForAFriend = 100;
}

/// <summary>
/// The ice claw hunters. Retail pattern <c>nlycan_HeA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It calls twice: once when it engages and once
/// when it is losing.</b> The first call is <c>7006</c> on entering combat; the second is <c>7007</c>,
/// on a seven-second timer, the first time it is found below half health.
/// <para>
/// <b>Only the ruthless grade hears the second one</b>, so a hunter in trouble is reinforced by its
/// better taygas and not by the rest of the camp — the payload is the same five hundred either way, but
/// the audience narrows.
/// </para>
/// <para>
/// <b>Not translated:</b> three skills and two shouts; <c>7003</c>, which it sends beside its first
/// call and which nothing our data places listens to; and the <c>7008</c> answer — a tayga calling
/// <em>it</em> for help, which retail answers with a skill on the tayga. The timers that answer arms
/// are built, because a pattern missing a timer is a different pattern.
/// </para>
/// </remarks>
[AIName("ice_claw_hunter")]
public class IceClawHunterAI : PatternAi
{
	private const int SlowTimer = 0;
	private const int CallTimer = 1;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(6, "engaging", [],
			Do.ArmTimer(SlowTimer, 20_000),
			Do.ArmTimer(CallTimer, 7_000),
			Do.Broadcast(IceClawCalls.Unheard, IceClawCalls.CallReach, aboutTarget: true),
			Do.Broadcast(IceClawCalls.OnMe, IceClawCalls.CallReach, aboutTarget: true))),

		OnBattleTimer = Of(Branch(4, "below half, once",
			[When.Timer(CallTimer), When.HpBelow(50), When.FirstTime(1)],
			Do.Broadcast(IceClawCalls.Hurting, IceClawCalls.CallReach, aboutTarget: true))),

		// A tayga calling for help. What retail does with it is a skill; the timers are the rest.
		OnMessage = Of(Branch(5, "a tayga is in trouble", [When.Message(RuthlessTaygaAI.HelpMe)],
			Do.ArmTimer(SlowTimer, 20_000),
			Do.ArmTimer(CallTimer, 7_000))),
	};

	public IceClawHunterAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The brutal ice claw tamers. Retail pattern <c>NLycan_HeB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It calls on engaging like the hunters, and again
/// below a third health</b> — the same number both times, so the same taygas answer twice.
/// <para>
/// <b>Not translated:</b> its skills and shout, and two pairs of timers that exist to make it
/// <c>random_move</c> — retail's way of having a tamer shift position mid-fight, which this port has no
/// action for.
/// </para>
/// </remarks>
[AIName("ice_claw_tamer")]
public class IceClawTamerAI : PatternAi
{
	private const int CallTimer = 0;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(6, "engaging", [],
			Do.ArmTimer(CallTimer, 7_000),
			Do.Broadcast(IceClawCalls.OnMe, IceClawCalls.CallReach, aboutTarget: true))),

		OnBattleTimer = Of(Branch(3, "below a third, once",
			[When.HpBelow(35), When.Timer(CallTimer), When.FirstTime(1)],
			Do.ArmTimer(CallTimer, 12_000),
			Do.Broadcast(IceClawCalls.OnMe, IceClawCalls.CallReach, aboutTarget: true))),
	};

	public IceClawTamerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The ruthless taygas. Retail pattern <c>NLycan_Pet_A</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Five hundred points on whoever its handler
/// named</b> — the largest answer to a field call outside Panesterra, and five times what the black
/// claw taygas of Morheim give.
/// <para>
/// <b>It hears both of its handler's calls</b>, the one on engaging and the one below half health, and
/// answers each with the same five hundred. Its lesser cousin hears only the first.
/// </para>
/// <para>
/// <b>And it answers a friend's death</b> — retail's <c>on_sense_friend_killed_by_user</c>, a hundred
/// points on <c>OBJI_KILLER</c>, which is the event the black claw taygas made this port carry.
/// </para>
/// <para>
/// <b>Not built: its own call.</b> Below half health it broadcasts <c>7008</c> at ten metres
/// <em>naming itself</em>, and its handler answers with a skill on it — so the whole leg is a heal this
/// port cannot cast, aimed by a message whose payload lands on a friend and is dropped. The
/// self-named shape the silent-conversations audit now flags. Recorded; the constant is kept so the
/// hunter's listener still names the right number.
/// </para>
/// </remarks>
[AIName("ruthless_tayga")]
public class RuthlessTaygaAI : PatternAi
{
	/// <summary>Retail's <c>7008</c>: a tayga calling its handler. Self-named — see the remarks.</summary>
	public const int HelpMe = 7008;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(6, "engaging", [], Do.ArmTimer(0, 6_000))),

		OnMessage = Of(
			Branch(3, "my handler is hurting", [When.Message(IceClawCalls.Hurting)],
				Do.ArmTimer(1, 7_000),
				Do.HateMessageTarget(IceClawCalls.Ruthless)),

			Branch(2, "my handler has picked one", [When.Message(IceClawCalls.OnMe)],
				Do.ArmTimer(0, 7_000),
				Do.HateMessageTarget(IceClawCalls.Ruthless))),

		OnFriendKilled = Of(Branch(4, "they killed one of ours", [],
			Do.HateFriendsKiller(IceClawCalls.ForAFriend))),
	};

	public RuthlessTaygaAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The lesser ruthless taygas. Retail pattern <c>NLycan_Pet_B</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The same name on the nameplate and a fifth of the
/// commitment.</b> It answers its handler's opening call with a hundred where its cousin gives five
/// hundred, and it does not hear the second call at all.
/// <para>
/// <b>Two grades of the same creature in the same camp, told apart by nothing a player can see.</b>
/// Which tayga a pull brings decides whether the handler is reinforced or merely accompanied.
/// </para>
/// <para><b>Not translated:</b> the skill on its answer.</para>
/// </remarks>
[AIName("ruthless_tayga_lesser")]
public class RuthlessTaygaLesserAI : PatternAi
{
	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(2, "my handler has picked one", [When.Message(IceClawCalls.OnMe)],
			Do.HateMessageTarget(IceClawCalls.Lesser))),

		OnFriendKilled = Of(Branch(4, "they killed one of ours", [],
			Do.HateFriendsKiller(IceClawCalls.ForAFriend))),
	};

	public RuthlessTaygaLesserAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
