using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Utils;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Vallakhan (215782), the fanatic elemental named of Udas Temple. Retail pattern
/// <c>IDTP_Fanatic_Boss_EL</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. His illusions were already in
/// <c>ai/spawn_helpers.xml</c> and already arriving; what was missing is the call that sets them on
/// somebody. Retail drops them <b>on his current target</b> and broadcasts at fifteen metres in the
/// same branch, and the illusions answer by engaging.
/// <para>
/// <b>Our thresholds are not retail's, and that is left alone rather than quietly corrected.</b> Retail
/// summons at 75%, 40% and 20% for two, two and three illusions; our table has 75%, 30% and 10% for two
/// each. The table predates this work and changing encounter data is a different job from translating a
/// pattern — recorded here so the next person sees the difference rather than discovering it.
/// </para>
/// <para>
/// <b>Not translated:</b> his shouts, his skill chain, and the condition spawn variable on his death.
/// </para>
/// </remarks>
[AIName("vallakhan")]
public class VallakhanAI : SummonerAI
{
	/// <summary>Retail's <c>6915</c>: that one, go.</summary>
	public const int SetThemOn = 6915;

	/// <summary>Retail's <c>range_as_meter</c> on all three branches.</summary>
	private const float Reach = 15f;

	public VallakhanAI(Npc owner)
		: base(owner)
	{
	}

	/// <summary>
	/// Retail broadcasts in the same branch as the summon, after it. Ours rides the hook that runs
	/// before <see cref="SummonerAI"/>'s scheduled spawn, so the call is delayed by a tick to let the
	/// illusions exist first — a broadcast to an empty room sets nothing on anybody.
	/// </summary>
	protected override void HandleBeforeSpawn(Percentage percent)
	{
		base.HandleBeforeSpawn(percent);

		VisibleObject? target = GetTarget();
		if (target == null)
			return;

		ThreadPoolManager.GetInstance().Schedule(_ =>
		{
			if (!IsDead())
				NpcMessageBus.Broadcast(GetOwner(), SetThemOn, target, Reach);
			return ValueTask.CompletedTask;
		}, 1000L);
	}
}

/// <summary>
/// Vallakhan's illusions of melancholy (281524, 216155). Retail pattern
/// <c>IDTP_Fanatic_Elementalearth2</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>An illusion that pops the instant anybody touches
/// it.</b> Its whole pattern is three ways of leaving and one way of engaging: it despawns when
/// attacked, despawns when the fight ends, and answers Vallakhan's call by going for whoever he named.
/// <para>
/// <b>They are not adds, they are a distraction with a cost.</b> Two of them land on the player
/// Vallakhan is holding and immediately attack; each takes exactly one blow to remove, so the question
/// they ask a group is whether the two seconds spent removing them are worth more than the damage they
/// do. An illusion built with any health at all would be a different fight.
/// </para>
/// <para>
/// <b>The caster's half is built now.</b> Retail's <c>on_spelled</c> pops the illusion the same way,
/// guarded on <c>is_hp_lower_than 99</c> so a spell doing no damage leaves it standing. Our engine had
/// no such handler when this first shipped; it has one now.
/// </para>
/// </remarks>
[AIName("illusion_of_melancholy")]
public class IllusionOfMelancholyAI : PatternAi
{
	/// <summary>
	/// Retail's <c>attack_most_hating</c> with no points. A freshly-placed illusion has an empty aggro
	/// list, so "most hated" means the one it was just told about; a zero-point entry is how our aggro
	/// list says that.
	/// </summary>
	private const int NoPointsJustGo = 0;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnAttacked = Of(Branch(18, "touched, and gone", [], Do.DespawnSelf())),

		// Retail guards this one on is_enemy and hp<99, so a spell that does no damage leaves the
		// illusion standing while one that lands pops it exactly as a blow does.
		OnSpelled = Of(Branch(19, "cast at, and gone",
			[When.CasterIsEnemy, When.HpBelow(99)], Do.DespawnSelf())),

		// Retail pattern <c>IDTP_Fanatic_Elementalearth2</c>. Its answer is a bare
		// <c>attack_most_hating</c> -- it goes for whoever it already hates most, and the call names
		// nobody. This was written as a zero-point hate on the message parameter, which points the
		// illusion at the caller's target instead: a different creature whenever the two disagree.
		OnMessage = Of(Branch(20, "that one, go",
			[When.Message(VallakhanAI.SetThemOn)],
			Do.SwitchTarget(Aion.GameServer.Controllers.Attack.AggroTarget.MOST_HATED))),

		OnLeaveAttack = Of(Branch(21, "the fight is over", [], Do.DespawnSelf())),
	};

	public IllusionOfMelancholyAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
