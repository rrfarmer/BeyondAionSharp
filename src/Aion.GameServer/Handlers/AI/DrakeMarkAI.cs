using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The marked drakes of Theobomos and Brusthonin. Retail pattern <c>ND2_Bst_38</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Below half health, once, it calls at twelve
/// metres naming whoever it is fighting</b> — and what answers is its drakies, which until that moment
/// have been running away from everybody.
/// <para>
/// <b>This is the pair that makes the mechanic.</b> The drake alone is an ordinary monster; the drakies
/// alone are skittish and harmless. The call is what turns a field of fleeing hatchlings into a fight,
/// and a player who pulls a drake without noticing the drakies around it finds that out at half health.
/// </para>
/// <para>
/// <b>Both provocations, one flag, and the melee branch has no <c>is_enemy</c> guard while the caster
/// branch does</b> — the fourth encounter in four entries to carry that asymmetry unchanged.
/// </para>
/// <para>
/// <b>Not translated:</b> the two skills it casts on engaging.
/// </para>
/// </remarks>
[AIName("drake_mark")]
public class DrakeMarkAI : PatternAi
{
	/// <summary>Retail's <c>6511</c>: that one, all of you.</summary>
	public const int AllOfYou = 6511;

	/// <summary>Retail's <c>range_as_meter</c>.</summary>
	private const float CallReach = 12f;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c>, shared across both provocations.</summary>
	private const int Called = 1;

	private const int Half = 50;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(7, "hit, below half", [When.HpBelow(Half), When.FirstTime(Called)],
			Do.Broadcast(AllOfYou, CallReach, aboutTarget: true))),

		OnSpelled = Of(Branch(7, "cast at, below half",
			[When.HpBelow(Half), When.CasterIsEnemy, When.FirstTime(Called)],
			Do.Broadcast(AllOfYou, CallReach, aboutTarget: true))),
	};

	public DrakeMarkAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The drakies that run from everybody until their drake calls them. Retail pattern <c>ND2_Bst_41</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A drakie that sees a player runs away for three
/// seconds</b> — while it is idle, and while it is walking a route, which are the two states retail
/// names and the only two it flees in. A drakie already fighting does not run.
/// <para>
/// <b>And when its drake calls, it stops running and comes.</b> Retail's answer is a single hate point
/// followed by a <c>switch_target</c> carrying a hundred, which is the same
/// point-then-switch shape the lich's servants use: the point is a glance and the switch is the
/// commitment.
/// </para>
/// <para>
/// <b>Not translated:</b> the skill it casts on engaging, and the <c>percent_to_add</c> of five on the
/// switch — this port has no equivalent for adding a percentage of existing hate, and on a drakie that
/// has been fleeing rather than fighting there is nothing for five percent to be five percent of.
/// Recorded, as it was for the faithful servants.
/// </para>
/// </remarks>
[AIName("drakie_mark")]
public class DrakieMarkAI : PatternAi
{
	/// <summary>Retail's <c>seconds</c> on both <c>flee_from</c> branches.</summary>
	private const int ThreeSeconds = 3;

	/// <summary>Retail's <c>points_to_add</c> on the switch.</summary>
	private const int Claim = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		// Retail splits this by npc state -- idle and walking a route -- and gives both the same body.
		// A drakie already in a fight has neither state and does not run, which is the whole of the
		// distinction.
		OnSeeUser = Of(Branch(7, "a player, and away", [When.Idle], Do.FleeFromSeen(ThreeSeconds))),

		OnMessage = Of(Branch(7, "the drake is calling", [When.Message(DrakeMarkAI.AllOfYou)],
			Do.HateMessageTarget(Claim))),
	};

	public DrakieMarkAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
