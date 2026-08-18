using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Model.GameObjects;
using static Aion.GameServer.Ai.Pattern.AiPattern;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Nochsana Training Camp's two naga wizards — the Protector (256690) and the Teleporter (256691).
/// Retail patterns <c>MiNaga_WeA</c> and <c>MiNaga_WeB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. Both were ELITEs on plain <c>aggressive</c>, and
/// between them they hold the camp's one piece of teamwork:
/// <list type="table">
/// <item><term>on engaging</term><description>each of them calls — the Protector to twenty-five
/// metres, the Teleporter to twenty — naming whoever pulled</description></item>
/// <item><term>on hearing the call</term><description>go for that player</description></item>
/// <item><term>the Teleporter only</term><description>a <b>nochsana reservist</b> lands on his quarry
/// as he engages, and a second one thirty seconds later while he is still above
/// seventy</description></item>
/// </list>
/// <para>
/// <b>The Teleporter answers the call twice over, and the two answers are not the same.</b> Retail
/// splits his <c>10004</c> handler on whether he is already fighting: if he is, he only turns to the
/// player named; if he is not, he takes hate and starts. That distinction survives here because the
/// runtime has <see cref="When.Fighting"/>, which is the first time in this log a retail branch split
/// on npc state has been ported rather than collapsed — the anuhart pet and Anuhart's subordinates
/// were both collapsed for want of exactly this guard.
/// </para>
/// <para>
/// <b>The Protector answers only once.</b> His branch carries a test-and-set flag, so the second call
/// he hears in a fight does nothing at all — which is what stops two wizards bouncing each other
/// between targets for the length of the fight.
/// </para>
/// <para>
/// <b>Not translated.</b> Retail's <c>param_obj=OBJI_EVENT_TARGET</c> on both calls, which we send as
/// the current target — the same player at the moment of engaging, and there is no other moment these
/// calls are made; five shouts; fourteen skill indices; and the three
/// <c>switch_target target=OBJI_CUR_TARGET</c> actions on the Protector and two on the Teleporter,
/// which target the object the NPC is already on and do nothing at all.
/// </para>
/// </remarks>
[AIName("nochsana_naga_protector")]
public class NochsanaNagaProtectorAI : PatternAi
{
	/// <summary>Retail's <c>10004</c>: "this one".</summary>
	public const int Call = 10004;

	/// <summary>Retail's <c>range_as_meter</c> on the Protector's call.</summary>
	private const float Reach = 25f;

	/// <summary>Retail's <c>FLAGVARI_ALPHA_1</c>, which makes him answer once a fight.</summary>
	private const int Answered = 1;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(8, "", When.Always,
				Do.ArmTimer(0, 12000),
				Do.Broadcast(Call, Reach, aboutTarget: true))),

		OnMessage = Of(
			Branch(9, "", [When.Message(Call), When.FirstTime(Answered)],
				Do.HateMessageTarget(SummonOrder.OnePoint))),
	};

	public NochsanaNagaProtectorAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}

/// <summary>
/// The Nochsana Teleporter (256691). Retail pattern <c>MiNaga_WeB</c>.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. The half of the pair that brings friends: a
/// reservist on his quarry as he engages, and one more thirty seconds in if he is still above seventy.
/// Both go when he does, on either of his two deaths or on losing the fight. See
/// <see cref="NochsanaNagaProtectorAI"/> for the call the two of them share.
/// </remarks>
[AIName("nochsana_naga_teleporter")]
public class NochsanaNagaTeleporterAI : PatternAi
{
	/// <summary><c>BMini_Castle_LizardmanFiSum_26_Ae</c> — a nochsana reservist.</summary>
	private const int Reservist = 290163;

	/// <summary>Retail's <c>SPAWN_ID_1</c>, cleared on all three of his exits.</summary>
	private const int Called = 1;

	/// <summary>Retail's <c>spawn_range</c> and <c>live_time</c> on both.</summary>
	private const float Ring = 5f;
	private const int Life = 300;

	/// <summary>Retail's <c>range_as_meter</c>, five metres shorter than the Protector's.</summary>
	private const float Reach = 20f;

	/// <summary>Retail's <c>FLAGVARI_GAMMA_1</c>: the second reservist comes once.</summary>
	private const int SecondCalled = 1;

	private static readonly AiPattern Pattern_ = new AiPattern
	{
		OnEnterAttack = Of(
			Branch(10, "", When.Always,
				Do.ArmTimer(0, 10000),
				Do.ArmTimer(1, 30000),
				Do.Broadcast(NochsanaNagaProtectorAI.Call, Reach, aboutTarget: true),
				Do.SpawnOnTarget(Reservist, Called, count: 1, range: Ring, liveSeconds: Life))),

		OnMessage = Of(
			// Already fighting: he turns, and nothing else. Retail's own split, and the runtime can
			// finally say it.
			Branch(3, "", [When.Message(NochsanaNagaProtectorAI.Call), When.Fighting],
				Do.TargetMessageParam()),

			Branch(2, "", [When.Message(NochsanaNagaProtectorAI.Call)],
				Do.HateMessageTarget(SummonOrder.OnePoint))),

		OnBattleTimer = Of(
			Branch(8, "", [When.Timer(0), When.HpBelow(50)],
				Do.ArmTimer(1, 12000)),

			Branch(7, "", [When.Timer(0), When.HpBetween(51, 100)],
				Do.ArmTimer(0, 15000)),

			Branch(6, "and one more", [When.Timer(1), When.HpBetween(71, 100),
					When.FirstTime(SecondCalled)],
				Do.ArmTimer(1, 30000),
				Do.SpawnOnTarget(Reservist, Called, count: 1, range: Ring, liveSeconds: Life)),

			Branch(5, "", [When.Timer(1)],
				Do.ArmTimer(1, 30000)),

			Branch(4, "", [When.Timer(0)],
				Do.ArmTimer(0, 15000))),

		OnLeaveAttack = Of(
			Branch(9, "", When.Always,
				Do.Despawn(Called))),

		OnDie = Of(
			Branch(12, "", When.Always,
				Do.Despawn(Called))),
	};

	public NochsanaNagaTeleporterAI(Npc owner)
		: base(owner)
	{
	}

	protected override AiPattern Pattern => Pattern_;
}
