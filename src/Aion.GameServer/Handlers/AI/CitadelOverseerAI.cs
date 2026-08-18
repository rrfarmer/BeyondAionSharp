using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// The citadel overseers of the Lepharist bastion. Retail pattern <c>Xlehpar_KeA</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>It calls its labourers when it is pulled, at
/// twenty metres</b> — and again when it stops running away, on two numbers at once.
/// <para>
/// <b>Not translated:</b> the opening skill and two shouts. Its <c>9001</c> call, sent alongside
/// <c>9003</c> after fleeing, reaches the two lepharist protectors — the same pair the bastion drudges
/// fetch, and the only live listeners that number has.
/// </para>
/// </remarks>
[AIName("citadel_overseer")]
public class CitadelOverseerAI : PatternAi
{
	/// <summary>Retail's <c>9003</c>: the overseers' call.</summary>
	public const int ToMe = 9003;

	/// <summary>Retail's <c>9001</c>, sent beside it after a flight.</summary>
	public const int Rallied = 9001;

	/// <summary>Retail's <c>range_as_meter</c> when it is pulled.</summary>
	public const float PulledReach = 20f;

	/// <summary>Retail's <c>range_as_meter</c> when it stops running.</summary>
	public const float FleeReach = 15f;

	/// <summary>The narrower reach of the call it sends to the protectors.</summary>
	public const float RallyReach = 10f;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(Branch(7, "pulled", [],
			Do.ArmTimer(0, 7_000),
			Do.Broadcast(ToMe, PulledReach, aboutTarget: true))),

		OnStopFleeing = Of(Branch(6, "done running", [],
			Do.Broadcast(Rallied, RallyReach, aboutTarget: true),
			Do.Broadcast(ToMe, FleeReach, aboutTarget: true))),
	};

	public CitadelOverseerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The citadel labourers who answer them. Retail pattern <c>Xlehpar_FeC</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. <b>A hundred points on whoever the overseer
/// named</b>, and a four-second timer armed alongside it that carries a skill.
/// </remarks>
[AIName("citadel_laborer")]
public class CitadelLaborerAI : PatternAi
{
	private const int Commit = 100;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnMessage = Of(Branch(9, "the overseer is calling", [When.Message(CitadelOverseerAI.ToMe)],
			Do.ArmTimer(1, 4_000),
			Do.HateMessageTarget(Commit))),
	};

	public CitadelLaborerAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
