using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The fortress guards who call: the ranged patrols and watchguards of both factions. Retail patterns
/// <c>F5_PvP_DGuard_Ra_Ae_Broad</c> and <c>F5_PvPLight_DGuard_Ra_An_Broad</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>The moment one of them is pulled it names the
/// player to every guard within twenty-five metres.</b> That is the fortress aggro mechanic, and until
/// now this server had none of it — a raid could pick guards off one at a time.
/// <para>
/// <b>The call is unconditional, and that is worth saying because the branch looks conditional.</b>
/// Retail splits <c>on_enter_attack_state</c> on <c>is_user_flying</c> — a guard whose puller is in the
/// air, and one whose puller is not — and <b>both halves broadcast the same message at the same
/// range</b>. The flying test picks which skill it opens with, nothing else. So the structural blocker
/// that keeps <c>is_user_flying</c> out of this port does not touch the call at all.
/// </para>
/// <para>
/// <b>Not translated:</b> the opening skills the two halves choose between, and the battle timers that
/// pace the rest of the fight — every action on them is a skill index.
/// </para>
/// </remarks>
[AIName("fortress_guard_call")]
public class FortressGuardCallAI : PatternAi
{
	/// <summary>Retail's <c>23200</c>: this one. Shared by both factions' guards.</summary>
	public const int ThisOne = 23200;

	/// <summary>Retail's <c>range_as_meter</c> on every caller in the family.</summary>
	public const float CallReach = 25f;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		// Retail's two branches differ only in the skill they open with, which is blocked -- so what is
		// left of them is one call. See the class remarks.
		OnEnterAttack = Of(Branch(7, "pulled, and naming the puller", [],
			Do.Broadcast(ThisOne, CallReach, aboutTarget: true))),
	};

	public FortressGuardCallAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The fortress guards who answer: the knights and defenders of both factions. Retail patterns
/// <c>F5_PvP_DGuard_Kn_Ae</c>, <c>F5_PvPLight_DGuard_Kn_An</c> and <c>F5_RvR_DGuard_Kn_Ae</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>What it does with a call depends on whether it
/// was busy.</b> Idle, it takes a single point on the player named and goes; already fighting, it
/// turns on that player and takes a hundred.
/// <para>
/// <b>It never checks who spoke.</b> Retail's guard is <c>is_enemy who=OBJI_MESSAGE_PARAM</c> — the
/// question is whether the *player named* is this guard's enemy, not whether the caller is a friend.
/// That is what lets one message number carry both factions' fortresses: an Elyos guard standing in
/// earshot of an Asmodian call hears it and does nothing, because the player it names is not its enemy.
/// <b>A guard written the obvious way — check the sender — would have needed two numbers and retail
/// uses one.</b>
/// </para>
/// <para>
/// <b>The idle answer is a single point and it is enough</b>, because an idle guard has nothing else on
/// its list. The busy answer is a hundred, which is a real claim on a guard already holding somebody.
/// </para>
/// <para>
/// <b>Not translated:</b> retail's <c>percent_to_add=10</c> riding with the busy answer's hundred —
/// this port has no way to add a percentage of existing hate, recorded here as it was for the faithful
/// servants and the drakies. And <c>23201</c>, "protect the sender", whose three listeners differ over
/// whether they cast on the sender or on what the message named; its only sender in the 5.8 files is
/// <c>F5_PvPLight_DGuard_Fi_An</c>, and the action is a skill index either way.
/// </para>
/// </remarks>
[AIName("fortress_guard_answer")]
public class FortressGuardAnswerAI : PatternAi
{
	/// <summary>Retail's <c>point_to_add</c> on the idle answer.</summary>
	private const int Glance = 1;

	/// <summary>Retail's <c>points_to_add</c> on the busy answer.</summary>
	private const int Claim = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(
			Branch(2, "a call, and I am already fighting",
				[When.Message(FortressGuardCallAI.ThisOne), When.MessageParamIsEnemy, When.Fighting],
				Do.HateMessageTarget(Claim)),

			Branch(1, "a call, and I am not",
				[When.Message(FortressGuardCallAI.ThisOne), When.MessageParamIsEnemy],
				Do.HateMessageTarget(Glance))),
	};

	public FortressGuardAnswerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
