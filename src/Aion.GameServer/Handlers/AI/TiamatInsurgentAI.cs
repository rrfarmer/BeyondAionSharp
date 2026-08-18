using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The Tiamat Remnant insurgent scouts. Retail pattern <c>TR_Drakan_As_Broad_First_solo</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Twelve seconds into a fight it names its target
/// once, at twenty metres</b> — and the infantry answer with three hundred points, which is the largest
/// single answer to a field call anywhere in this log.
/// <para>
/// <b>Twelve seconds is not a number retail writes down.</b> It is a chain: engaging arms a
/// five-second timer, that timer arms a seven-second one, and the seven-second one carries the call.
/// After it fires the chain hands over to a pair of timers that swap between themselves every fifteen
/// seconds forever, carrying skills — so the call happens exactly once and the rotation runs for the
/// rest of the fight.
/// </para>
/// <para>
/// <b>The whole four-timer chain is built even though three quarters of it does nothing here</b>, for
/// the reason Masto's spare timer was: the timers are real state, and a pattern missing one is a
/// different pattern. What they carry is skill indices.
/// </para>
/// <para><b>Not translated:</b> four skills and the shout.</para>
/// </remarks>
[AIName("insurgent_scout")]
public class InsurgentScoutAI : PatternAi
{
	/// <summary>Retail's <c>22001</c>: the scouts' call.</summary>
	public const int GetHim = 22001;

	/// <summary>Retail's <c>range_as_meter</c>.</summary>
	public const float CallReach = 20f;

	private const int Opening = 0;
	private const int CallTimer = 1;
	private const int RotationA = 2;
	private const int RotationB = 3;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(5, "engaging", [], Do.ArmTimer(Opening, 5_000))),

		OnBattleTimer = Of(
			Branch(9, "rotation, back again", [When.Timer(RotationB)],
				Do.ArmTimer(RotationA, 15_000)),

			Branch(8, "rotation, over", [When.Timer(RotationA)],
				Do.ArmTimer(RotationB, 15_000)),

			// The call, seven seconds after the opening timer -- twelve into the fight, and once,
			// because nothing ever re-arms this one.
			Branch(7, "twelve seconds in", [When.Timer(CallTimer)],
				Do.ArmTimer(RotationA, 10_000),
				Do.Broadcast(GetHim, CallReach, aboutTarget: true)),

			Branch(6, "five seconds in", [When.Timer(Opening)],
				Do.ArmTimer(CallTimer, 7_000))),
	};

	public InsurgentScoutAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The Tiamat Remnant insurgent infantry, who answer them. Retail pattern
/// <c>TR_Lizard_Basic_First</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>Three hundred points, once.</b> Retail flags the
/// branch, so an infantryman answers the first call it hears and never another — which against a caller
/// that only calls once means one commitment each, permanently.
/// <para>
/// <b>Eleven of them to four scouts.</b> Three hundred is enough to take an infantryman off whatever it
/// was holding, and the flag is what stops the camp from being re-taunted for the rest of the fight.
/// </para>
/// <para>
/// <b>Not translated:</b> nothing — this branch is a <c>switch_target</c> and its payload, and both are
/// here. It is the first answer in this log with no blocked action at all.
/// </para>
/// </remarks>
[AIName("insurgent_infantry")]
public class InsurgentInfantryAI : PatternAi
{
	/// <summary>Retail's <c>points_to_add</c> — the largest answer to a field call in this log.</summary>
	private const int Commit = 300;

	/// <summary>Retail's flag var: one answer per infantryman, ever.</summary>
	private const int Answered = 1;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(1, "a scout is calling, and I have not answered",
			[When.Message(InsurgentScoutAI.GetHim), When.FirstTime(Answered)],
			Do.HateMessageTarget(Commit))),
	};

	public InsurgentInfantryAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
