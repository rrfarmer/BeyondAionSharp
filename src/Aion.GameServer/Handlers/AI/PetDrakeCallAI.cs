using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The pet drakes the lizardmen and naga keep. Retail pattern <c>Lizardman_BeastA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Thirteen of them across the Abyss camps, and they
/// are the only thing in the game that answers <c>3201</c></b> — a number forty-two retail patterns
/// broadcast. One listener, a hundred and thirty callers.
/// <para>
/// <b>Its answer is a point and then a hundred</b>, in retail's usual order, so what lands is a hundred
/// and one — the same shape the tamed taygas use.
/// </para>
/// <para>
/// <b>Not translated:</b> its own <c>3298</c> call at a quarter health, which nothing live listens to;
/// the <c>3299</c> answer; and every skill.
/// </para>
/// </remarks>
[AIName("pet_drake")]
public class PetDrakeAI : PatternAi
{
	/// <summary>Retail's <c>3201</c>: get that one. Broadcast by forty-two patterns, heard by this.</summary>
	public const int GetThatOne = 3201;

	/// <summary>Retail's <c>point_to_add</c>, taken before the switch.</summary>
	private const int Glance = 1;

	/// <summary>Retail's <c>points_to_add</c> on the switch that follows.</summary>
	private const int Commit = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(8, "my handler has named one", [When.Message(GetThatOne)],
			Do.HateMessageParam(Glance),
			Do.HateMessageTarget(Commit))),
	};

	public PetDrakeAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The Abyss reward camps' ranx officers, who set their drakes on whoever pulls them. Thirty-three
/// retail patterns, all of the <c>*_ABRwd*</c> family.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>One action, thirty metres, on entering
/// combat.</b> Thirty-three retail patterns carry exactly that and nothing else on this number — ranx
/// fearblades, tribunes, medicos, sartips, conquerors, archmages — so they collapse into one class
/// without losing anything.
/// </remarks>
[AIName("reward_guard_call")]
public class RewardGuardCallAI : PatternAi
{
	/// <summary>Retail's <c>range_as_meter</c> across the whole <c>ABRwd</c> family.</summary>
	public const float CallReach = 30f;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.Broadcast(PetDrakeAI.GetThatOne, CallReach, aboutTarget: true))),
	};

	public RewardGuardCallAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The reward camps' named officers, who call the same drakes from fifty metres. Twelve retail
/// patterns, the plain <c>DrGuard_*_Reward</c> and <c>Naga_*_Reward</c> family.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The same single action at fifty metres instead of
/// thirty</b> — and that is the only difference between the two halves of the reward family. The named
/// ones reach further, which given one shared pack of drakes decides whose drakes arrive.
/// </remarks>
[AIName("reward_guard_wide_call")]
public class RewardGuardWideCallAI : PatternAi
{
	/// <summary>Retail's <c>range_as_meter</c> on the named officers.</summary>
	public const float CallReach = 50f;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.Broadcast(PetDrakeAI.GetThatOne, CallReach, aboutTarget: true))),
	};

	public RewardGuardWideCallAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The bakarma lookouts and fangsnares, who watch each other. Retail patterns <c>Lizardman_FeA</c> and
/// <c>Lizardman_FeA_Solo</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It does not call when it is pulled — it calls
/// when it sees somebody else pulled.</b> A friend below three-quarters health, once, and the drakes
/// are sent after whoever is doing it.
/// <para>
/// <b>This is the first encounter built on <see cref="Ai.FriendCombatNotice"/></b>, the event behind
/// retail's <c>on_see_friend_attacked</c> and <c>on_friend_spelled</c> — 397 and 344 patterns
/// respectively, the largest handlers this port had no event for.
/// </para>
/// <para>
/// <b>The spell branch checks the caster is an enemy and the melee branch checks nothing</b>, which is
/// the seventh encounter in this log to carry that exact asymmetry and the seventh to keep it. The
/// flag is shared across both, so a lookout calls once for one friend's beating however it is
/// delivered.
/// </para>
/// <para>
/// <b>And it calls again if it stops fleeing</b> — retail's <c>on_stop_to_flee</c>, at fifteen metres
/// rather than thirteen, naming whatever it is facing. Built; unpinnable for the reason every flee in
/// this port is.
/// </para>
/// <para>
/// <b>Not translated:</b> the shouts on all three branches, and every skill.
/// </para>
/// </remarks>
[AIName("lizardman_watch")]
public class LizardmanWatchAI : PatternAi
{
	/// <summary>Retail's <c>range_as_meter</c> on the two watching branches.</summary>
	public const float WatchReach = 13f;

	/// <summary>Retail's <c>range_as_meter</c> when it stops running.</summary>
	private const float FleeReach = 15f;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_2</c>, shared across both watching branches.</summary>
	private const int Called = 2;

	private const int ThreeQuarters = 75;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnFriendAttacked = Of(Branch(2, "a friend is being beaten",
			[When.FriendHpBelow(ThreeQuarters), When.FirstTime(Called)],
			Do.BroadcastAboutFriendsAttacker(PetDrakeAI.GetThatOne, WatchReach))),

		OnFriendSpelled = Of(Branch(1, "a friend is being cast at",
			[When.FriendsAttackerIsEnemy, When.FriendHpBelow(ThreeQuarters), When.FirstTime(Called)],
			Do.BroadcastAboutFriendsAttacker(PetDrakeAI.GetThatOne, WatchReach))),

		OnStopFleeing = Of(Branch(7, "done running", [],
			Do.Broadcast(PetDrakeAI.GetThatOne, FleeReach, aboutTarget: true))),
	};

	public LizardmanWatchAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
